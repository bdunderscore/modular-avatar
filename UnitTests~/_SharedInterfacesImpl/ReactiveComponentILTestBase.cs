using System;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using UnitTests.SharedInterfaces;
using UnityEditor;
using UnityEngine;

namespace UnitTests.SharedInterfacesImpl
{
    public class ReactiveComponentILTestBase : ReactiveComponentILTestSharedBase
    {
        protected GameObject avatar;
        protected Animator animator;
        protected BuildContext buildContext;
        protected AnimatorServicesContext asc;
        protected CloneContext cc;
        protected VirtualAnimatorController vac;
        
        private VirtualClip resetClip;
        private int _sensorCounter;

        
        public override void SetUp()
        {
            _sensorCounter = 0;
            avatar = CreateRoot("test");
            animator = avatar.AddComponent<Animator>();

            buildContext = new BuildContext(avatar, null, PlatformRegistry.PlatformProviders[WellKnownPlatforms.Generic], true);
            asc = buildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            cc = asc.ControllerContext.CloneContext;

            vac = VirtualAnimatorController.Create(cc, "test");
            asc.ControllerContext.Controllers[animator] = vac;
            
            resetClip = VirtualClip.Create("reset");
            var layer = vac.AddLayer(LayerPriority.Default, "reset");
            layer.StateMachine!.DefaultState = layer.StateMachine.AddState("reset", motion: resetClip);
        }

        public override void TearDown()
        {
            
        }
        
        protected GameObject CreateRoot(string name)
        {
            GameObject obj = new GameObject(name);

            return obj;
        }

        /// <summary>
        /// Creates a sensor (cube) that can be tested with a motion that moves it.
        /// </summary>
        /// <param name="motionName">Name of the motion/clip</param>
        /// <param name="motionNode">Output: the MotionNode that moves the cube</param>
        /// <param name="testFunc">Output: a function that returns true if the cube has been moved</param>
        protected void CreateSensor(string motionName, out MotionNode motionNode, out Func<bool> testFunc)
        {
            var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var uniqueName = $"{motionName}_sensor_{_sensorCounter++}";
            prim.gameObject.name = uniqueName;
            prim.transform.parent = avatar.transform;

            var clip = VirtualClip.Create(motionName);
            clip.SetFloatCurve(
                EditorCurveBinding.FloatCurve(uniqueName, typeof(Transform), "localPosition.x"),
                AnimationCurve.Constant(0, 1, 1f)
            );

            motionNode = new MotionNode(clip);
            testFunc = () => prim.transform.localPosition.x > 0.5f;
            
            resetClip.SetFloatCurve(
                EditorCurveBinding.FloatCurve(uniqueName, typeof(Transform), "localPosition.x"),
                AnimationCurve.Constant(0, 1, 0f)
            );
        }

        /// <summary>
        /// Adds a parameter to the virtual animator controller.
        /// </summary>
        /// <param name="paramName">Name of the parameter</param>
        /// <param name="defaultValue">Default value of the parameter</param>
        protected void AddParameter(string paramName, float defaultValue)
        {
            vac.Parameters = vac.Parameters.Add(paramName, new()
            {
                name = paramName,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultValue
            });
        }

        /// <summary>
        /// Bakes a root node into a new layer and deactivates the extension context.
        /// </summary>
        /// <param name="rootNode">The node to bake</param>
        protected void BakeConditions(IConditionNode rootNode)
        {
            var bakeContext = new BakeContext(cc, vac);
            var motion = rootNode.Bake(bakeContext);

            var layer = vac.AddLayer(LayerPriority.Default, "test");
            layer.DefaultWeight = 1;
            var sm = layer.StateMachine;
            sm.DefaultState = sm.AddState("test", motion: motion);
            
            buildContext.DeactivateAllExtensionContexts();
        }
    }
}