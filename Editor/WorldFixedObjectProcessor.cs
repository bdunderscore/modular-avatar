using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.Constraint.Components;

#else
using UnityEngine.Animations;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    internal class WorldFixedObjectProcessor
    {
        private BuildContext _context;
        private Transform _avatarTransform;
        private Transform _proxy;

        public void Process(BuildContext context)
        {
            _avatarTransform = context.AvatarRootTransform;
            _context = context;
            foreach (var target in _avatarTransform.GetComponentsInChildren<ModularAvatarWorldFixedObject>(true)
                         .OrderByDescending(x => NestCount(x.transform)))
                BuildReport.ReportingObject(target, () => Process(target));
        }

        int NestCount(Transform transform)
        {
            int count = 0;
            while (transform.parent != null) transform = transform.parent;
            return count;
        }

        void Process(ModularAvatarWorldFixedObject target)
        {
            var retargeter = new ActiveAnimationRetargeter(
                _context,
                new BoneDatabase(),
                target.transform
            );

            var proxy = CreateProxy();

            var parent = retargeter.CreateIntermediateObjects(proxy.gameObject);

            var xform = target.transform;

            var pscale = proxy.lossyScale;
            var oscale = xform.lossyScale;
            xform.localScale = new Vector3(oscale.x / pscale.x, oscale.y / pscale.y, oscale.z / pscale.z);

            if (parent.transform.Find(target.gameObject.name) != null)
            {
                target.gameObject.name = target.gameObject.name + "$" + GUID.Generate();
            }

            target.transform.SetParent(parent.transform, true);

            retargeter.FixupAnimations();

            Object.DestroyImmediate(target);
        }

        private Transform CreateProxy()
        {
            if (_proxy != null) return _proxy;

            // 78828bfbcb4cb4ce3b00de044eb2d927: Assets/FixedPrefab.prefab
            var fixedGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath("78828bfbcb4cb4ce3b00de044eb2d927"));

            var avatarRoot = _avatarTransform;
            GameObject obj = new GameObject("(MA WorldFixedRoot)");

            obj.transform.SetParent(avatarRoot, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            CreateConstraint(obj, fixedGameObject);

            _proxy = obj.transform;

            return obj.transform;
        }
#if MA_VRCSDK3_AVATARS
        private static void CreateConstraint(GameObject target, GameObject fixedGameObject)
        {
            var constraint = target.AddComponent<VRCParentConstraint>();
            constraint.Sources.Add(new VRCConstraintSource
            {
                Weight = 1.0f,
                SourceTransform = fixedGameObject.transform,
                ParentRotationOffset = Vector3.zero,
                ParentPositionOffset = Vector3.zero
            });
            constraint.IsActive = true;
            constraint.Locked = true;
        }
#else
private static void CreateConstraint(GameObject target, GameObject fixedGameObject)
        {
            var constraint = target.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource()
            {
                weight = 1.0f,
                sourceTransform = fixedGameObject.transform,
            });
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.rotationOffsets = new[] {Vector3.zero};
            constraint.translationOffsets = new[] {Vector3.zero};
        }
#endif
    }
}