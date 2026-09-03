using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnitTestsReactiveComponentIL
{
    public class PropActionTests : TestBase
    {
        private UnityBlendTreeBackend _blendTreeBackend;
        private GameObject _root;

        private sealed class UnknownAction : IAction
        {
            public object TargetKey => this;
        }

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _root = CreateRoot("root");
            var buildContext = CreateContext(_root);
            var animatorServices = buildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var controller = VirtualAnimatorController.Create(animatorServices.ControllerContext.CloneContext);
            _blendTreeBackend = new UnityBlendTreeBackend(buildContext, controller);
        }

        [Test]
        public void UnknownAction_EmitAction_LogsWarningWithoutThrowing()
        {
            var action = new UnknownAction();
            LogAssert.Expect(LogType.Warning, $"Unsupported action type: {action.GetType().FullName}");

            Assert.DoesNotThrow(() => _blendTreeBackend.EmitAction(action));
        }

        [Test]
        public void UnknownAction_ApplyBaseState_LogsWarningWithoutThrowing()
        {
            var action = new UnknownAction();
            LogAssert.Expect(LogType.Warning, $"Unsupported action type: {action.GetType().FullName}");

            Assert.DoesNotThrow(() => _blendTreeBackend.ApplyBaseState(action, actionStartsActive: false));
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
            var motion = _blendTreeBackend.BakeMotion(_blendTreeBackend.EmitAction(action)) as VirtualClip;
            Assert.IsNotNull(motion);
            var binding = ObjectBindingFor(meshFilter);

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

            _blendTreeBackend.ApplyBaseState(action, actionStartsActive: false);

            var keys = _blendTreeBackend.BaseLayerClip.GetObjectCurve(binding);
            Assert.IsNotNull(keys, "The base layer must preserve a null object-reference default as a curve.");
            Assert.AreEqual(1, keys.Length);
            Assert.AreEqual(0f, keys[0].time);
            Assert.IsNull(keys[0].value);
        }

        private EditorCurveBinding ObjectBindingFor(MeshFilter meshFilter)
        {
            return EditorCurveBinding.PPtrCurve(
                _blendTreeBackend.ObjectPathRemapper.GetVirtualPathForObject(meshFilter.gameObject),
                typeof(MeshFilter),
                "m_Mesh"
            );
        }
    }
}
