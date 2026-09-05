using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
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

            // Create a semantic target for the action groups.
            var targetObj = CreateChild(root, "target");
            var targetKey = new ObjectActiveTarget(targetObj);

            // Create 3 action groups:
            // - rule0: IsConstantActive = false (dead group, condition with IsConstant = false)
            // - rule1: IsConstantActive = false (dead group, condition with IsConstant = false)
            // - rule2: IsConstantActive = true (always-on group, condition with IsConstant = true)
            var animatedProperty = new AnimatedProperty(targetKey);

            // Dead rule 0: condition is not constant → IsConstantActive = false
            var rule0 = CreateRuleWithCondition(isConstant: false, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule0);

            // Dead rule 1: condition is not constant → IsConstantActive = false
            var rule1 = CreateRuleWithCondition(isConstant: false, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule1);

            // Always-on rule 2: condition is constant and initially active → IsConstantActive = true
            var rule2 = CreateRuleWithCondition(isConstant: true, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule2);

            var shapes = new Dictionary<object, AnimatedProperty>
            {
                { targetKey, animatedProperty }
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

            var target = new ShapeKeyTarget(renderer, "new_shape");
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

            var invalidTarget = new MaterialSlotTarget(renderer, -1);
            Assert.IsFalse(analysis.Shapes.ContainsKey(invalidTarget),
                "A negative material slot must not generate a material animation action.");
        }
        [Test]
        public void Analyze_ProducesSemanticActions()
        {
            var root = CreateRoot("root");
            var rendererObject = CreateChild(root, "renderer");
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = TrackObject(new Mesh());
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.AddBlendShapeFrame("shape", 100, new[] { Vector3.up, Vector3.up, Vector3.up },
                new Vector3[3], new Vector3[3]);
            renderer.sharedMesh = mesh;

            var material = TrackObject(new Material(Shader.Find("Sprites/Default")));
            renderer.sharedMaterials = new Material[2];
            var shapeChanger = root.AddComponent<ModularAvatarShapeChanger>();
            shapeChanger.Shapes.Add(new ChangedShape
            {
                Object = new AvatarObjectReference(rendererObject),
                ShapeName = "shape",
                ChangeType = ShapeChangeType.Set,
                Value = 42f,
            });

            var materialSetter = root.AddComponent<ModularAvatarMaterialSetter>();
            materialSetter.Objects.Add(new MaterialSwitchObject
            {
                Object = new AvatarObjectReference(rendererObject),
                MaterialIndex = 0,
                Material = material,
            });
            materialSetter.Objects.Add(new MaterialSwitchObject
            {
                Object = new AvatarObjectReference(rendererObject),
                MaterialIndex = 1,
                Material = null,
            });

            var meshCutter = root.AddComponent<ModularAvatarMeshCutter>();
            meshCutter.Object.Set(rendererObject);
            var axisFilter = root.AddComponent<VertexFilterByAxisComponent>();
            axisFilter.Axis = Vector3.up;

            var toggledObject = CreateChild(root, "toggled");
            var objectToggle = root.AddComponent<ModularAvatarObjectToggle>();
            objectToggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(toggledObject),
                Active = false,
            });

            var actions = new ReactiveObjectAnalyzer().Analyze(root).Shapes.Values
                .SelectMany(property => property.actionGroups)
                .Select(rule => rule.Action)
                .ToList();
            var shapeAction = actions.OfType<SetShapeKey>().Single();
            Assert.AreSame(renderer, shapeAction.Renderer);
            Assert.AreEqual("shape", shapeAction.ShapeName);
            Assert.AreEqual(42f, shapeAction.Value);

            var materialActions = actions.OfType<SetMaterial>().ToList();
            Assert.AreEqual(2, materialActions.Count);
            Assert.AreSame(material, materialActions.Single(action => action.MaterialIndex == 0).Material);
            Assert.IsNull(materialActions.Single(action => action.MaterialIndex == 1).Material);
            Assert.IsTrue(materialActions.All(action => action.Renderer == renderer));

            var hideAction = actions.OfType<HideMeshSection>().Single(action => action.ShouldHide);
            var expectedSelector = new VertexFilterByAxis(axisFilter, ComputeContext.NullContext);
            Assert.AreEqual(MeshSectionTarget.ForMask(renderer, expectedSelector), hideAction.Target);
            Assert.IsTrue(expectedSelector.Equals(hideAction.Selector));
            Assert.IsFalse(actions.OfType<NullAction>().Any());

            var activeAction = actions.OfType<DriveActiveState>().Single();
            Assert.AreSame(toggledObject, activeAction.Target);
            Assert.IsFalse(activeAction.Active);
        }


        [Test]
        public void ReactionDebugger_RendersSemanticActionRows()
        {
            var root = CreateRoot("root");
            var rendererObject = CreateChild(root, "renderer");
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            var controller = root.AddComponent<ModularAvatarShapeChanger>();
            var material = TrackObject(new Material(Shader.Find("Sprites/Default")));
            var selector = new VertexFilterByShape("shape", 0.001f);
            var meshTarget = MeshSectionTarget.ForShape(renderer, "shape");
            var shapes = new Dictionary<object, AnimatedProperty>();

            AddRule(new ShapeKeyTarget(renderer, "shape"), new SetShapeKey(renderer, "shape", 42f));
            AddRule(new MaterialSlotTarget(renderer, 0), new SetMaterial(renderer, 0, material));
            AddRule(new MaterialSlotTarget(renderer, 1), new SetMaterial(renderer, 1, null));
            AddRule(meshTarget, new HideMeshSection(meshTarget, selector));
            AddRule(MeshSectionTarget.ForShape(renderer, "cancelled"),
                HideMeshSection.Retain(MeshSectionTarget.ForShape(renderer, "cancelled")));
            AddRule(new ObjectActiveTarget(rendererObject), new DriveActiveState(rendererObject, false));

            var window = ScriptableObject.CreateInstance<ROSimulator>();
            try
            {
                InvokePrivate(window, "LoadUI");
                typeof(ROSimulator).GetMethod("SetAffectedBy", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, new object[] { rendererObject, shapes });

                var rows = window.rootVisualElement.Query<VisualElement>(className: "effect-group").ToList();
                Assert.AreEqual(6, rows.Count);

                var shapeDescription = LocalizedDescription("ro_sim.effect.shape_key", "shape");
                var material0Description = LocalizedDescription("ro_sim.effect.material_slot", 0);
                var material1Description = LocalizedDescription("ro_sim.effect.material_slot", 1);

                var shapeRow = RowForProperty(shapeDescription);
                Assert.AreSame(renderer, shapeRow.Q<ObjectField>("effect__target").value);
                Assert.AreEqual(42f, shapeRow.Q<FloatField>("effect__value").value);
                Assert.AreEqual(DisplayStyle.Flex, shapeRow.Q<FloatField>("effect__value").style.display.value);

                var materialRow = RowForProperty(material0Description);
                Assert.AreSame(material, materialRow.Q<ObjectField>("effect__material").value);
                var nullMaterialRow = RowForProperty(material1Description);
                Assert.IsNull(nullMaterialRow.Q<ObjectField>("effect__material").value);
                Assert.AreEqual(DisplayStyle.Flex,
                    nullMaterialRow.Q<ObjectField>("effect__material").style.display.value);
                var foldoutLabels = window.rootVisualElement.Query<Foldout>().ToList()
                    .Select(foldout => foldout.text)
                    .ToList();
                Assert.Contains(shapeDescription, foldoutLabels);
                Assert.Contains(material0Description, foldoutLabels);
                Assert.IsFalse(foldoutLabels.Any(label => label.Contains("m_Materials.Array.data")));

                var deleteRow = rows.Single(row => row.Q<TextField>("effect__deleted").style.display.value ==
                                                   DisplayStyle.Flex);
                Assert.AreEqual(selector.ToString(), deleteRow.Q<TextField>("effect__deleted").value);

                var cancellationRow = rows.Single(row =>
                    row.Q<ObjectField>("effect__target").value == renderer &&
                    row.Q<TextField>("effect__prop").style.display.value == DisplayStyle.None &&
                    row.Q<TextField>("effect__deleted").style.display.value == DisplayStyle.None);
                Assert.AreEqual(DisplayStyle.None,
                    cancellationRow.Q<FloatField>("effect__value").style.display.value);
                Assert.AreEqual(DisplayStyle.None,
                    cancellationRow.Q<ObjectField>("effect__material").style.display.value);

                var inactiveRow = rows.Single(row =>
                    row.Q<VisualElement>("effect__set-inactive").style.display.value == DisplayStyle.Flex);
                Assert.AreSame(rendererObject, inactiveRow.Q<ObjectField>("effect__target").value);

                VisualElement RowForProperty(string property)
                {
                    return rows.Single(row => row.Q<TextField>("effect__prop").value == property);
                }
                static string LocalizedDescription(string key, object value)
                {
                    return string.Format(Localization.L.GetLocalizedString(key), value);
                }

            }
            finally
            {
                Object.DestroyImmediate(window);
            }

            void AddRule(object targetKey, IAction action)
            {
                var property = new AnimatedProperty(targetKey);
                property.actionGroups.Add(new ReactionRule(action) { ControllingObject = controller });
                shapes.Add(targetKey, property);
            }

            static void InvokePrivate(ROSimulator target, string method)
            {
                typeof(ROSimulator).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(target, null);
            }
        }

        [Test]
        public void MaterialSetterPreview_AppliesNullMaterialOverride()
        {
            var root = CreateRoot("root");
            var rendererObject = CreateChild(root, "renderer");
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = TrackObject(new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 }
            });
            var originalMaterial = TrackObject(new Material(Shader.Find("Sprites/Default")));
            renderer.sharedMaterials = new[] { originalMaterial };
            var materialSetter = root.AddComponent<ModularAvatarMaterialSetter>();
            materialSetter.Objects.Add(new MaterialSwitchObject
            {
                Object = new AvatarObjectReference(rendererObject),
                MaterialIndex = 0,
                Material = null,
            });

            var proxy = CreateChild(root, "proxy").AddComponent<SkinnedMeshRenderer>();
            proxy.sharedMaterials = new[] { originalMaterial };
            var context = new ComputeContext("MaterialSetterPreview null material test");
            try
            {
                var preview = new MaterialSetterPreview();
                var groups = (IEnumerable<RenderGroup>)typeof(MaterialSetterPreview)
                    .GetMethod("GroupsForAvatar", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, new object[] { context, root })!;
                var group = groups.Single(g => g.Renderers.Contains(renderer));
                var node = preview.Instantiate(
                    group,
                    new[] { ((Renderer)renderer, (Renderer)proxy) },
                    context).Result;

                node.OnFrame(renderer, proxy);

                Assert.IsNull(proxy.sharedMaterials[0]);
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [Test]
        public void ObjectTogglePreview_UsesActiveInitialActionForInactiveObject()
        {
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var renderer = target.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = TrackObject(new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 }
            });
            target.SetActive(false);
            var toggle = root.AddComponent<ModularAvatarObjectToggle>();
            toggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target),
                Active = true,
            });

            var context = new ComputeContext("ObjectTogglePreview active override test");
            try
            {
                var preview = new ObjectSwitcherPreview();
                var groups = (IEnumerable<RenderGroup>)typeof(ObjectSwitcherPreview)
                    .GetMethod("RootsForAvatar", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, new object[] { context, root })!;
                var group = groups.Single(candidate => candidate.Renderers.Contains(renderer));

                Assert.IsTrue(group.GetData<bool>());
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [Test]
        public void ActiveSelfProxiesAreUniquePerGameObject()
        {
            var root = CreateRoot("root");
            var activeObject = CreateChild(root, "active");
            var inactiveObject = CreateChild(root, "inactive");
            inactiveObject.SetActive(false);

            var analyzer = new ReactiveObjectAnalyzer();

            Assert.AreNotEqual(
                analyzer.GetGameObjectStateProperty(activeObject),
                analyzer.GetGameObjectStateProperty(inactiveObject),
                "Distinct GameObjects must not share an ActiveSelf proxy parameter."
            );
        }

        private ReactionRule CreateRuleWithCondition(bool isConstant, bool initiallyActive, bool inverted)
        {
            var rule = new ReactionRule(new NullAction());

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
