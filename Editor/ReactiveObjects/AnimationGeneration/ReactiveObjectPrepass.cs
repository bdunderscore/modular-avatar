using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Reserve an animator layer for reactive object use. We do this here so that we can take advantage of MergeAnimator's
    ///     layer reference correction logic; this can go away once we have a more unified animation services API.
    /// </summary>
    internal class ReactiveObjectPrepass : Pass<ReactiveObjectPrepass>
    {
        internal const string TAG_PATH = "__MA/ShapeChanger/PrepassPlaceholder";

        protected override void Execute(ndmf.BuildContext context)
        {
            var hasShapeChanger = context.AvatarRootObject.GetComponentInChildren<ModularAvatarShapeChanger>(true) != null;
            var hasObjectSwitcher =
                context.AvatarRootObject.GetComponentInChildren<ModularAvatarObjectToggle>(true) != null;
            var hasMaterialSetter =
                context.AvatarRootObject.GetComponentInChildren<ModularAvatarMaterialSetter>(true) != null;
            if (hasShapeChanger || hasObjectSwitcher || hasMaterialSetter)
            {
                var clip = new AnimationClip();
                clip.name = "MA Shape Changer Defaults";

                var curve = new AnimationCurve();
                curve.AddKey(0, 0);
                clip.SetCurve(TAG_PATH, typeof(Transform), "localPosition.x", curve);

                // Merge using a null blend tree. This also ensures that we initialize the Merge Blend Tree system.
                var bt = new BlendTree();
                bt.name = "MA Shape Changer Defaults";
                bt.blendType = BlendTreeType.Direct;
                bt.children = new[]
                {
                    new ChildMotion
                    {
                        motion = clip,
                        timeScale = 1,
                        cycleOffset = 0,
                        directBlendParameter = MergeBlendTreePass.ALWAYS_ONE
                    }
                };
                bt.useAutomaticThresholds = false;

                // This is a hack and a half - put in a dummy path so we can find the cloned clip later on...
                var obj = new GameObject("MA SC Defaults");
                obj.transform.SetParent(context.AvatarRootTransform);
                var mambt = obj.AddComponent<ModularAvatarMergeBlendTree>();
                mambt.BlendTree = bt;
                mambt.PathMode = MergeAnimatorPathMode.Absolute;
            }
        }
    }
}