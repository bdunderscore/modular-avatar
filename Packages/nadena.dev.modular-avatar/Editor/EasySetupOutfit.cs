using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public class EasySetupOutfit
    {
        private const int PRIORITY = 49;

        [MenuItem("GameObject/[ModularAvatar] Setup Outfit", false, PRIORITY)]
        static void SetupOutfit(MenuCommand cmd)
        {
            if (!FindBones(cmd.context,
                    out var avatarRoot, out var avatarHips, out var outfitHips)
               ) return;

            var avatarArmature = avatarHips.transform.parent;
            var outfitArmature = outfitHips.transform.parent;

            var merge = Undo.AddComponent<ModularAvatarMergeArmature>(outfitArmature.gameObject);
            merge.mergeTarget = new AvatarObjectReference();
            merge.mergeTarget.referencePath = RuntimeUtil.RelativePath(avatarRoot, avatarArmature.gameObject);
            merge.InferPrefixSuffix();
        }

        [MenuItem("GameObject/[ModularAvatar] Setup Outfit", true, PRIORITY)]
        static bool ValidateSetupOutfit()
        {
            foreach (var obj in Selection.objects)
            {
                if (!FindBones(obj, out var _, out var _, out var outfitHips)
                    || outfitHips.transform.parent.GetComponent<ModularAvatarMergeArmature>() != null)
                {
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
            if (avatarAnimator == null) return false;

            avatarHips = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject;
            if (avatarHips == null) return false;

            var outfitAnimator = outfitRoot.GetComponent<Animator>();
            if (outfitAnimator != null)
            {
                outfitHips = outfitAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject;
            }

            if (outfitHips == null)
            {
                // Heuristic search - usually there'll be root -> Armature -> (single child) Hips
                foreach (Transform child in outfitRoot.transform)
                {
                    if (child.childCount == 1)
                    {
                        if (child.GetChild(0).name.Contains(avatarHips.name))
                        {
                            outfitHips = child.GetChild(0).gameObject;
                        }
                    }
                }
            }

            return avatarHips != null && outfitHips != null;
        }
    }
}