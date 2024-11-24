/*
 * MIT License
 *
 * Copyright (c) 2022 bd_
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.armature_lock;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public enum ArmatureLockMode
    {
        Legacy,
        NotLocked,
        BaseToMerge,
        BidirectionalExact
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Merge Armature")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/merge-armature?lang=auto")]
    public class ModularAvatarMergeArmature : AvatarTagComponent, IHaveObjReferences
    {
        // Injected by HeuristicBoneMapper
        internal static Func<string, string> NormalizeBoneName;
        internal static ImmutableHashSet<string> AllBoneNames;
        
        public AvatarObjectReference mergeTarget = new AvatarObjectReference();
        public GameObject mergeTargetObject => mergeTarget.Get(this);

        public string prefix = "";
        public string suffix = "";

        [FormerlySerializedAs("locked")] public bool legacyLocked;

        public ArmatureLockMode LockMode = ArmatureLockMode.Legacy;

        public bool mangleNames = true;

        // Inserted from HeuristicBoneMapper(Editor Assembly) with InitializeOnLoadMethod
        // We use raw `boneNamePatterns` instead of `BoneToNameMap` because BoneToNameMap requires matching with normalized bone name, but normalizing makes raw prefix/suffix unavailable.
        internal static string[][] boneNamePatterns;
        private ArmatureLockController _lockController;

        internal Transform MapBone(Transform bone)
        {
            var relPath = RuntimeUtil.RelativePath(gameObject, bone.gameObject);
            
            if (relPath == null) throw new ArgumentException("Bone is not a child of this component");
            if (relPath == "") return mergeTarget.Get(this).transform;
            
            var segments = relPath.Split('/');
            
            var pointer = mergeTarget.Get(this).transform;
            foreach (var segment in segments)
            {
                if (!segment.StartsWith(prefix) || !segment.EndsWith(suffix)
                                                || segment.Length == prefix.Length + suffix.Length) return null;
                var targetObjectName = segment.Substring(prefix.Length,
                    segment.Length - prefix.Length - suffix.Length);
                pointer = pointer.Find(targetObjectName);
            }

            return pointer;
        }
        
        internal Transform FindCorrespondingBone(Transform bone, Transform baseParent)
        {
            var childName = bone.gameObject.name;

            if (!childName.StartsWith(prefix) || !childName.EndsWith(suffix)
                                              || childName.Length == prefix.Length + suffix.Length) return null;
            var targetObjectName = childName.Substring(prefix.Length,
                childName.Length - prefix.Length - suffix.Length);
            return baseParent.Find(targetObjectName);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            MigrateLockConfig();
            RuntimeUtil.delayCall(SetLockMode);
        }

        internal void ResetArmatureLock()
        {
            if (_lockController != null)
            {
                _lockController.Dispose();
                _lockController = null;
            }

            SetLockMode();
        }

        internal void SetLockMode()
        {
            if (this == null) return;

            if (_lockController == null)
            {
                _lockController = ArmatureLockController.ForMerge(this, GetBonesForLock);
                _lockController.WhenUnstable += OnUnstableLock;
            }

            _lockController.Mode = LockMode;

            _lockController.Enabled = enabled;
        }

        private void OnUnstableLock()
        {
            _lockController.Mode = LockMode = ArmatureLockMode.NotLocked;
        }

        private void MigrateLockConfig()
        {
            if (LockMode == ArmatureLockMode.Legacy)
            {
                LockMode = legacyLocked ? ArmatureLockMode.BidirectionalExact : ArmatureLockMode.BaseToMerge;
            }
        }

        private void OnEnable()
        {
            MigrateLockConfig();

            SetLockMode();
        }

        private void OnDisable()
        {
            // we use enabled instead of activeAndEnabled to ensure we track even when the GameObject is disabled
            _lockController.Enabled = enabled;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _lockController?.Dispose();
            _lockController = null;
        }

        public override void ResolveReferences()
        {
            mergeTarget?.Get(this);
        }

        private List<(Transform, Transform)> GetBonesForLock()
        {
            if (this == null) return null;

            var mergeRoot = this.transform;
            var baseRoot = mergeTarget.Get(this);

            if (baseRoot == null) return null;

            List<(Transform, Transform)> mergeBones = new List<(Transform, Transform)>();

            ScanHierarchy(mergeRoot, baseRoot.transform);

            return mergeBones;


            void ScanHierarchy(Transform merge, Transform baseBone)
            {
                foreach (Transform t in merge)
                {
                    var subMerge = t.GetComponent<ModularAvatarMergeArmature>();
                    if (subMerge != null && subMerge != this) continue;
                    
                    var baseChild = FindCorrespondingBone(t, baseBone);
                    if (baseChild != null)
                    {
                        mergeBones.Add((baseChild, t));
                        ScanHierarchy(t, baseChild);
                    }
                }
            }
        }

        class PSCandidate
        {
            public string prefix, suffix;
            public int matches;

            public PSCandidate CountMatches(ModularAvatarMergeArmature merger)
            {
                var target = merger.mergeTarget.Get(merger).transform;
                var source = merger.transform;

                var oldPrefix = merger.prefix;
                var oldSuffix = merger.suffix;
                
                try
                {
                    merger.prefix = prefix;
                    merger.suffix = suffix;
                    
                    matches = merger.GetBonesForLock().Count;
                    return this;
                }
                finally
                {
                    merger.prefix = oldPrefix;
                    merger.suffix = oldSuffix;
                }
            }

            /// <summary>
            ///  Counts the number of children which take the form prefix // heuristic bone name // suffix
            /// </summary>
            /// <returns></returns>
            public PSCandidate CountHeuristicMatches(Transform root)
            {
                int count = 1;

                Walk(root);
                
                matches = count;
                return this;
                
                void Walk(Transform t)
                {
                    foreach (Transform child in t)
                    {
                        if (child.name.StartsWith(prefix) && child.name.EndsWith(suffix))
                        {
                            var boneName = child.name.Substring(prefix.Length, child.name.Length - prefix.Length - suffix.Length);
                            boneName = NormalizeBoneName(boneName);
                            if (AllBoneNames.Contains(boneName))
                            {
                                count++;
                                Walk(child);
                            }
                        }
                    }
                }
            }
        }
        
        public void InferPrefixSuffix()
        {
            // We only infer if targeting the armature (below the Hips bone)
            var rootAnimator = RuntimeUtil.FindAvatarTransformInParents(transform)?.GetComponent<Animator>();
            if (rootAnimator == null || !rootAnimator.isHuman) return;

            var hips = rootAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || hips.transform.parent != mergeTargetObject.transform) return;

            // We also require that the attached object has exactly one child (presumably the hips)
            if (transform.childCount != 1) return;

            List<PSCandidate> candidates = new();
            
            // always consider the current configuration
            candidates.Add(new PSCandidate() {prefix = prefix, suffix = suffix}.CountMatches(this));
                        
            // Infer the prefix and suffix by comparing the names of the mergeTargetObject's hips with the child of the
            // GameObject we're attached to.
            var baseName = hips.name;
            var mergeHips = transform.GetChild(0);
            var mergeName = mergeHips.name;

            // Classic substring match
            {
                var prefixLength = mergeName.IndexOf(baseName, StringComparison.InvariantCulture);
                if (prefixLength >= 0)
                {
                    var suffixLength = mergeName.Length - prefixLength - baseName.Length;

                    candidates.Add(new PSCandidate()
                    {
                        prefix = mergeName.Substring(0, prefixLength),
                        suffix = mergeName.Substring(mergeName.Length - suffixLength)
                    }.CountMatches(this));
                }
            }

            // Heuristic match - try to see if we get a better prefix/suffix pattern if we allow for fuzzy-matching of
            // bone names. Since our goal is to minimize unnecessary renaming (and potentially failing matches), we do
            // this only if the number of heuristic matches is more than twice the number of matches from the static
            // pattern above, as using this will force most bones to be renamed.
            foreach (var hipNameCandidate in
                     boneNamePatterns[(int)HumanBodyBones.Hips].OrderByDescending(p => p.Length))
            {
                var prefixLength = mergeName.IndexOf(hipNameCandidate, StringComparison.InvariantCultureIgnoreCase);
                if (prefixLength < 0) continue;

                var suffixLength = mergeName.Length - prefixLength - hipNameCandidate.Length;

                var prefix = mergeName.Substring(0, prefixLength);
                var suffix = mergeName.Substring(mergeName.Length - suffixLength);

                var candidate = new PSCandidate
                {
                    prefix = prefix,
                    suffix = suffix
                }.CountHeuristicMatches(mergeHips);
                candidate.matches = (candidate.matches + 1) / 2;

                candidates.Add(candidate);
                break;
            }
            
            // Select which candidate to use
            var selected = candidates.OrderByDescending(c => c.matches).FirstOrDefault();
            if (selected != null && selected.matches > 0)
            {
                prefix = selected.prefix;
                suffix = selected.suffix;
            }

            if (prefix == "J_Bip_C_")
            {
                // VRM workaround
                prefix = "J_Bip_";
            }

            if (!string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix))
            {
                RuntimeUtil.MarkDirty(this);
            }
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (mergeTarget != null) yield return mergeTarget;
        }
    }
}
