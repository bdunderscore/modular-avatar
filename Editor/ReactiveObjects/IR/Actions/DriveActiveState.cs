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