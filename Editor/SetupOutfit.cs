#region

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static nadena.dev.modular_avatar.core.editor.Localization;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ESOErrorWindow : EditorWindow
    {
        private string header;
        private string[] messageGroups;
        private static GUIStyle buttonStyle, labelStyle;
        private const float SeparatorSize = 6f;

        internal static bool Suppress = false;

        static ESOErrorWindow()
        {
        }

        internal static void InitStyles()
        {
            buttonStyle = new GUIStyle(EditorStyles.miniButtonRight);
            labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.wordWrap = true;

            buttonStyle.fixedWidth = 40f;
            buttonStyle.fixedHeight = EditorGUIUtility.singleLineHeight * 1.5f;
        }

        internal static void Show(
            string header,
            string[] messageGroups
        )
        {
            if (Suppress) return;

            InitStyles();

            var window = CreateInstance<ESOErrorWindow>();
            window.titleContent = new GUIContent("Setup Outfit");
            window.header = header;
            window.messageGroups = messageGroups;

            // Compute required window size
            var height = 0f;
            var width = 450f;

            height += SeparatorSize;
            height += EditorStyles.helpBox.CalcHeight(new GUIContent(header), width);
            foreach (var message in messageGroups)
            {
                height += 6f; // TODO: constant
                height += labelStyle.CalcHeight(new GUIContent(message), width);
            }

            height += buttonStyle.fixedHeight;
            height += SeparatorSize;

            window.minSize = new Vector2(width, height);

            window.ShowModal();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(SeparatorSize);

            EditorGUILayout.HelpBox(header, MessageType.Error);

            foreach (var message in messageGroups)
            {
                EditorGUILayout.Space(SeparatorSize);
                var style = new GUIStyle(EditorStyles.label);
                style.wordWrap = true;

                EditorGUILayout.LabelField(message, style);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("OK", buttonStyle))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();

            var finalRect = GUILayoutUtility.GetRect(SeparatorSize, SeparatorSize, GUILayout.ExpandWidth(true));

            var size = this.minSize;
            size.y = finalRect.position.y + finalRect.height;

            if (size.y > 10)
            {
                if (Vector2.Distance(this.minSize, size) > 1f)
                {
                    this.minSize = size;
                }

                if (Vector2.Distance(this.maxSize, size) > 1f)
                {
                    this.maxSize = size;
                }
            }
        }
    }

    public static class SetupOutfit
    {
        private static string[] errorMessageGroups;
        private static string errorHeader;

        [MenuItem(UnityMenuItems.GameObject_SetupOutfit, false, UnityMenuItems.GameObject_SetupOutfitOrder)]
        internal static void SetupOutfitMenu(MenuCommand cmd)
        {
            var outfitRoot = cmd.context as GameObject;

            SetupOutfitUI(outfitRoot);
        }

        /// <summary>
        ///     Executes the `Setup Outfit` operation, as if the user selected `outfitRoot` and ran Setup Outfit from the
        ///     context menu. Any errors encountered will trigger a popup error window.
        /// </summary>
        /// <param name="outfitRoot"></param>
        [PublicAPI]
        public static void SetupOutfitUI(GameObject outfitRoot)
        {
            if (!ValidateSetupOutfit(outfitRoot))
            {
                ESOErrorWindow.Show(errorHeader, errorMessageGroups);
                return;
            }

            if (!FindBones(outfitRoot,
                    out var avatarRoot, out var avatarHips, out var outfitHips)
               ) return;

            var avatarArmature = avatarHips.transform.parent;
            var outfitArmature = outfitHips.transform.parent;
            
            if (outfitArmature.GetComponent<ModularAvatarMergeArmature>() == null)
            {
                var merge = Undo.AddComponent<ModularAvatarMergeArmature>(outfitArmature.gameObject);
                merge.mergeTarget = new AvatarObjectReference();
                merge.mergeTarget.referencePath = RuntimeUtil.RelativePath(avatarRoot, avatarArmature.gameObject);
                merge.LockMode = ArmatureLockMode.BaseToMerge;
                merge.InferPrefixSuffix();

                List<Transform> subRoots = new List<Transform>();
                HeuristicBoneMapper.RenameBonesByHeuristic(merge, skipped: subRoots);

                // If the outfit has an UpperChest bone but the avatar doesn't, add an additional MergeArmature to
                // help with this
                foreach (var subRoot in subRoots)
                {
                    var subConfig = Undo.AddComponent<ModularAvatarMergeArmature>(subRoot.gameObject);
                    var parentTransform = subConfig.transform.parent;
                    var parentConfig = parentTransform.GetComponentInParent<ModularAvatarMergeArmature>();
                    var parentMapping = parentConfig.MapBone(parentTransform);

                    subConfig.mergeTarget = new AvatarObjectReference();
                    subConfig.mergeTarget.referencePath =
                        RuntimeUtil.RelativePath(avatarRoot, parentMapping.gameObject);
                    subConfig.LockMode = ArmatureLockMode.BaseToMerge;
                    subConfig.prefix = merge.prefix;
                    subConfig.suffix = merge.suffix;
                    subConfig.mangleNames = false;
                }

                var avatarRootMatchingArmature = avatarRoot.transform.Find(outfitArmature.gameObject.name);
                if (merge.prefix == "" && merge.suffix == "" && avatarRootMatchingArmature != null)
                {
                    // We have an armature whose names exactly match the root armature - this can cause some serious
                    // confusion in Unity's humanoid armature matching system. Fortunately, we can avoid this by
                    // renaming a bone close to the root; this will ensure the number of matching bones is small, and
                    // Unity's heuristics (apparently) will choose the base avatar's armature as the "true" armature.
                    outfitArmature.name += ".1";

                    // Also make sure to refresh the avatar's animator humanoid bone cache.
                    var avatarAnimator = avatarRoot.GetComponent<Animator>();
                    var humanDescription = avatarAnimator.avatar;
                    avatarAnimator.avatar = null;
                    // ReSharper disable once Unity.InefficientPropertyAccess
                    avatarAnimator.avatar = humanDescription;
                }
            }

            FixAPose(avatarRoot, outfitArmature);

            if (outfitRoot != null
                && outfitRoot.GetComponent<ModularAvatarMeshSettings>() == null
                && outfitRoot.GetComponentInParent<ModularAvatarMeshSettings>() == null)
            {
                var meshSettings = Undo.AddComponent<ModularAvatarMeshSettings>(outfitRoot.gameObject);
                Transform rootBone = null, probeAnchor = null;
                Bounds bounds = ModularAvatarMeshSettings.DEFAULT_BOUNDS;

                FindConsistentSettings(avatarRoot, avatarHips.transform, ref probeAnchor, ref rootBone, ref bounds);

                if (probeAnchor == null)
                {
                    probeAnchor = avatarHips.transform;
                }

                if (rootBone == null)
                {
                    rootBone = avatarRoot.transform;
                }

                meshSettings.InheritProbeAnchor = ModularAvatarMeshSettings.InheritMode.SetOrInherit;
                meshSettings.InheritBounds = ModularAvatarMeshSettings.InheritMode.SetOrInherit;

                meshSettings.ProbeAnchor = new AvatarObjectReference();
                meshSettings.ProbeAnchor.referencePath = RuntimeUtil.RelativePath(avatarRoot, probeAnchor.gameObject);

                meshSettings.RootBone = new AvatarObjectReference();
                meshSettings.RootBone.referencePath = RuntimeUtil.RelativePath(avatarRoot, rootBone.gameObject);
                meshSettings.Bounds = bounds;
            }
        }

        private static void FixAPose(GameObject avatarRoot, Transform outfitArmature)
        {
            var mergeArmature = outfitArmature.GetComponent<ModularAvatarMergeArmature>();
            if (mergeArmature == null) return;

            var mergeTarget = mergeArmature.mergeTarget.Get(mergeArmature)?.transform;
            if (mergeTarget == null) return;

            var rootAnimator = avatarRoot.GetComponent<Animator>();
            if (rootAnimator == null) return;

            FixSingleArm(HumanBodyBones.LeftShoulder);
            FixSingleArm(HumanBodyBones.RightShoulder);
            FixSingleArm(HumanBodyBones.LeftUpperArm);
            FixSingleArm(HumanBodyBones.RightUpperArm);

            void FixSingleArm(HumanBodyBones arm)
            {
                var lowerArm = (HumanBodyBones)((int)arm + 2);

                // check if the rotation of the arm differs, but distances and origin point are the same
                var avatarArm = rootAnimator.GetBoneTransform(arm);
                var outfitArm = avatarToOutfit(avatarArm);

                var avatarLowerArm = rootAnimator.GetBoneTransform(lowerArm);
                var outfitLowerArm = avatarToOutfit(avatarLowerArm);

                if (outfitArm == null) return;
                if (outfitLowerArm == null) return;

                if ((avatarArm.position - outfitArm.position).magnitude > 0.001f) return;

                // check relative distance to lower arm as well
                var avatarArmLength = (avatarLowerArm.position - avatarArm.position).magnitude;
                var outfitArmLength = (outfitLowerArm.position - outfitArm.position).magnitude;

                if (Mathf.Abs(avatarArmLength - outfitArmLength) > 0.001f) return;

                // Rotate the outfit arm to ensure these two points match.
                var relRot = Quaternion.FromToRotation(
                    outfitLowerArm.position - outfitArm.position,
                    avatarLowerArm.position - avatarArm.position
                );
                outfitArm.rotation = relRot * outfitArm.rotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(outfitArm);
                EditorUtility.SetDirty(outfitArm);
            }

            Transform avatarToOutfit(Transform avBone)
            {
                if (avBone == null) return null;
                if (!avBone.IsChildOf(mergeTarget)) return null;
                var parts = RuntimeUtil.RelativePath(mergeTarget.gameObject, avBone.gameObject)
                    .Split('/');
                var outfitPath = string.Join("/", parts.Select(p => mergeArmature.prefix + p + mergeArmature.suffix));
                var candidate = outfitArmature.transform.Find(outfitPath);

                if (candidate == null) return null;

                var merger = candidate.GetComponentInParent<ModularAvatarMergeArmature>();
                if (merger != mergeArmature) return null;

                return candidate;
            }
        }

        private static void FindConsistentSettings(
            GameObject avatarRoot,
            Transform avatarHips,
            ref Transform probeAnchor,
            ref Transform rootBone,
            ref Bounds bounds
        )
        {
            // We assume the renderers directly under the avatar root came from the original avatar and are _probably_
            // set consistently. If so, we use this as a basis for the new outfit's settings.

            bool firstRenderer = true;
            bool firstSkinnedMeshRenderer = true;

            foreach (Transform directChild in avatarRoot.transform)
            {
                var renderer = directChild.GetComponent<Renderer>();
                if (renderer == null) continue;

                if (firstRenderer)
                {
                    probeAnchor = renderer.probeAnchor;
                }
                else
                {
                    if (renderer.probeAnchor != probeAnchor)
                    {
                        probeAnchor = null; // inconsistent configuration
                    }
                }

                firstRenderer = false;

                var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                if (skinnedMeshRenderer == null) continue;

                if (firstSkinnedMeshRenderer)
                {
                    rootBone = skinnedMeshRenderer.rootBone;
                    bounds = skinnedMeshRenderer.localBounds;
                }
                else
                {
                    if (rootBone != skinnedMeshRenderer.rootBone)
                    {
                        rootBone = avatarHips; // inconsistent configuration
                        bounds = TransformBounds(rootBone, ModularAvatarMeshSettings.DEFAULT_BOUNDS);
                    }
                    else if (Vector3.Distance(bounds.center, skinnedMeshRenderer.bounds.center) > 0.01f
                             || Vector3.Distance(bounds.extents, skinnedMeshRenderer.bounds.extents) > 0.01f)
                    {
                        bounds = TransformBounds(rootBone, ModularAvatarMeshSettings.DEFAULT_BOUNDS);
                    }
                }

                firstSkinnedMeshRenderer = false;
            }
        }

        private static Bounds TransformBounds(Transform rootBone, Bounds bounds)
        {
            bounds.extents = bounds.extents / (Vector3.Dot(rootBone.lossyScale, Vector3.one) / 3);

            return bounds;
        }

        static bool ValidateSetupOutfit()
        {
            errorHeader = S("setup_outfit.err.header.notarget");
            errorMessageGroups = new string[] { S("setup_outfit.err.unknown") };

            if (Selection.objects.Length == 0)
            {
                errorMessageGroups = new string[] { S("setup_outfit.err.no_selection") };
                return false;
            }

            foreach (var obj in Selection.objects)
            {
                errorHeader = S_f("setup_outfit.err.header", obj.name);
                if (!(obj is GameObject gameObj)) return false;

                if (!ValidateSetupOutfit(gameObj)) return false;
            }

            return true;
        }

        private static bool ValidateSetupOutfit(GameObject gameObj)
        {
            if (gameObj == null)
            {
                errorHeader = S("setup_outfit.err.header.notarget");
                errorMessageGroups = new string[] { S("setup_outfit.err.no_selection") };
                return false;
            }

            errorHeader = S_f("setup_outfit.err.header", gameObj.name);
            var xform = gameObj.transform;

            if (!FindBones(gameObj, out var _, out var _, out var outfitHips)) return false;

            // Some users have been accidentally running Setup Outfit on the avatar itself, and/or nesting avatar
            // descriptors when transplanting outfits. Block this (and require that there be only one avdesc) by
            // refusing to run if we detect multiple avatar descriptors above the current object (or if we're run on
            // the avdesc object itself)
            var nearestAvatarTransform = RuntimeUtil.FindAvatarTransformInParents(xform);
            if (nearestAvatarTransform == null)
            {
                errorMessageGroups = new[]
                {
                    S_f("setup_outfit.err.no_avatar_descriptor", xform.gameObject.name)
                };
                return false;
            }

            if (nearestAvatarTransform == xform)
            {
                errorMessageGroups = new[]
                    { S_f("setup_outfit.err.run_on_avatar_itself", xform.gameObject.name) };
                return false;
            }

            var parent = nearestAvatarTransform.parent;
            if (parent != null && RuntimeUtil.FindAvatarTransformInParents(parent) != null)
            {
                errorMessageGroups = new[]
                {
                    S_f("setup_outfit.err.multiple_avatar_descriptors", xform.gameObject.name)
                };
                return false;
            }

            return true;
        }

        internal static bool FindBones(Object obj, out GameObject avatarRoot, out GameObject avatarHips,
            out GameObject outfitHips)
        {
            avatarHips = outfitHips = null;
            var outfitRoot = obj as GameObject;
            avatarRoot = outfitRoot != null
                ? RuntimeUtil.FindAvatarTransformInParents(outfitRoot.transform)?.gameObject
                : null;

            if (avatarRoot == null)
            {
                errorMessageGroups = new string[]
                {
                    S_f("setup_outfit.err.no_avatar_descriptor", outfitRoot != null ? outfitRoot.name : "<null>")
                };
            }

            if (outfitRoot == null || avatarRoot == null) return false;

            var avatarAnimator = avatarRoot.GetComponent<Animator>();
            if (avatarAnimator == null)
            {
                errorMessageGroups = new string[]
                {
                    S("setup_outfit.err.no_animator")
                };
                return false;
            }

            avatarHips = avatarAnimator.isHuman
                ? avatarAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject
                : null;

            if (avatarHips == null)
            {
                errorMessageGroups = new string[]
                {
                    S("setup_outfit.err.no_hips")
                };
                return false;
            }

            var outfitAnimator = outfitRoot.GetComponent<Animator>();
            if (outfitAnimator != null)
            {
                outfitHips = outfitAnimator.isHuman
                    ? outfitAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject
                    : null;
                
                if (outfitHips != null && outfitHips.transform.parent == outfitRoot.transform)
                {
                    // Sometimes broken rigs can have the hips as a direct child of the root, instead of having
                    // an intermediate Armature object. We do not currently support this kind of rig, and so we'll
                    // assume the outfit's humanoid rig is broken and move on to heuristic matching.
                    outfitHips = null;
                }
            }

            var hipsCandidates = new List<string>();

            if (outfitHips == null)
            {
                // Heuristic search - usually there'll be root -> Armature -> (single child) Hips.
                // First, look for an exact match.
                foreach (Transform child in outfitRoot.transform)
                {
                    foreach (Transform tempHip in child)
                    {
                        if (tempHip.name.Contains(avatarHips.name))
                        {
                            outfitHips = tempHip.gameObject;
                            // Prefer the first hips we find
                            break;
                        }
                    }

                    if (outfitHips != null) return true; // found an exact match, bail outgit
                }

                hipsCandidates.Add(avatarHips.name);

                // If that doesn't work out, we'll check for heuristic bone mapper mappings.
                foreach (var hbm in HeuristicBoneMapper.BoneToNameMap[HumanBodyBones.Hips])
                {
                    if (hipsCandidates[0] != hbm)
                    {
                        hipsCandidates.Add(hbm);
                    }
                }

                foreach (Transform child in outfitRoot.transform)
                {
                    foreach (Transform tempHip in child)
                    {
                        foreach (var candidate in hipsCandidates)
                        {
                            if (HeuristicBoneMapper.NormalizeName(tempHip.name).Contains(candidate))
                            {
                                outfitHips = tempHip.gameObject;
                            }
                        }
                    }
                }
            }

            if (outfitHips == null)
            {
                errorMessageGroups = new string[]
                {
                    S("setup_outfit.err.no_outfit_hips"),
                    string.Join("\n", hipsCandidates.Select(c => "・　" + c).ToArray())
                };
            }

            return avatarHips != null && outfitHips != null;
        }
    }
}
