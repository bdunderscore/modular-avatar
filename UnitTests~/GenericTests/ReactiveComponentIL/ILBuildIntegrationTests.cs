using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class ILBuildIntegrationTests : TestBase
    {
        private BakeContext _bakeContext;
        private GameObject _root;
        private AnimatorServicesContext _asc;
        private VirtualAnimatorController _vac;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _root = CreateRoot("root");
            var bc = CreateContext(_root);
            _asc = bc.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            _vac = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(bc, _vac);
        }

        [Test]
        public void Build_InactiveObjectDrivenActiveByAlwaysOnNode_StartsActive()
        {
            // End-to-end regression for the pipeline-ordering bug:
            // AssignInitialStates.ProcessGraph was called before ConvertToInternalParametersTransform,
            // so the fixpoint saw no DriveInternalParameter effects and was a complete no-op.
            // Object A is inactive in the scene (activeSelf=false), but an always-on node drives
            // it active. After ILBuild.Build the ObjActive/A parameter must be 1, not 0.
            var objA = CreateChild(_root, "A");
            objA.SetActive(false);

            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveActiveState(objA, true)));

            ILBuild.Build(_bakeContext, graph);

            var paramName = _vac.Parameters.Keys.Single(k => k.Contains("ObjActive/A"));
            Assert.AreEqual(1.0f, _bakeContext.GetParameterInitialValue(paramName),
                "A is inactive (activeSelf=false) but always driven active — initial value must be 1, not 0");
        }
    }
}
