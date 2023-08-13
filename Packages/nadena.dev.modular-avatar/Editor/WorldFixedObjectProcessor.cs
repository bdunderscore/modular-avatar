using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class WorldFixedObjectProcessor
    {
        internal enum ReadyStatus
        {
            Ready,
            ParentMarked,
        }

        private VRCAvatarDescriptor _avatar;
        private Transform _proxy;

        public WorldFixedObjectProcessor(VRCAvatarDescriptor avatar)
        {
            _avatar = avatar;
        }

        public void Process(BuildContext context)
        {
            foreach (var target in _avatar.GetComponentsInChildren<ModularAvatarWorldFixedObject>(true))
                BuildReport.ReportingObject(target, () => Process(target));
        }

        void Process(ModularAvatarWorldFixedObject target)
        {
            if (Validate(target) == ReadyStatus.Ready)
            {
                var proxy = CreateProxy();

                var xform = target.transform;

                var pscale = proxy.lossyScale;
                var oscale = xform.lossyScale;
                xform.localScale = new Vector3(oscale.x / pscale.x, oscale.y / pscale.y, oscale.z / pscale.z);

                target.transform.SetParent(proxy, true);
            }

            Object.DestroyImmediate(target);
        }

        private Transform CreateProxy()
        {
            if (_proxy != null) return _proxy;

            // 78828bfbcb4cb4ce3b00de044eb2d927: Assets/FixedPrefab.prefab
            var fixedGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath("78828bfbcb4cb4ce3b00de044eb2d927"));

            var avatarRoot = _avatar.transform;
            GameObject obj = new GameObject(avatarRoot.name + " (WorldFixedRoot)");

            obj.transform.SetParent(avatarRoot, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            var constraint = obj.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource()
            {
                weight = 1.0f,
                sourceTransform = fixedGameObject.transform,
            });
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.rotationOffsets = new[] {Vector3.zero};
            constraint.translationOffsets = new[] {Vector3.zero};

            _proxy = obj.transform;

            return obj.transform;
        }

        internal ReadyStatus Validate(ModularAvatarWorldFixedObject target)
        {
            ReadyStatus status = ReadyStatus.Ready;
            Transform node = target.transform.parent;

            while (node != null)
            {
                if (node.GetComponent<ModularAvatarWorldFixedObject>()) return ReadyStatus.ParentMarked;

                node = node.parent;
            }

            return status;
        }
    }
}