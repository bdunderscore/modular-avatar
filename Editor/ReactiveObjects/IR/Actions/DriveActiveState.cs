#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class DriveActiveState : IAction
    {
        public object TargetKey => new ObjectActiveTarget(Target);
        public GameObject Target { get; set; }
        public bool Active { get; set; }

        public DriveActiveState(GameObject target, bool active)
        {
            Target = target;
            Active = active;
        }

        public override string ToString() => $"DriveActiveState({Target.name}, {Active})";
    }
}
