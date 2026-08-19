using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class PropActionTests : TestBase
    {
        private BakeContext _bakeContext;
        private GameObject _root;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _root = CreateRoot("root");
            var buildContext = CreateContext(_root);
            var animatorServices = buildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var controller = VirtualAnimatorController.Create(animatorServices.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(buildContext, controller);
        }

        [Test]
        public void NullObjectReference_ToMotion_EmitsPPtrCurveWithNullKey()
        {
            var meshFilter = CreateChild(_root, "mesh-filter").AddComponent<MeshFilter>();
            var action = new PropAction(new TargetProp
            {
                TargetObject = meshFilter,
                PropertyName = "m_Mesh",
            });
            var motion = VirtualClip.Create("motion");
            var binding = ObjectBindingFor(meshFilter);

            action.ToMotion(_bakeContext, motion);

            var keys = motion.GetObjectCurve(binding);
            Assert.IsNotNull(keys, "A null object reference must still produce an object-reference curve.");
            Assert.AreEqual(1, keys.Length);
            Assert.AreEqual(0f, keys[0].time);
            Assert.IsNull(keys[0].value);
        }

        [Test]
        public void NullObjectReference_SetBaseState_EmitsPPtrCurveWithNullKey()
        {
            var meshFilter = CreateChild(_root, "mesh-filter").AddComponent<MeshFilter>();
            var action = new PropAction(new TargetProp
            {
                TargetObject = meshFilter,
                PropertyName = "m_Mesh",
            });
            var binding = ObjectBindingFor(meshFilter);

            action.SetBaseState(_bakeContext, actionStartsActive: false);

            var keys = _bakeContext.BaseLayerClip.GetObjectCurve(binding);
            Assert.IsNotNull(keys, "The base layer must preserve a null object-reference default as a curve.");
            Assert.AreEqual(1, keys.Length);
            Assert.AreEqual(0f, keys[0].time);
            Assert.IsNull(keys[0].value);
        }

        private EditorCurveBinding ObjectBindingFor(MeshFilter meshFilter)
        {
            return EditorCurveBinding.PPtrCurve(
                _bakeContext.ObjectPathRemapper.GetVirtualPathForObject(meshFilter.gameObject),
                typeof(MeshFilter),
                "m_Mesh"
            );
        }
    }
}
