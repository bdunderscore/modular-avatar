#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    public class DriveActiveState : IAction
    {
        public object TargetKey => new ObjectActiveTarget(Target);

        public GameObject Target { get; set; }
        public bool Active { get; set; }

        public DriveActiveState(GameObject target, bool active)
        {
            Target = target;
            Active = active;
        }

        public override string ToString()
        {
            return $"DriveActiveState({Target.name}, {Active})";
        }

        public void SetBaseState(BakeContext context, bool actionStartsActive)
        {
            // We always set to the original active state to give other layers a chance to override
            context.BaseLayerClip.SetFloatCurve(
                EditorCurveBinding.FloatCurve(
                    context.ObjectPathRemapper.GetVirtualPathForObject(Target),
                    typeof(GameObject),
                    "m_IsActive"
                ),
                AnimationCurve.Constant(0, 1, Target.activeSelf ? 1 : 0)
            );

            // Now we can set the true initial state of the object
            if (actionStartsActive)
            {
                Target.SetActive(Active);
            }
        }
        
        public void ToMotion(BakeContext context, VirtualClip clip)
        {
            var path = context.ObjectPathRemapper.GetVirtualPathForObject(Target);

            clip.SetFloatCurve(
                EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"),
                AnimationCurve.Constant(0, 1, Active ? 1 : 0)
            );
        }
    }
}