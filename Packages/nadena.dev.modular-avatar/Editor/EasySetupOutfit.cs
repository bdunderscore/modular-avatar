using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        private static readonly GUIStyle buttonStyle, labelStyle;
        private const float SeparatorSize = 6f;

        static ESOErrorWindow()
        {
            buttonStyle = EditorStyles.miniButtonRight;
            labelStyle = EditorStyles.label;
            labelStyle.wordWrap = true;

            buttonStyle.fixedWidth = 40f;
            buttonStyle.fixedHeight = EditorGUIUtility.singleLineHeight * 1.5f;
        }

        private void OnEnable()
        {
        }

        internal static void Show(
            string header,
            string[] messageGroups
        )
        {
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

    internal class EasySetupOutfit
    {
        private const int PRIORITY = 49;
        private static string[] errorMessageGroups;
        private static string errorHeader;

        [MenuItem("GameObject/ModularAvatar/Setup Outfit", false, PRIORITY)]
        static void SetupOutfit(MenuCommand cmd)
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

            var merge = outfitArmature.GetComponent<ModularAvatarMergeArmature>();
            if (merge == null)
            {
                merge = Undo.AddComponent<ModularAvatarMergeArmature>(outfitArmature.gameObject);
                merge.mergeTarget = new AvatarObjectReference();
                merge.mergeTarget.referencePath = RuntimeUtil.RelativePath(avatarRoot, avatarArmature.gameObject);
                merge.LockMode = ArmatureLockMode.BaseToMerge;
                merge.InferPrefixSuffix();
            }

            HeuristicBoneMapper.RenameBonesByHeuristic(merge);

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
                var nearestAvatar = RuntimeUtil.FindAvatarInParents(xform);
                if (nearestAvatar == null || nearestAvatar.transform == xform)
                {
                    errorMessageGroups = new string[]
                        {S_f("setup_outfit.err.multiple_avatar_descriptors", xform.gameObject.name)};
                    return false;
                }

                var parent = nearestAvatar.transform.parent;
                if (parent != null && RuntimeUtil.FindAvatarInParents(parent) != null)
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

        private static bool FindBones(Object obj, out GameObject avatarRoot, out GameObject avatarHips,
            out GameObject outfitHips)
        {
            avatarHips = outfitHips = null;
            var outfitRoot = obj as GameObject;
            avatarRoot = outfitRoot != null
                ? RuntimeUtil.FindAvatarInParents(outfitRoot.transform)?.gameObject
                : null;
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

            var avatarBoneMappings = GetAvatarBoneMappings(avatarAnimator);
            if (!avatarBoneMappings.ContainsKey(HumanBodyBones.Hips))
            {
                errorMessageGroups = new string[]
                {
                    S("setup_outfit.err.no_hips")
                };
                return false;
            }

            // We do an explicit search for the hips bone rather than invoking the animator, as we want to control
            // traversal order.
            foreach (var maybeHips in avatarRoot.GetComponentsInChildren<Transform>())
            {
                if (maybeHips.name == avatarBoneMappings[HumanBodyBones.Hips] &&
                    !maybeHips.IsChildOf(outfitRoot.transform))
                {
                    avatarHips = maybeHips.gameObject;
                    break;
                }
            }

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
                        if (tempHip.name.Contains(avatarBoneMappings[HumanBodyBones.Hips]))
                        {
                            outfitHips = tempHip.gameObject;
                        }
                    }
                }

                hipsCandidates.Add(avatarBoneMappings[HumanBodyBones.Hips]);

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

        private static ImmutableDictionary<HumanBodyBones, string> GetAvatarBoneMappings(Animator avatarAnimator)
        {
            var avatarHuman = avatarAnimator.avatar?.humanDescription.human ?? new HumanBone[0];
            return avatarHuman
                .Where(hb => !string.IsNullOrEmpty(hb.boneName))
                .Select(hb => new KeyValuePair<HumanBodyBones, string>(
                    (HumanBodyBones) Enum.Parse(typeof(HumanBodyBones), hb.humanName.Replace(" ", "")),
                    hb.boneName
                ))
                .ToImmutableDictionary();
        }
    }
}