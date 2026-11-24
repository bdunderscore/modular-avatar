#if MA_VRCSDK3_AVATARS

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AvatarProcessor = nadena.dev.ndmf.AvatarProcessor;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.test.ReactiveComponent
{
    public class ReactiveDebugDumpTests : TestBase
    {
        private const string DumpDirectory = "Library/nadena.dev.modular-avatar/ReactiveComponentDumps";
        private readonly List<string> _testFiles = new();
        private readonly List<Object> _knownTestAssets = new();
        private bool _oldDumpEnabled;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _oldDumpEnabled = ReactiveDebugDump.Enabled;
            if (!AssetDatabase.IsValidFolder("Assets/ZZZ_Temp"))
            {
                AssetDatabase.CreateFolder("Assets", "ZZZ_Temp");
            }
            ReactiveDebugDump.Enabled = false;
        }

        [TearDown]
        public override void Teardown()
        {
            ReactiveDebugDump.Enabled = _oldDumpEnabled;

            foreach (var path in _testFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }

            foreach (var asset in _knownTestAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset, true);
            }

            // The importer intentionally persists its replay prefab and synthetic assets below the TestBase temp
            // folder. Remove those first so TestBase never attempts DestroyImmediate on a persisted test asset.
            AssetDatabase.DeleteAsset("Assets/ZZZ_Temp");
            FileUtil.DeleteFileOrDirectory("Assets/ZZZ_Temp");
            base.Teardown();
        }

        [Test]
        public void DisabledDump_DoesNotWriteAndStillBuildsToggle()
        {
            var root = CreateToggleAvatar(out var target);
            var before = DumpFiles();

            AvatarProcessor.ProcessAvatar(root);

            Assert.That(target.activeSelf, Is.True);
            CollectionAssert.AreEquivalent(before, DumpFiles());
            Assert.That(((AnimatorController)FindFxController(root).animatorController).parameters
                .Any(p => p.name == "outfit"), Is.True);
        }

        [Test]
        public void EnabledDump_CapturesPresenceOnlyActiveStateInputsWithoutLeakingSourceAnimationData()
        {
            var root = CreateToggleAvatar(out var target);
            target.name = "privacy-active-target";
            var sourceClip = AddActiveStateAnimation(root, target,
                "PRIVACY_SOURCE_CLIP_NAME_6F595E4A", 0.1234567f, 0f, 0.8765432f, 1f);
            var activeCurve = AnimationUtility.GetEditorCurve(sourceClip,
                EditorCurveBinding.FloatCurve(target.name, typeof(GameObject), "m_IsActive"))!;
            activeCurve.keys = new[]
            {
                new Keyframe(0.1234567f, -76543.25f, 31415.9f, -27182.8f),
                new Keyframe(0.8765432f, 98765.5f, -16180.3f, 14142.1f)
            };
            AnimationUtility.SetEditorCurve(sourceClip,
                EditorCurveBinding.FloatCurve(target.name, typeof(GameObject), "m_IsActive"), activeCurve);
            var objectCurveAsset = TrackKnownAsset(new Material(Shader.Find("Sprites/Default"))
            {
                name = "PRIVACY_OBJECT_CURVE_ASSET_830F91"
            });
            AnimationUtility.SetObjectReferenceCurve(sourceClip,
                EditorCurveBinding.PPtrCurve(target.name, typeof(Renderer), "m_Materials.Array.data[0]"),
                new[] { new ObjectReferenceKeyframe { time = 0.456789f, value = objectCurveAsset } });
            var inactiveOwner = CreateChild(root, "inactive-owner");
            var disabledToggle = inactiveOwner.AddComponent<ModularAvatarObjectToggle>();
            disabledToggle.Objects.Add(new ToggledObject { Object = new AvatarObjectReference(target), Active = false });
            disabledToggle.enabled = false;
            inactiveOwner.SetActive(false);
            var unrelated = CreateChild(root, "PRIVACY_UNRELATED_OBJECT_9C6B77");
            unrelated.AddComponent<PrivacySentinelComponent>().Sentinel = "PRIVACY_UNKNOWN_FIELD_1E4D20";
            ReactiveDebugDump.Enabled = true;
            var before = DumpFiles();

            AvatarProcessor.ProcessAvatar(root);

            var dump = ReadNewDump(before);
            Assert.That(dump["input"]!["platform"]!.Value<string>(), Is.EqualTo(WellKnownPlatforms.VRChatAvatar30));
            Assert.That(dump["formatVersion"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(dump["input"], Is.TypeOf<JObject>());
            Assert.That(dump.SelectTokens("input.components[*]").OfType<JObject>()
                .Any(c => c["kind"]?.Value<string>() == nameof(ModularAvatarObjectToggle)), Is.True);
            Assert.That(dump.SelectTokens("input.components[*].kind").Values<string>(),
                Does.Not.Contain(nameof(PrivacySentinelComponent)));
            var capturedOwner = dump["input"]!["objects"]!.Values<JObject>()
                .Single(record => record.Value<string>("name") == "inactive-owner");
            Assert.That(capturedOwner.Value<bool>("activeSelf"), Is.False);
            var capturedDisabledToggle = dump["input"]!["components"]!.Values<JObject>()
                .Single(record => record.Value<int?>("objectId") == capturedOwner.Value<int>("id")
                    && record.Value<string>("kind") == nameof(ModularAvatarObjectToggle));
            Assert.That(capturedDisabledToggle.Value<bool>("enabled"), Is.False);

            var animatedObject = dump["input"]!["objects"]!.Values<JObject>()
                .Single(record => record.Value<string>("name") == target.name);
            var knownToggleTarget = (JObject)capturedDisabledToggle["data"]!["m_objects"]![0]!["Object"]!["target"]!;
            Assert.That(knownToggleTarget.Value<int>("id"), Is.EqualTo(animatedObject.Value<int>("id")),
                "The explicit schema must still retain allowlisted compiler target references.");
            var activeStateAnimations = dump["input"]!["activeStateAnimations"]!.Values<JObject>().ToList();
            Assert.That(activeStateAnimations, Has.Count.EqualTo(1));
            Assert.That(activeStateAnimations[0].Properties().Select(property => property.Name),
                Is.EquivalentTo(new[] { "objectId", "path" }));
            Assert.That(activeStateAnimations[0].Value<int>("objectId"), Is.EqualTo(animatedObject.Value<int>("id")));
            Assert.That(activeStateAnimations[0].Value<string>("path"), Is.EqualTo(target.name));

            var rawDump = dump.ToString(Formatting.None);
            foreach (var sourceOnlySentinel in new[]
                     {
                         sourceClip.name, objectCurveAsset.name, "0.1234567", "-76543.25", "31415.9",
                         "-27182.8", "0.8765432", "98765.5", "-16180.3", "14142.1", "0.456789",
                         "PRIVACY_UNKNOWN_FIELD_1E4D20", "PRIVACY_UNRELATED_OBJECT_9C6B77"
                     })
            {
                Assert.That(rawDump, Does.Not.Contain(sourceOnlySentinel));
            }
            Assert.That(dump.SelectTokens("$..objectCurves"), Is.Empty);
            Assert.That(dump.SelectTokens("$..sourceClipId"), Is.Empty);
            Assert.That(dump.SelectTokens("$..sourceClipName"), Is.Empty);
            Assert.That(dump.SelectTokens("input..keys"), Is.Empty,
                "Source animation keys and sync remap keys must never enter the dump.");

            var stages = dump["stages"]!.Values<JObject>().Select(s => s["name"]!.Value<string>()).ToList();
            CollectionAssert.AreEqual(new[]
            {
                "Before backend.PreprocessGraph", "Before ILBuild.Simplify", "Before StaticProcessing.Apply",
                "Before backend.Build", "After OptimizeForBackend"
            }, stages);
            Assert.That(dump["effectGroups"]!.Values<JObject>()
                .Any(g => g["motion"] != null && g["motion"]!.Type != JTokenType.Null), Is.True);
            Assert.That(dump.SelectTokens("effectGroups[*]..floatCurves[*].propertyName")
                .Any(token => token.Value<string>() == "m_IsActive"), Is.True,
                "Generated effect-group motion must retain the expected active-state target binding.");

            var initialGraph = dump["stages"]!.Values<JObject>().First()["graphs"]!.DeepClone();
            Assert.That(initialGraph.ToString(Formatting.None), Does.Contain("outfit"));
            Assert.That(dump["stages"]!.Values<JObject>().Last()["graphs"]!.ToString(Formatting.None),
                Is.Not.EqualTo(initialGraph.ToString(Formatting.None)),
                "Stages must be snapshot values, not retained live graph references.");
        }

        [Test]
        public void EmptyAndStaticOnlyInputs_ProduceValidPartialDumps()
        {
            ReactiveDebugDump.Enabled = true;
            var beforeEmpty = DumpFiles();
            AvatarProcessor.ProcessAvatar(CreateRoot("empty"));
            var empty = ReadNewDump(beforeEmpty);
            Assert.That(empty["input"], Is.Not.Null);
            Assert.That(empty["input"]!["objects"]!.Values<JObject>().Count(), Is.EqualTo(1),
                "An empty capture retains only its avatar root.");
            Assert.That(empty["stages"], Is.TypeOf<JArray>());
            var emptyJsonPath = Path.Combine("Library", "reactive-debug-empty-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(emptyJsonPath, empty.ToString(Formatting.Indented));
            _testFiles.Add(emptyJsonPath);
            var emptyReplay = ReactiveDebugReconstruction.Reconstruct(emptyJsonPath, "Assets/ZZZ_Temp/empty-replay.prefab");
            TrackObject(emptyReplay);
            Assert.That(emptyReplay, Is.Not.Null);
            Assert.That(emptyReplay.transform.childCount, Is.Zero,
                "An empty capture must reconstruct an inspectable root without fabricated hierarchy.");

            var staticOnly = CreateRoot("static-only");
            var target = CreateChild(staticOnly, "target");
            staticOnly.AddComponent<ModularAvatarObjectToggle>().Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target), Active = true
            });
            var beforeStatic = DumpFiles();
            var staticContext = CreateContext(staticOnly, WellKnownPlatforms.Generic);
            staticContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            staticContext.ActivateExtensionContextRecursive<ReadablePropertyExtension>();
            try
            {
                new ReactiveObjectPassV2(staticContext).Execute();
            }
            finally
            {
                staticContext.DeactivateAllExtensionContexts();
            }
            var dump = ReadNewDump(beforeStatic);
            Assert.That(dump["formatVersion"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(dump["stages"], Is.TypeOf<JArray>());
        }

        [Test]
        public void EnabledProbe_WritesAvailableInputWithoutMaskingAProductionException()
        {
            var root = CreateToggleAvatar(out _);
            CreateChild(root, "missing-mesh").AddComponent<MeshRenderer>();
            ReactiveDebugDump.Enabled = true;
            var before = DumpFiles();
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var expected = new InvalidOperationException("expected production failure");

            try
            {
                var observed = Assert.Catch<InvalidOperationException>(() =>
                {
                    using var probe = ReactiveDebugProbe.Create();
                    probe.Offer("Before RC processing", root, animatorServices);
                    throw expected;
                });
                Assert.That(observed, Is.SameAs(expected), "Probe disposal must not replace the production exception.");
            }
            finally
            {
                context.DeactivateAllExtensionContexts();
            }

            var dump = ReadNewDump(before);
            Assert.That(dump["input"], Is.Not.Null);
            Assert.That(dump["input"]!["components"]!.Values<JObject>()
                    .Any(record => record.Value<string>("kind") == nameof(MeshRenderer)),
                Is.False, "An unrelated renderer is outside the closed compiler-input capture.");
        }

        [Test]
        public void DirectPass_CapturesInitialMenuParameterSourcesBeforeSentinelResolution()
        {
            var dump = CaptureInitialParameterMatrix(out _);
            var initialStates = dump["input"]!["initialParameterStates"]!.Values<JObject>().OfType<JObject>().ToList();

            CollectionAssert.AreEqual(new[]
            {
                "both", "disabled-fx", "expression-only", "fx-only", "inactive-expression", "neither",
                "standalone-neither"
            }, initialStates.Select(state => state.Value<string>("name")));
            AssertInitialState(initialStates, "both",
                expressionType: "Bool", expressionDefault: 1, fxType: "Float", fxDefault: 42.5f);
            AssertInitialState(initialStates, "expression-only",
                expressionType: "Int", expressionDefault: -999, fxType: null, fxDefault: null);
            AssertInitialState(initialStates, "fx-only",
                expressionType: null, expressionDefault: null, fxType: "Bool", fxDefault: 1);
            AssertInitialState(initialStates, "neither",
                expressionType: null, expressionDefault: null, fxType: null, fxDefault: null);
            AssertInitialState(initialStates, "inactive-expression",
                expressionType: "Float", expressionDefault: 3.25f, fxType: null, fxDefault: null);
            AssertInitialState(initialStates, "disabled-fx",
                expressionType: null, expressionDefault: null, fxType: "Int", fxDefault: 7);
            AssertInitialState(initialStates, "standalone-neither",
                expressionType: null, expressionDefault: null, fxType: null, fxDefault: null);

            Assert.That(dump["input"]!["expressionParameters"]!.Values<JObject>().OfType<JObject>()
                    .Select(parameter => parameter.Value<string>("name")),
                Is.EquivalentTo(new[] { "both", "expression-only", "inactive-expression" }));
            Assert.That(dump["input"]!["animatorParameters"]!.Values<JObject>().OfType<JObject>()
                    .Select(parameter => parameter.Value<string>("name")),
                Is.EquivalentTo(new[] { "both", "disabled-fx", "fx-only" }));
            Assert.That(dump.ToString(Formatting.None), Does.Not.Contain("unreferenced-expression")
                .And.Not.Contain("unreferenced-fx"));

            var preBackend = dump["stages"]!.Values<JObject>()
                .Single(stage => stage.Value<string>("name") == "Before backend.PreprocessGraph");
            Assert.That(preBackend.SelectTokens("graphs[*].parameterDefaults[*]")
                .OfType<JObject>().Where(parameter => parameter.Value<float>("value") == -999)
                .Select(parameter => parameter.Value<string>("name")),
                Does.Contain("both").And.Contain("expression-only").And.Contain("fx-only"),
                "All non-default controlling menu items must enter the graph through the -999 placeholder.");
            var afterPreprocess = dump["stages"]!.Values<JObject>()
                .Single(stage => stage.Value<string>("name") == "Before ILBuild.Simplify");
            Assert.That(afterPreprocess.SelectTokens("graphs[*].parameterDefaults[*].value")
                .Select(value => value.Value<float>()), Has.No.Member(-999f),
                "Backend preprocessing must resolve graph placeholders without mutating captured source metadata.");
            AssertInitialState(dump["input"]!["initialParameterStates"]!.Values<JObject>().OfType<JObject>().ToList(), "expression-only",
                expressionType: "Int", expressionDefault: -999, fxType: null, fxDefault: null);
        }

        [Test]
        public void Reconstruction_ValidInitialParameterStatesPreserveMissingSources()
        {
            var dump = CaptureInitialParameterMatrix(out var source);
            Object.DestroyImmediate(source);
            var jsonPath = Path.Combine("Library", "reactive-debug-initial-states-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(jsonPath, dump.ToString(Formatting.Indented));
            _testFiles.Add(jsonPath);

            var replay = ReactiveDebugReconstruction.Reconstruct(jsonPath, "Assets/ZZZ_Temp/initial-states.prefab");
            TrackObject(replay);
            var descriptor = replay.GetComponent<VRCAvatarDescriptor>();
            var expression = descriptor.expressionParameters!.parameters.ToDictionary(parameter => parameter.name);
            var fx = ((AnimatorController)FindFxController(replay).animatorController).parameters
                .ToDictionary(parameter => parameter.name);

            CollectionAssert.AreEquivalent(new[] { "both", "expression-only", "inactive-expression" }, expression.Keys);
            CollectionAssert.AreEquivalent(new[] { "both", "disabled-fx", "fx-only" }, fx.Keys);
            Assert.That(expression["expression-only"].defaultValue, Is.EqualTo(-999));
            Assert.That(fx["both"].defaultFloat, Is.EqualTo(42.5f));
            Assert.That(expression.ContainsKey("fx-only"), Is.False);
            Assert.That(fx.ContainsKey("expression-only"), Is.False);
            Assert.That(expression.ContainsKey("neither"), Is.False);
            Assert.That(fx.ContainsKey("neither"), Is.False);
        }

        [Test]
        public void Reconstruction_RejectsMalformedInitialParameterStatesWithoutArtifacts()
        {
            var valid = CaptureInitialParameterMatrix(out _);

            AssertMalformedMutation(valid, "initial-state-missing-presence", dump =>
                ((JObject)dump["input"]!["initialParameterStates"]![0]!["expressionParameters"]!)
                .Property("present")!.Remove());
            AssertMalformedMutation(valid, "initial-state-absent-payload", dump =>
                ((JObject)dump["input"]!["initialParameterStates"]![3]!["expressionParameters"]!)["type"] = "Bool");
            AssertMalformedMutation(valid, "initial-state-missing-present-default", dump =>
                ((JObject)dump["input"]!["initialParameterStates"]![0]!["fxLayer"]!)
                .Property("defaultValue")!.Remove());
            AssertMalformedMutation(valid, "initial-state-duplicate-name", dump =>
                ((JObject)dump["input"]!["initialParameterStates"]![1]!)["name"] = "both");
            AssertMalformedMutation(valid, "initial-state-invalid-type", dump =>
                ((JObject)dump["input"]!["initialParameterStates"]![0]!["expressionParameters"]!)["type"] = "Unknown");

            AssertMalformedMutation(valid, "initial-state-inconsistent-source", dump =>
                ((JObject)dump["input"]!["initialParameterStates"]![0]!["fxLayer"]!)["defaultValue"] = 99);
        }

        [Test]
        public void Probe_SnapshotsMutableGraphImmediately()
        {
            ReactiveDebugDump.Enabled = true;
            var before = DumpFiles();
            var graph = new ReactionGraph();
            var node = new ReactionNode(new Constant(true), new DriveInternalParameter("snapshot", true));
            graph.AddNode(node);

            using (var probe = ReactiveDebugProbe.Create())
            {
                probe.Offer("Before mutation", graph);
                node.Priority = 9;
                node.Expression = new Constant(false);
                node.Effects.Clear();
                node.Effects.Add(new DriveInternalParameter("snapshot", false));
                probe.Offer("After mutation", graph);
            }

            var stages = ReadNewDump(before)["stages"]!.Values<JObject>().ToArray();
            Assert.That(stages[0]["graphs"]![0]!["nodes"]![0]!["priority"]!.Value<int>(), Is.EqualTo(0));
            Assert.That(stages[0]["graphs"]![0]!["nodes"]![0]!["expression"]!["value"]!.Value<bool>(), Is.True);
            Assert.That(stages[1]["graphs"]![0]!["nodes"]![0]!["priority"]!.Value<int>(), Is.EqualTo(9));
            Assert.That(stages[1]["graphs"]![0]!["nodes"]![0]!["expression"]!["value"]!.Value<bool>(), Is.False);
        }

        [Test]
        public void Probe_OmitsUnknownPropertyTargetsWithoutLeakingTheirAssets()
        {
            ReactiveDebugDump.Enabled = true;
            var root = CreateToggleAvatar(out _);
            var renderer = CreateChild(root, "known-renderer").AddComponent<SkinnedMeshRenderer>();
            var knownMaterial = TrackKnownAsset(new Material(Shader.Find("Sprites/Default"))
            {
                name = "PRIVACY_KNOWN_MATERIAL_74BB21"
            });
            renderer.sharedMaterial = knownMaterial;
            var privateClip = TrackKnownAsset(new AnimationClip { name = "PRIVACY_PRIVATE_CLIP_85D43A" });
            var privateTexture = TrackKnownAsset(new Texture2D(1, 1) { name = "PRIVACY_PRIVATE_TEXTURE_C92E17" });
            var validGraph = new ReactionGraph();
            validGraph.AddNode(new ReactionNode(new Constant(true), new SetMaterial(renderer, 0, knownMaterial)));
            validGraph.AddNode(new ReactionNode(new Constant(true),
                new ObjectPropAction(new PropertyTarget(renderer, "m_Materials.Array.data[0]"), knownMaterial)));
            var unsafeNode = new ReactionNode(new Constant(true),
                new FloatPropAction(new PropertyTarget(renderer.transform, "privateSentinelProperty"), 12345.678f));
            unsafeNode.Effects.Add(new ObjectPropAction(new PropertyTarget(renderer, "m_Materials.Array.data[0]"), privateClip));
            unsafeNode.Effects.Add(new ObjectPropAction(new PropertyTarget(renderer, "m_Materials.Array.data[0]"), privateTexture));
            var before = DumpFiles();
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            try
            {
                using var probe = ReactiveDebugProbe.Create();
                probe.Offer("Before RC processing", root, animatorServices);
                probe.Offer("Known material-slot stage", validGraph);
                foreach (var unsafeAction in unsafeNode.Effects)
                {
                    var unsafeGraph = new ReactionGraph();
                    unsafeGraph.AddNode(new ReactionNode(new Constant(true), unsafeAction));
                    probe.Offer("Unsafe property stage", unsafeGraph);
                }
            }
            finally
            {
                context.DeactivateAllExtensionContexts();
            }

            var dump = ReadNewDump(before);
            Assert.That(dump["input"], Is.Not.Null, "A failed graph snapshot must not discard prior valid input.");
            Assert.That(dump["stages"]!.Values<JObject>().Select(stage => stage.Value<string>("name")),
                Does.Contain("Known material-slot stage").And.Not.Contain("Unsafe property stage"));
            Assert.That(dump.ToString(Formatting.None), Does.Not.Contain("privateSentinelProperty")
                .And.Not.Contain("otherPrivateSentinelProperty")
                .And.Not.Contain("12345.678")
                .And.Not.Contain(privateClip.name)
                .And.Not.Contain(privateTexture.name));
            Assert.That(dump.SelectTokens("stages[*]..kind").Values<string>(), Does.Contain(nameof(SetMaterial)));
        }

        [Test]
        public void Reconstruction_RoundTripsCompilerInputAndProducesRealSyntheticCutterOutput()
        {
            var source = CreateReplayFixture(out var sourceRenderer);
            ReactiveDebugDump.Enabled = true;
            var beforeOriginal = DumpFiles();
            AvatarProcessor.ProcessAvatar(source);
            var original = ReadNewDump(beforeOriginal);
            var originalSemanticGraph = CanonicalPreBackendGraph(original);
            Assert.That(original.ToString(Formatting.None), Does.Contain("\"NaN\""),
                "Non-finite source values must remain strict JSON strings.");
            var rawOriginal = original.ToString(Formatting.None);
            foreach (var excludedSourceValue in new[]
                     {
                         "PRIVACY_REPLAY_SHARED_ACTIVE_44E7D1", "17.1234", "-42.5678", "91.2468", "-13.579",
                         "82.9876", "63.2468", "-24.6813", "75.319", "m_quickSwapMode"
                     })
            {
                Assert.That(rawOriginal, Does.Not.Contain(excludedSourceValue));
            }
            Assert.That(original.SelectTokens("input..keys"), Is.Empty);
            Assert.That(original["effectGroups"]!.Values<JObject>().Any(group =>
                    group["targetKey"]?.Value<string>("kind") == "PropertyTarget"
                    && group["targetKey"]?.Value<string>("propertyName") == "m_Materials.Array.data[0]"
                    && group["motion"]?.Type != JTokenType.Null),
                Is.True, "Known material-property groups must remain available without serializing object curves.");
            Assert.That(original.SelectTokens("$..objectCurves"), Is.Empty);
            var localSyncObject = original["input"]!["objects"]!.Values<JObject>()
                .Single(record => record.Value<string>("name") == "sync-local-not-reactively-targeted");
            Assert.That(original["input"]!["components"]!.Values<JObject>().Any(component =>
                    component.Value<int?>("objectId") == localSyncObject.Value<int>("id")
                    && component.Value<string>("kind") == nameof(SkinnedMeshRenderer)),
                Is.True, "The local BlendshapeSync renderer must be captured even when no reactive action targets it.");
            Assert.That(SourceComponent(original, nameof(ModularAvatarBlendshapeSync))["data"]!["Bindings"]![0]!["LocalBlendshape"]!
                .Value<string>(), Is.Empty, "The blank local blendshape fallback must remain a compiler input.");
            var shapeTargetId = SourceComponent(original, nameof(ModularAvatarShapeChanger))["data"]!["m_shapes"]![0]!["Object"]!["target"]!["id"]!.Value<int>();
            var capturedShapeRenderer = original["input"]!["components"]!.Values<JObject>()
                .Single(component => component.Value<int?>("objectId") == shapeTargetId
                    && component.Value<string>("kind") == nameof(SkinnedMeshRenderer));
            Assert.That(capturedShapeRenderer["renderer"]!.Value<bool>("updateWhenOffscreen"),
                Is.True, "The source renderer's compiler-relevant update-when-offscreen state must be retained.");
            var componentKinds = original.SelectTokens("input.components[*].kind").Values<string>().ToHashSet();
            CollectionAssert.IsSubsetOf(new[]
            {
                nameof(ModularAvatarObjectToggle), nameof(ModularAvatarShapeChanger), nameof(ModularAvatarBlendshapeSync),
                nameof(ModularAvatarMaterialSetter), nameof(ModularAvatarMaterialSwap), nameof(ModularAvatarMeshCutter),
                nameof(ModularAvatarReactiveMove), nameof(ModularAvatarMenuItem), nameof(SkinnedMeshRenderer),
                nameof(AudioSource)
            }, componentKinds);
            Assert.That(original.SelectTokens("input.activeStateAnimations[*]").OfType<JObject>().Count(), Is.EqualTo(2),
                "Presence records are deduplicated by animated GameObject rather than source clip identity.");
            CollectionAssert.AreEqual(new[] { "Z-priority-owner", "Y-priority-owner", "A-priority-owner" },
                original["input"]!["objects"]!.Values<JObject>()
                    .Where(record => record.Value<string>("name") is "Z-priority-owner" or "Y-priority-owner" or "A-priority-owner")
                    .OrderBy(record => record.Value<int>("siblingIndex"))
                    .Select(record => record.Value<string>("name")));
            var cutterRecords = original.SelectTokens("input.components[*]")
                .OfType<JObject>().Where(c => c.Value<string>("kind") == nameof(ModularAvatarMeshCutter)).ToList();
            Assert.That(cutterRecords, Has.Count.EqualTo(3));
            Assert.That(cutterRecords[0]["selectorPartitionId"], Is.EqualTo(cutterRecords[1]["selectorPartitionId"]));
            Assert.That(cutterRecords[2]["selectorPartitionId"], Is.Not.EqualTo(cutterRecords[0]["selectorPartitionId"]));
            var sourceRendererName = sourceRenderer.gameObject.name;


            // The replay must depend solely on the dump, rather than live Unity objects or original assets.
            Object.DestroyImmediate(source);
            DestroyKnownTestAssets();
            var jsonPath = Path.Combine("Library", "reactive-debug-roundtrip-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(jsonPath, original.ToString(Formatting.Indented));
            _testFiles.Add(jsonPath);

            var requestedReplayPath = "Assets/ZZZ_Temp/reactive-debug-replay.prefab";
            var reconstructed = ReactiveDebugReconstruction.Reconstruct(jsonPath, requestedReplayPath);
            Assert.That(reconstructed, Is.Not.Null);
            var replayPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(reconstructed));
            Assert.That(replayPath, Does.EndWith(".prefab"));
            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(replayPath);
            Assert.That(prefab, Is.Not.Null, "The reconstruction must be a persisted inspectable prefab.");
            Object.DestroyImmediate(reconstructed);
            var replayInstance = TrackObject((GameObject)PrefabUtility.InstantiatePrefab(prefab));
            PrefabUtility.UnpackPrefabInstance(replayInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var beforeReplay = DumpFiles();
            AvatarProcessor.ProcessAvatar(replayInstance);
            var replay = ReadNewDump(beforeReplay);
            Assert.That(CanonicalPreBackendGraph(replay), Is.EqualTo(originalSemanticGraph));

            var replayRenderer = replayInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(r => r.gameObject.name == sourceRendererName);
            Assert.That(replayRenderer.sharedMesh, Is.Not.Null);
            Assert.That(replayRenderer.sharedMesh!.triangles.Length, Is.GreaterThan(0),
                "Menu-controlled cutters use dynamic NaNimation and retain the synthetic mesh geometry.");
            Assert.That(HasNaNimationCurve(replayInstance), Is.True,
                "Dynamic cutter output must contain a generated NaNimation curve, not only a reconstructed component.");
            Assert.That(replayInstance.GetComponentsInChildren<ModularAvatarMeshCutter>(true), Is.Empty,
                "A normal build must consume reconstructed source components.");
        }
        [Test]
        public void Probe_RepresentsSharedAndCyclicVirtualMotionByReference()
        {
            ReactiveDebugDump.Enabled = true;
            var before = DumpFiles();
            var group = new EffectGroup(new SimplePropProcessor(), new ParameterTarget("motion"),
                new List<ReactionNode>(), 0);
            var sharedClip = VirtualClip.Create("shared");
            var cyclic = VirtualBlendTree.Create("cycle");
            cyclic.Children = cyclic.Children.Add(new VirtualBlendTree.VirtualChildMotion { Motion = cyclic });
            var root = VirtualBlendTree.Create("root");
            root.Children = root.Children
                .Add(new VirtualBlendTree.VirtualChildMotion { Motion = sharedClip })
                .Add(new VirtualBlendTree.VirtualChildMotion { Motion = sharedClip })
                .Add(new VirtualBlendTree.VirtualChildMotion { Motion = cyclic });

            using (var probe = ReactiveDebugProbe.Create())
            {
                probe.Offer("Effect group blend tree", group, root);
            }

            var motion = ReadNewDump(before)["effectGroups"]![0]!["motion"]!;
            Assert.That(motion.SelectTokens("$..ref").Any(), Is.True,
                "Shared and cyclic motions must be represented by references rather than recursive expansion.");
            Assert.That(motion.SelectTokens("$..id").OfType<JValue>().Select(v => v.Value<int>()).Distinct().Count(),
                Is.GreaterThanOrEqualTo(3));
        }
        [Test]
        public void Probe_OmitsObjectCurvesFromCapturedMaterialMotion()
        {
            ReactiveDebugDump.Enabled = true;
            var root = CreateRoot("object-curve-probe");
            var renderer = CreateChild(root, "renderer").AddComponent<MeshRenderer>();
            var material = TrackKnownAsset(new Material(Shader.Find("Sprites/Default"))
            {
                name = "PRIVACY_OBJECT_CURVE_MATERIAL_2C3D"
            });
            var hiddenClip = TrackKnownAsset(new AnimationClip { name = "PRIVACY_OBJECT_CURVE_CLIP_6A7B" });
            var group = new EffectGroup(new SimplePropProcessor(), new MaterialSlotTarget(renderer, 0),
                new List<ReactionNode> { new(new Constant(true), new SetMaterial(renderer, 0, material)) }, 0);
            var motion = VirtualClip.Create("object-curve-motion");
            motion.SetObjectCurve(EditorCurveBinding.PPtrCurve("", typeof(Renderer), "m_Materials.Array.data[0]"),
                new[] { new ObjectReferenceKeyframe { time = 0.25f, value = hiddenClip } });
            motion.SetFloatCurve(EditorCurveBinding.FloatCurve("", typeof(GameObject), "m_IsActive"),
                AnimationCurve.Constant(0, 1, 1));
            var before = DumpFiles();

            using (var probe = ReactiveDebugProbe.Create())
            {
                probe.Offer("Effect group blend tree", group, motion);
            }

            var dump = ReadNewDump(before);
            var captured = dump["effectGroups"]!.Values<JObject>().Single();
            Assert.That(captured["motion"]!.Type, Is.Not.EqualTo(JTokenType.Null));
            Assert.That(captured["motion"]!.SelectTokens("$..floatCurves[*]").Any(), Is.True);
            Assert.That(dump.SelectTokens("$..objectCurves"), Is.Empty);
            Assert.That(dump.ToString(Formatting.None), Does.Not.Contain(material.name).And.Not.Contain(hiddenClip.name));
        }


        [Test]
        public void Reconstruction_NonCutterRoundTripPreservesLaterGraphsAndGeneratedCurves()
        {
            var source = CreateReplayFixture(out _, includeCutters: false, includeExternalAnimations: false,
                includeSyncRemap: false);
            ReactiveDebugDump.Enabled = true;
            var beforeSource = DumpFiles();
            AvatarProcessor.ProcessAvatar(source);
            var original = ReadNewDump(beforeSource);
            Object.DestroyImmediate(source);
            DestroyKnownTestAssets();

            var jsonPath = Path.Combine("Library", "reactive-debug-noncutter-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(jsonPath, original.ToString(Formatting.Indented));
            _testFiles.Add(jsonPath);
            var reconstructed = ReactiveDebugReconstruction.Reconstruct(jsonPath, "Assets/ZZZ_Temp/noncutter-replay.prefab");
            var prefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(reconstructed));
            Object.DestroyImmediate(reconstructed);
            var replay = TrackObject((GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)));
            PrefabUtility.UnpackPrefabInstance(replay, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var beforeReplay = DumpFiles();
            AvatarProcessor.ProcessAvatar(replay);
            var replayDump = ReadNewDump(beforeReplay);

            Assert.That(CanonicalStages(replayDump), Is.EqualTo(CanonicalStages(original)));
            Assert.That(CanonicalGeneratedFloatCurves(replayDump), Is.EqualTo(CanonicalGeneratedFloatCurves(original)));
        }

        [TestCase("{\"formatVersion\":1}")]
        [TestCase("{\"formatVersion\":2,\"input\":{\"objects\":[{\"id\":1,\"parentId\":1}]}}")]
        public void Reconstruction_RejectsMalformedInputWithoutCreatingPrefab(string json)
        {
            var jsonPath = Path.Combine("Library", "reactive-debug-invalid-" + Guid.NewGuid().ToString("N") + ".json");
            var prefabPath = "Assets/ZZZ_Temp/invalid-reactive-debug.prefab";
            File.WriteAllText(jsonPath, json);
            _testFiles.Add(jsonPath);

            Assert.Catch<Exception>(() => ReactiveDebugReconstruction.Reconstruct(jsonPath, prefabPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Null);
        }

        [Test]
        public void Reconstruction_RejectsUnknownDiscriminatorWithoutCreatingPrefab()
        {
            ReactiveDebugDump.Enabled = true;
            var before = DumpFiles();
            AvatarProcessor.ProcessAvatar(CreateToggleAvatar(out _));
            var invalid = ReadNewDump(before);
            invalid.SelectToken("input.components[0].kind")!.Replace("UnknownReactiveComponent");

            var jsonPath = Path.Combine("Library", "reactive-debug-unknown-" + Guid.NewGuid().ToString("N") + ".json");
            var prefabPath = "Assets/ZZZ_Temp/unknown-reactive-debug.prefab";
            File.WriteAllText(jsonPath, invalid.ToString(Formatting.Indented));
            _testFiles.Add(jsonPath);

            Assert.Catch<Exception>(() => ReactiveDebugReconstruction.Reconstruct(jsonPath, prefabPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Null);
        }

        [Test]
        public void Reconstruction_RejectsDanglingReferencesAndHierarchyCyclesFromFullDump()
        {
            ReactiveDebugDump.Enabled = true;
            var before = DumpFiles();
            AvatarProcessor.ProcessAvatar(CreateToggleAvatar(out _));
            var valid = ReadNewDump(before);

            var dangling = (JObject)valid.DeepClone();
            dangling.SelectToken("input.components[0].objectId")!.Replace(999999);
            AssertInvalidReplay(dangling, "dangling");

            var cyclic = (JObject)valid.DeepClone();
            var rootObject = cyclic["input"]!["objects"]!.Values<JObject>().First();
            rootObject["parentId"] = rootObject["id"]!.Value<int>();
            AssertInvalidReplay(cyclic, "cycle");
        }

        [Test]
        public void Reconstruction_RejectsMalformedTypedSourceFieldsWithoutCreatingObjectsOrAssets()
        {
            ReactiveDebugDump.Enabled = true;
            var source = CreateReplayFixture(out _, includeCutters: false);
            var before = DumpFiles();
            AvatarProcessor.ProcessAvatar(source);
            var valid = ReadNewDump(before);

            var invalidFloat = (JObject)valid.DeepClone();
            invalidFloat.SelectTokens("input.components[*]")
                .OfType<JObject>().First(component => component.Value<string>("kind") == nameof(ModularAvatarShapeChanger))
                ["data"]!["m_threshold"] = "not-a-float";
            AssertInvalidReplay(invalidFloat, "invalid-float");
        }

        [Test]
        public void Reconstruction_RejectsRemovedAndUnknownSourceFieldsWithoutCreatingObjectsOrAssets()
        {
            ReactiveDebugDump.Enabled = true;
            var source = CreateReplayFixture(out _, includeCutters: true);
            var before = DumpFiles();
            AvatarProcessor.ProcessAvatar(source);
            var valid = ReadNewDump(before);

            AssertMalformedMutation(valid, "missing-active-path", dump =>
                ((JObject)dump["input"]!["activeStateAnimations"]![0]!).Property("path")!.Remove());
            AssertMalformedMutation(valid, "dangling-active-object", dump =>
                ((JObject)dump["input"]!["activeStateAnimations"]![0]!)["objectId"] = 999999);
            AssertMalformedMutation(valid, "removed-source-clip", dump =>
                ((JObject)dump["input"]!["activeStateAnimations"]![0]!)["sourceClipId"] = 42);
            AssertMalformedMutation(valid, "removed-source-curve", dump =>
                ((JObject)dump["input"]!["activeStateAnimations"]![0]!)["keys"] = new JArray());
            AssertMalformedMutation(valid, "unknown-component-field", dump =>
                ((JObject)SourceComponent(dump, nameof(ModularAvatarObjectToggle))["data"]!)
                ["m_futurePrivacySentinel"] = "PRIVACY_UNKNOWN_FIELD_1E4D20");
            AssertMalformedMutation(valid, "removed-sync-remap", dump =>
                ((JObject)SourceComponent(dump, nameof(ModularAvatarBlendshapeSync))["data"]!)
                ["RemapCurve"] = new JArray());
            AssertMalformedMutation(valid, "removed-quick-swap", dump =>
                ((JObject)SourceComponent(dump, nameof(ModularAvatarMaterialSwap))["data"]!)
                ["m_quickSwapMode"] = nameof(QuickSwapMode.SiblingDirectory));
            AssertMalformedMutation(valid, "bad-renderer-mesh", dump =>
                dump.SelectTokens("input.components[*]").OfType<JObject>()
                    .First(component => component.Value<string>("kind") == nameof(SkinnedMeshRenderer))["renderer"]!
                    ["sharedMesh"]!["id"] = 999999);
            AssertMalformedMutation(valid, "missing-component-order", dump =>
                ((JObject)dump["input"]!["components"]![0]!).Property("componentIndex")!.Remove());
        }

        private static JObject SourceComponent(JObject dump, string kind) =>
            dump["input"]!["components"]!.Values<JObject>()
                .First(component => component.Value<bool?>("source") == true && component.Value<string>("kind") == kind);


        private JObject CaptureInitialParameterMatrix(out GameObject root)
        {
            root = CreateRoot("initial-parameter-matrix");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackKnownAsset(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "both", valueType = VRCExpressionParameters.ValueType.Bool, defaultValue = 1,
                    saved = true, networkSynced = false
                },
                new VRCExpressionParameters.Parameter
                {
                    name = "expression-only", valueType = VRCExpressionParameters.ValueType.Int, defaultValue = -999,
                    saved = false, networkSynced = true
                },
                new VRCExpressionParameters.Parameter
                {
                    name = "inactive-expression", valueType = VRCExpressionParameters.ValueType.Float, defaultValue = 3.25f,
                    saved = false, networkSynced = false
                },
                new VRCExpressionParameters.Parameter
                {
                    name = "unreferenced-expression", valueType = VRCExpressionParameters.ValueType.Float, defaultValue = 123
                }
            };

            AddMatrixMenuToggle(root, "both-first", "both");
            AddMatrixMenuToggle(root, "both-duplicate", "both");
            AddMatrixMenuToggle(root, "expression-only", "expression-only");
            AddMatrixMenuToggle(root, "fx-only", "fx-only");
            AddMatrixMenuToggle(root, "neither", "neither");
            var inactive = AddMatrixMenuToggle(root, "inactive-expression", "inactive-expression");
            inactive.SetActive(false);
            var disabled = AddMatrixMenuToggle(root, "disabled-fx", "disabled-fx");
            disabled.GetComponent<ModularAvatarMenuItem>().enabled = false;
            AddMatrixStandaloneMenu(root, "standalone-neither", "standalone-neither");

            ReactiveDebugDump.Enabled = true;
            var before = DumpFiles();
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var fxController = VirtualAnimatorController.Create(animatorServices.ControllerContext.CloneContext);
            fxController.Parameters = fxController.Parameters
                .Add("both", new AnimatorControllerParameter
                {
                    name = "both", type = AnimatorControllerParameterType.Float, defaultFloat = 42.5f
                })
                .Add("fx-only", new AnimatorControllerParameter
                {
                    name = "fx-only", type = AnimatorControllerParameterType.Bool, defaultBool = true
                })
                .Add("disabled-fx", new AnimatorControllerParameter
                {
                    name = "disabled-fx", type = AnimatorControllerParameterType.Int, defaultInt = 7
                })
                .Add("unreferenced-fx", new AnimatorControllerParameter
                {
                    name = "unreferenced-fx", type = AnimatorControllerParameterType.Float, defaultFloat = 456
                });
            animatorServices.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX] = fxController;
            context.ActivateExtensionContextRecursive<InjectROBaseLayer>();
            context.ActivateExtensionContextRecursive<ReadablePropertyExtension>();
            try
            {
                new ReactiveObjectPassV2(context).Execute();
            }
            finally
            {
                context.DeactivateAllExtensionContexts();
            }

            return ReadNewDump(before);
        }

        private GameObject AddMatrixMenuToggle(GameObject root, string objectName, string parameter)
        {
            var owner = CreateChild(root, objectName);
            var item = owner.AddComponent<ModularAvatarMenuItem>();
            item.automaticValue = false;
            item.isDefault = false;
            item.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
                value = 1
            };
            var target = CreateChild(root, objectName + "-target");
            owner.AddComponent<ModularAvatarObjectToggle>().Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target), Active = true
            });
            return owner;
        }

        private void AddMatrixStandaloneMenu(GameObject root, string objectName, string parameter)
        {
            var owner = CreateChild(root, objectName);
            var item = owner.AddComponent<ModularAvatarMenuItem>();
            item.automaticValue = false;
            item.isDefault = false;
            item.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
                value = 1
            };
        }

        private static void AssertInitialState(IEnumerable<JObject> states, string name, string? expressionType,
            float? expressionDefault, string? fxType, float? fxDefault)
        {
            var state = states.Single(candidate => candidate.Value<string>("name") == name);
            AssertSourceState((JObject)state["expressionParameters"]!, expressionType, expressionDefault);
            AssertSourceState((JObject)state["fxLayer"]!, fxType, fxDefault);
        }

        private static void AssertSourceState(JObject source, string? type, float? defaultValue)
        {
            Assert.That(source.Value<bool>("present"), Is.EqualTo(type != null));
            if (type == null)
            {
                Assert.That(source.Properties().Select(property => property.Name), Is.EquivalentTo(new[] { "present" }));
                return;
            }

            Assert.That(source.Value<string>("type"), Is.EqualTo(type));
            Assert.That(source.Value<float>("defaultValue"), Is.EqualTo(defaultValue!.Value));
        }

        private GameObject CreateToggleAvatar(out GameObject target)
        {
            var root = CreateRoot("debug-avatar");
            target = CreateChild(root, "target");
            var menu = CreateChild(root, "menu");
            var item = menu.AddComponent<ModularAvatarMenuItem>();
            item.automaticValue = false;
            item.isDefault = true;
            item.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }, value = 1
            };
            menu.AddComponent<ModularAvatarObjectToggle>().Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target), Active = true
            });
            return root;
        }

        private GameObject CreateReplayFixture(out SkinnedMeshRenderer renderer, bool includeCutters = true,
            bool includeExternalAnimations = true, bool includeSyncRemap = true)
        {
            var root = CreateToggleAvatar(out var toggleTarget);
            root.name = "replay-source";
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackKnownAsset(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "outfit", valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 1, saved = true, networkSynced = true
                }
            };
            var meshObject = CreateChild(root, "duplicate");
            var sameNameSibling = CreateChild(root, "duplicate");
            renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateShapeFilterTestMesh();
            renderer.updateWhenOffscreen = true;
            _knownTestAssets.Add(renderer.sharedMesh);
            renderer.sharedMesh.name = "Replay Mesh";
            renderer.SetBlendShapeWeight(0, 25);
            var materialA = TrackKnownAsset(new Material(Shader.Find("Sprites/Default")) { name = "Shared A" });
            var materialB = TrackKnownAsset(new Material(Shader.Find("Sprites/Default")) { name = "Shared B" });
            renderer.sharedMaterials = new[] { materialA, null };
            foreach (var (name, active) in new[] { ("Z-priority-owner", true), ("Y-priority-owner", false), ("A-priority-owner", true) })
            {
                var owner = CreateChild(root, name);
                owner.AddComponent<ModularAvatarObjectToggle>().Objects.Add(new ToggledObject
                {
                    Object = new AvatarObjectReference(meshObject), Active = active
                });
            }

            var shape = root.AddComponent<ModularAvatarShapeChanger>();
            shape.Threshold = float.NaN;
            shape.Shapes.Add(new ChangedShape
            {
                Object = new AvatarObjectReference(meshObject), ShapeName = "Positive", ChangeType = ShapeChangeType.Set, Value = 50
            });
            shape.Shapes.Add(new ChangedShape
            {
                Object = new AvatarObjectReference(meshObject), ShapeName = "Negative", ChangeType = ShapeChangeType.Delete, Value = 0
            });
            var localSyncObject = CreateChild(root, "sync-local-not-reactively-targeted");
            var localSyncRenderer = localSyncObject.AddComponent<SkinnedMeshRenderer>();
            localSyncRenderer.sharedMesh = renderer.sharedMesh;
            var sync = localSyncObject.AddComponent<ModularAvatarBlendshapeSync>();
            sync.Bindings.Add(new BlendshapeBinding
            {
                ReferenceMesh = new AvatarObjectReference(meshObject), Blendshape = "Positive", LocalBlendshape = "",
                RemapCurveIsValid = includeSyncRemap,
                RemapCurve = includeSyncRemap
                    ? new AnimationCurve(new Keyframe(17.1234f, -42.5678f, 91.2468f, -13.579f),
                        new Keyframe(82.9876f, 63.2468f, -24.6813f, 75.319f))
                    : new AnimationCurve()
            });

            var setter = root.transform.Find("menu").gameObject.AddComponent<ModularAvatarMaterialSetter>();
            setter.Objects.Add(new MaterialSwitchObject
            {
                Object = new AvatarObjectReference(meshObject), Material = materialB, MaterialIndex = 0
            });
            var swap = root.AddComponent<ModularAvatarMaterialSwap>();
            swap.Root = new AvatarObjectReference(root);
            swap.Swaps.Add(new MatSwap { From = materialA, To = materialB });
            swap.Swaps.Add(new MatSwap { From = materialB, To = null });
            swap.QuickSwapMode = QuickSwapMode.SiblingDirectory;

            if (includeCutters)
            {
                var cutterOwner = root.transform.Find("menu").gameObject;
                var cutter = cutterOwner.AddComponent<ModularAvatarMeshCutter>();
                cutter.Object.Set(meshObject);
                cutter.MultiMode = MeshCutterMultiMode.VertexUnion;
                var secondCutter = cutterOwner.AddComponent<ModularAvatarMeshCutter>();
                secondCutter.Object.Set(meshObject);
                secondCutter.MultiMode = MeshCutterMultiMode.VertexIntersection;
                cutterOwner.AddComponent<VertexFilterByAxisComponent>().Axis = Vector3.right;
                var unequalOwner = CreateChild(cutterOwner, "unequal-cutter");
                var unequalCutter = unequalOwner.AddComponent<ModularAvatarMeshCutter>();
                unequalCutter.Object.Set(meshObject);
                unequalOwner.AddComponent<VertexFilterByAxisComponent>().Center = Vector3.one;
            }

            var moveTarget = CreateChild(root, "move-target");
            var move = root.AddComponent<ModularAvatarReactiveMove>();
            move.ToMove = new AvatarObjectReference(meshObject);
            move.WhereTo = new AvatarObjectReference(moveTarget);
            move.SetsPosition = true;
            move.SetsRotation = false;
            move.SetsScale = true;
            move.TransitionTime = 0.2f;
            toggleTarget.AddComponent<AudioSource>().enabled = false;

            var otherMenu = CreateChild(root, "other-choice");
            var otherItem = otherMenu.AddComponent<ModularAvatarMenuItem>();
            otherItem.automaticValue = false;
            otherItem.isDefault = false;
            otherItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }, value = 2
            };
            otherMenu.AddComponent<ModularAvatarObjectToggle>().Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(sameNameSibling), Active = true
            });

            if (includeExternalAnimations)
            {
                AddActiveStateAnimation(root, meshObject, "PRIVACY_REPLAY_SHARED_ACTIVE_44E7D1", 0f, 1f, 1f, 0f, moveTarget);
            }
            return root;
        }

        private AnimationClip AddActiveStateAnimation(GameObject root, GameObject target, string name,
            float firstTime, float firstValue, float secondTime, float secondValue, GameObject? secondTarget = null)
        {
            var controller = TrackKnownAsset(new AnimatorController { name = "Debug FX" });
            controller.AddLayer("Debug");
            var clip = TrackKnownAsset(new AnimationClip { name = name });
            controller.AddParameter("outfit", AnimatorControllerParameterType.Float);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(target.name, typeof(GameObject), "m_IsActive"),
                new AnimationCurve(new Keyframe(firstTime, firstValue), new Keyframe(secondTime, secondValue)));
            if (secondTarget != null)
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(secondTarget.name, typeof(GameObject), "m_IsActive"),
                    new AnimationCurve(new Keyframe(firstTime, firstValue), new Keyframe(secondTime, secondValue)));
            }
            controller.layers[0].stateMachine.defaultState = controller.layers[0].stateMachine.AddState("Debug");
            controller.layers[0].stateMachine.defaultState.motion = clip;
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            var layers = descriptor.baseAnimationLayers;
            var index = Array.FindIndex(layers, l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            layers[index].isDefault = false;
            layers[index].animatorController = controller;
            descriptor.baseAnimationLayers = layers;
            return clip;
        }

        private T TrackKnownAsset<T>(T asset) where T : Object
        {
            _knownTestAssets.Add(asset);
            return TrackObject(asset);
        }

        private void DestroyKnownTestAssets()
        {
            foreach (var asset in _knownTestAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset, true);
            }
        }

        private static string CanonicalPreBackendGraph(JObject dump)
        {
            AssertCompleteBuildStages(dump);
            var graph = dump["stages"]!.Values<JObject>()
                .First(s => s["name"]!.Value<string>() == "Before backend.PreprocessGraph")["graphs"]!.DeepClone();
            var identities = new Dictionary<string, int>(StringComparer.Ordinal);
            NormalizeIdentityLabels(graph, identities);
            return graph.ToString(Formatting.None);
        }

        private static string CanonicalStages(JObject dump)
        {
            AssertCompleteBuildStages(dump);
            var stages = (JArray)dump["stages"]!.DeepClone();
            NormalizeIdentityLabels(stages, new Dictionary<string, int>(StringComparer.Ordinal));
            return stages.ToString(Formatting.None);
        }

        private static void AssertCompleteBuildStages(JObject dump)
        {
            CollectionAssert.AreEqual(new[]
            {
                "Before backend.PreprocessGraph", "Before ILBuild.Simplify", "Before StaticProcessing.Apply",
                "Before backend.Build", "After OptimizeForBackend"
            }, dump["stages"]!.Values<JObject>().Select(stage => stage.Value<string>("name")));
        }

        private static string CanonicalGeneratedFloatCurves(JObject dump)
        {
            var groups = new JArray(dump["effectGroups"]!.Values<JObject>().Select(group => new JObject
            {
                ["targetKey"] = group["targetKey"]!.DeepClone(),
                ["floatCurves"] = new JArray(group.SelectTokens("motion..floatCurves[*]").Select(curve =>
                {
                    var copy = (JObject)curve.DeepClone();
                    copy.Remove("path");
                    return copy;
                }))
            }));
            NormalizeIdentityLabels(groups, new Dictionary<string, int>(StringComparer.Ordinal));
            return groups.ToString(Formatting.None);
        }



        private static bool HasNaNimationCurve(GameObject avatar)
        {
            var controller = FindFxController(avatar).animatorController as AnimatorController;
            if (controller == null) return false;
            return controller.layers.SelectMany(layer => layer.stateMachine.states)
                .Select(state => state.state.motion)
                .SelectMany(EnumerateClips)
                .Any(clip => AnimationUtility.GetCurveBindings(clip)
                    .Select(binding => AnimationUtility.GetEditorCurve(clip, binding))
                    .Any(curve => curve != null && curve.keys.Any(key => float.IsNaN(key.value))));
        }

        private static IEnumerable<AnimationClip> EnumerateClips(Motion? motion)
        {
            if (motion is AnimationClip clip) yield return clip;
            if (motion is BlendTree tree)
            {
                foreach (var child in tree.children)
                {
                    foreach (var childClip in EnumerateClips(child.motion)) yield return childClip;
                }
            }
        }

        private static void NormalizeIdentityLabels(JToken token, Dictionary<string, int> identities)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties().ToList())
                {
                    var isReferenceId = property.Name == "id"
                        && obj["kind"]?.Value<string>() is "object" or "component" or "asset";
                    if ((property.Name.EndsWith("Id", StringComparison.Ordinal) || isReferenceId)
                        && (property.Value.Type == JTokenType.Integer || property.Value.Type == JTokenType.String))
                    {
                        var value = (property.Name == "selectorPartitionId" ? "selector:" : "object:")
                            + property.Value.ToString(Formatting.None);
                        if (!identities.TryGetValue(value, out var normalized))
                        {
                            normalized = identities.Count + 1;
                            identities[value] = normalized;
                        }

                        property.Value = normalized;
                    }
                    else
                    {
                        NormalizeIdentityLabels(property.Value, identities);
                    }
                }
            }
            else if (token is JArray array)
            {
                foreach (var child in array) NormalizeIdentityLabels(child, identities);
            }
        }

        private void AssertMalformedMutation(JObject valid, string label, Action<JObject> mutate)
        {
            var malformed = (JObject)valid.DeepClone();
            mutate(malformed);
            AssertInvalidReplay(malformed, label);
        }

        private void AssertInvalidReplay(JObject document, string label)
        {
            var jsonPath = Path.Combine("Library", $"reactive-debug-{label}-{Guid.NewGuid():N}.json");
            var prefabPath = $"Assets/ZZZ_Temp/{label}-reactive-debug.prefab";
            var beforeObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Count(gameObject => gameObject.scene.IsValid());
            var replayFolders = Directory.Exists("Assets/ZZZ_Temp")
                ? Directory.GetDirectories("Assets/ZZZ_Temp", $"{label}-reactive-debug-ReactiveReplay*").ToHashSet()
                : new HashSet<string>();
            File.WriteAllText(jsonPath, document.ToString(Formatting.Indented));
            _testFiles.Add(jsonPath);
            Assert.Catch<Exception>(() => ReactiveDebugReconstruction.Reconstruct(jsonPath, prefabPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(gameObject => gameObject.scene.IsValid()),
                Is.EqualTo(beforeObjects));
            var foldersAfter = Directory.Exists("Assets/ZZZ_Temp")
                ? Directory.GetDirectories("Assets/ZZZ_Temp", $"{label}-reactive-debug-ReactiveReplay*").ToHashSet()
                : new HashSet<string>();
            CollectionAssert.AreEquivalent(replayFolders, foldersAfter);
        }
        private static HashSet<string> DumpFiles()
        {
            return Directory.Exists(DumpDirectory)
                ? Directory.GetFiles(DumpDirectory, "*.json").ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private JObject ReadNewDump(ISet<string> before)
        {
            var paths = DumpFiles().Except(before, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.That(paths, Has.Count.EqualTo(1), "Each enabled build must write one uniquely named dump.");
            _testFiles.Add(paths[0]);
            var text = File.ReadAllText(paths[0]);
            Assert.That(text, Does.StartWith("{").And.Contain(Environment.NewLine), "Dumps must be indented JSON.");
            Assert.That(Regex.IsMatch(text, "(?<![\\\"\\w])-?(?:NaN|Infinity)(?![\\\"\\w])"), Is.False,
                "Non-finite values must be JSON strings rather than invalid bare JSON numeric tokens.");
            return JObject.Parse(text);
        }
    }

    public sealed class PrivacySentinelComponent : MonoBehaviour
    {
        [SerializeField] private string m_sentinel = "";

        public string Sentinel
        {
            get => m_sentinel;
            set => m_sentinel = value;
        }
    }
}

#endif
