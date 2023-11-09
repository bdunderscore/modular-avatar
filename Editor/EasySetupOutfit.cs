using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static nadena.dev.modular_avatar.core.editor.Localization;

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
            buttonStyle = EditorStyles.miniButtonRight;
            labelStyle = EditorStyles.label;
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
                EditorGUILayout.LabelField(message);
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

    internal static class EasySetupOutfit
    {
        private const int PRIORITY = 49;
        private static string[] errorMessageGroups;
        private static string errorHeader;

        [MenuItem("GameObject/ModularAvatar/Setup Outfit", false, PRIORITY)]
        internal static void SetupOutfit(MenuCommand cmd)
        {
            if (!ValidateSetupOutfit())
            {
                ESOErrorWindow.Show(errorHeader, errorMessageGroups);
                return;
            }

            if (!FindBones(cmd.context,
                    out var avatarRoot, out var avatarHips, out var outfitHips)
               ) return;

            var outfitRoot = cmd.context as GameObject;
            var avatarArmature = avatarHips.transform.parent;
            var outfitArmature = outfitHips.transform.parent;

            if (outfitArmature.GetComponent<ModularAvatarMergeArmature>() == null)
            {
                var merge = Undo.AddComponent<ModularAvatarMergeArmature>(outfitArmature.gameObject);
                merge.mergeTarget = new AvatarObjectReference();
                merge.mergeTarget.referencePath = RuntimeUtil.RelativePath(avatarRoot, avatarArmature.gameObject);
                merge.LockMode = ArmatureLockMode.BaseToMerge;
                merge.InferPrefixSuffix();
                HeuristicBoneMapper.RenameBonesByHeuristic(merge);

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

                meshSettings.InheritProbeAnchor = ModularAvatarMeshSettings.InheritMode.Set;
                meshSettings.InheritBounds = ModularAvatarMeshSettings.InheritMode.Set;

                meshSettings.ProbeAnchor = new AvatarObjectReference();
                meshSettings.ProbeAnchor.referencePath = RuntimeUtil.RelativePath(avatarRoot, probeAnchor.gameObject);

                meshSettings.RootBone = new AvatarObjectReference();
                meshSettings.RootBone.referencePath = RuntimeUtil.RelativePath(avatarRoot, rootBone.gameObject);
                meshSettings.Bounds = bounds;
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
            errorMessageGroups = new string[] {S("setup_outfit.err.unknown")};

            if (Selection.objects.Length == 0)
            {
                errorMessageGroups = new string[] {S("setup_outfit.err.no_selection")};
                return false;
            }

            foreach (var obj in Selection.objects)
            {
                errorHeader = S_f("setup_outfit.err.header", obj.name);

                if (!(obj is GameObject gameObj)) return false;
                var xform = gameObj.transform;

                if (!FindBones(obj, out var _, out var _, out var outfitHips))
                {
                    return false;
                }

                // Some users have been accidentally running Setup Outfit on the avatar itself, and/or nesting avatar
                // descriptors when transplanting outfits. Block this (and require that there be only one avdesc) by
                // refusing to run if we detect multiple avatar descriptors above the current object (or if we're run on
                // the avdesc object itself)
                var nearestAvatarTransform = RuntimeUtil.FindAvatarTransformInParents(xform);
                if (nearestAvatarTransform == null || nearestAvatarTransform == xform)
                {
                    errorMessageGroups = new string[]
                        {S_f("setup_outfit.err.multiple_avatar_descriptors", xform.gameObject.name)};
                    return false;
                }

                var parent = nearestAvatarTransform.parent;
                if (parent != null && RuntimeUtil.FindAvatarTransformInParents(parent) != null)
                {
                    errorMessageGroups = new string[]
                    {
                        S_f("setup_outfit.err.no_avatar_descriptor", xform.gameObject.name)
                    };
                    return false;
                }
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

            avatarHips = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject;

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
                outfitHips = outfitAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject;
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

                    if (outfitHips != null) break;
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