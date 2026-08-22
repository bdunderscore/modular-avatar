using System.Collections;
using System.Collections.Generic;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace modular_avatar_tests
{
    /// <summary>
    /// Regression test: An off-by-one error resulted in AnalyzeConstants leaving one extra dead group when there are
    /// dead groups before the last always-on group.
    /// </summary>
    public class ReactiveObjectAnalyzerAnalyzeConstantsTest : TestBase
    {
        /// <summary>
        /// When there are dead groups before the last always-on group, AnalyzeConstants should
        /// remove ALL of them, not one fewer. The off-by-one leaves a redundant dead group.
        /// </summary>
        [Test]
        public void AnalyzeConstants_RemovesAllDeadGroupsBeforeLastAlwaysOn()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            buildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            // ReadablePropertyExtension depends on AnimatorServicesContext
            buildContext.ActivateExtensionContextRecursive<ReadablePropertyExtension>();

            var analyzer = new ReactiveObjectAnalyzer(buildContext);

            // Create a target property for the action groups
            var targetObj = CreateChild(root, "target");
            var targetProp = TargetProp.ForObjectActive(targetObj);

            // Create 3 action groups:
            // - rule0: IsConstantActive = false (dead group, condition with IsConstant = false)
            // - rule1: IsConstantActive = false (dead group, condition with IsConstant = false)
            // - rule2: IsConstantActive = true (always-on group, condition with IsConstant = true)
            var animatedProperty = new AnimatedProperty(targetProp, 1.0f);

            // Dead rule 0: condition is not constant → IsConstantActive = false
            var rule0 = CreateRuleWithCondition(isConstant: false, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule0);

            // Dead rule 1: condition is not constant → IsConstantActive = false
            var rule1 = CreateRuleWithCondition(isConstant: false, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule1);

            // Always-on rule 2: condition is constant and initially active → IsConstantActive = true
            var rule2 = CreateRuleWithCondition(isConstant: true, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule2);

            var shapes = new Dictionary<TargetProp, AnimatedProperty>
            {
                { targetProp, animatedProperty }
            };

            analyzer.AnalyzeConstants(shapes);

            // After AnalyzeConstants:
            // - lastAlwaysOnGroup should be 2 (index of rule2, the only IsConstantActive=true)
            // - RemoveRange(0, lastAlwaysOnGroup) = RemoveRange(0, 2) should remove rule0 and rule1
            // - Only rule2 should remain
            //
            // BUG: The code does RemoveRange(0, lastAlwaysOnGroup - 1) = RemoveRange(0, 1)
            // which only removes rule0, leaving rule1 and rule2 (2 groups instead of 1).
            Assert.AreEqual(1, animatedProperty.actionGroups.Count,
                $"Expected 1 action group remaining after pruning, but found {animatedProperty.actionGroups.Count}. " +
                "The off-by-one in RemoveRange leaves a redundant dead group.");
        }

        [UnityTest]
        public IEnumerator CachedAnalyze_InvalidatesWhenRendererSharedMeshChanges()
        {
            var root = CreateRoot("root");
            var rendererObject = CreateChild(root, "renderer");
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            var changer = root.AddComponent<ModularAvatarShapeChanger>();
            changer.Shapes.Add(new ChangedShape
            {
                Object = new AvatarObjectReference(rendererObject),
                ShapeName = "new_shape",
                ChangeType = ShapeChangeType.Set,
                Value = 100,
            });

            var target = new TargetProp
            {
                TargetObject = renderer,
                PropertyName = ReactiveObjectAnalyzer.BlendshapePrefix + "new_shape",
            };
            var context = new ComputeContext("sharedMesh invalidation test");
            try
            {
                var initialAnalysis = ReactiveObjectAnalyzer.CachedAnalyze(context, root);
                Assert.IsFalse(initialAnalysis.Shapes.ContainsKey(target));

                var mesh = TrackObject(new Mesh());
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2 };
                var deltas = new[] { Vector3.up, Vector3.up, Vector3.up };
                mesh.AddBlendShapeFrame("new_shape", 100, deltas, new Vector3[3], new Vector3[3]);

                var serializedRenderer = new SerializedObject(renderer);
                serializedRenderer.FindProperty("m_Mesh").objectReferenceValue = mesh;
                serializedRenderer.ApplyModifiedProperties();
                yield return null;
                ComputeContext.FlushInvalidates();

                Assert.IsTrue(context.IsInvalidated,
                    "Changing a renderer's sharedMesh must invalidate the cached reactive analysis.");

                var refreshedContext = new ComputeContext("sharedMesh refreshed analysis test");
                try
                {
                    var refreshedAnalysis = ReactiveObjectAnalyzer.CachedAnalyze(refreshedContext, root);
                    Assert.IsTrue(refreshedAnalysis.Shapes.ContainsKey(target),
                        "The refreshed analysis must register the shape provided by the new mesh.");
                }
                finally
                {
                    refreshedContext.Invalidate();
                    ComputeContext.FlushInvalidates();
                }
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }


        [Test]
        public void Analyze_IgnoresNegativeMaterialSetterSlot()
        {
            var root = CreateRoot("root");
            var rendererObject = CreateChild(root, "renderer");
            var renderer = rendererObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new Material[1];

            var setter = root.AddComponent<ModularAvatarMaterialSetter>();
            setter.Objects.Add(new MaterialSwitchObject
            {
                Object = new AvatarObjectReference(rendererObject),
                MaterialIndex = -1,
            });

            ReactiveObjectAnalyzer.AnalysisResult analysis = default;
            Assert.DoesNotThrow(() => analysis = new ReactiveObjectAnalyzer().Analyze(root));

            var invalidTarget = new TargetProp
            {
                TargetObject = renderer,
                PropertyName = "m_Materials.Array.data[-1]",
            };
            Assert.IsFalse(analysis.Shapes.ContainsKey(invalidTarget),
                "A negative material slot must not generate a material animation action.");
        }

        private ReactionRule CreateRuleWithCondition(bool isConstant, bool initiallyActive, bool inverted)
        {
            var targetProp = new TargetProp { TargetObject = null, PropertyName = "test" };
            var rule = new ReactionRule(targetProp, 1.0f);
            rule.Inverted = inverted;

            // Create a condition with ReferenceObject = null so AnalyzeConstants won't modify IsConstant
            var condition = new ControlCondition
            {
                Parameter = "test_param",
                IsConstant = isConstant,
                // Set values so InitiallyActive = true: InitialValue must be in (ParameterValueLo, ParameterValueHi)
                InitialValue = 0.5f,
                ParameterValueLo = 0.0f,
                ParameterValueHi = 1.0f,
                ReferenceObject = null // Prevents AnalyzeConstants from overriding IsConstant
            };

            rule.ControllingConditions.Add(condition);
            return rule;
        }
    }
}
