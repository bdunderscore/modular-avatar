#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.modular_avatar.ui;
using nadena.dev.ndmf.runtime.components;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>Imports the portable source section of a reactive-component diagnostic dump.</summary>
    internal static class ReactiveDebugReconstruction
    {
        private const int FormatVersion = 2;

#if MODULAR_AVATAR_DEBUG
        [MenuItem(UnityMenuItems.TopMenu_ReconstructReactiveComponentAvatar,
            priority = UnityMenuItems.TopMenu_ReconstructReactiveComponentAvatarOrder)]
#endif
        private static void ReconstructFromMenu()
        {
            var source = EditorUtility.OpenFilePanel("Reconstruct Reactive Component Avatar", "", "json");
            if (string.IsNullOrEmpty(source)) return;

            var destination = EditorUtility.SaveFilePanelInProject("Save reconstructed avatar", "ReactiveReplay", "prefab",
                "Choose where to save the reconstructed source prefab");
            if (string.IsNullOrEmpty(destination)) return;

            try
            {
                Reconstruct(source, destination);
            }
            catch (Exception e)
            {
                Debug.LogError($"Unable to reconstruct reactive component dump: {e.Message}");
            }
        }

        /// <summary>
        /// Reconstructs a persisted, inspectable source prefab. This deliberately rebuilds normal inputs rather than
        /// injecting the captured reaction graph.
        /// </summary>
        internal static GameObject Reconstruct(string jsonPath, string prefabAssetPath)
        {
            if (string.IsNullOrEmpty(jsonPath)) throw new ArgumentException("A dump JSON path is required", nameof(jsonPath));
            if (string.IsNullOrEmpty(prefabAssetPath)) throw new ArgumentException("A prefab destination is required", nameof(prefabAssetPath));
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("Dump JSON was not found", jsonPath);
            if (!prefabAssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The destination must be a .prefab asset", nameof(prefabAssetPath));

            var document = JObject.Parse(File.ReadAllText(jsonPath));
            var input = Validate(document);
            var outputDirectory = CreateOutputDirectory(prefabAssetPath);
            var createdAssets = new List<string>();
            GameObject? root = null;
            Importer? importer = null;

            try
            {
                importer = new Importer(input, outputDirectory, createdAssets);
                root = importer.CreateHierarchy();
                importer.CreateAssets();
                importer.CreateComponents();
                importer.ApplyComponents();
                importer.CreateAnimatorInputs(root);
                importer.ApplyActiveStates();

                var destination = $"{outputDirectory}/{Path.GetFileNameWithoutExtension(prefabAssetPath)}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, destination);
                createdAssets.Add(destination);
                var instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(destination)) as GameObject;
                if (instance == null) throw new InvalidOperationException("Unity did not instantiate the reconstructed prefab");
                Undo.RegisterCreatedObjectUndo(instance, "Reconstruct Reactive Component Avatar");
                Selection.activeGameObject = instance;
                UnityEngine.Object.DestroyImmediate(root);
                return instance;
            }
            catch
            {
                importer?.DestroyHierarchy();
                for (var i = createdAssets.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(createdAssets[i]);
                AssetDatabase.DeleteAsset(outputDirectory);
                throw;
            }
        }

        private static JObject Validate(JObject document)
        {
            RequireOnlyFields(document, "dump", "formatVersion", "input", "stages", "effectGroups", "errors");
            foreach (var required in new[] { "formatVersion", "input", "stages", "effectGroups" })
                if (document.Property(required) == null) throw new InvalidDataException($"dump is missing required field '{required}'");
            if (document.Value<int?>("formatVersion") != FormatVersion)
                throw new InvalidDataException($"Unsupported reactive debug dump format version '{document["formatVersion"]}'");
            if (document.Property("errors") != null) ValidateErrors(RequiredArray(document, "errors"));
            var input = document["input"] as JObject ?? throw new InvalidDataException("Dump is missing input");
            RequireOnlyFields(input, "input", "platform", "avatarName", "objects", "components", "assets",
                "animatorParameters", "expressionParameters", "activeStateAnimations", "selectorPartitions",
                "initialParameterStates");
            _ = RequiredString(input, "avatarName");
            foreach (var table in new[] { "components", "assets", "animatorParameters", "expressionParameters", "activeStateAnimations", "selectorPartitions" })
                _ = RequiredArray(input, table);
            if (input.Property("initialParameterStates") != null)
                _ = RequiredArray(input, "initialParameterStates");
            var allObjects = RequiredArray(input, "objects");
            var objects = new JArray(SourceRecords(allObjects));
            var ids = new HashSet<int>();
            foreach (var token in allObjects.Concat(RequiredArray(input, "components")).Concat(RequiredArray(input, "assets")))
            {
                var record = token as JObject ?? throw new InvalidDataException("Identity table record must be an object");
                var id = RequiredInt(record, "id");
                if (id <= 0 || !ids.Add(id)) throw new InvalidDataException($"Invalid or duplicate dump ID {id}");
                if (record["source"]?.Type != JTokenType.Boolean)
                    throw new InvalidDataException($"Identity record {id} must specify source provenance");
            }
            var parents = new Dictionary<int, int?>();
            foreach (var obj in objects)
            {
                var record = obj as JObject ?? throw new InvalidDataException("Object record is not an object");
                RequireOnlyFields(record, "object record", "id", "source", "type", "name", "parentId", "siblingIndex",
                    "activeSelf", "path", "localPosition", "localRotation", "localScale");
                var id = RequiredInt(record, "id");
                if (RequiredString(record, "type") != "GameObject") throw new InvalidDataException($"Unsupported object type on {id}");
                _ = RequiredString(record, "name");
                _ = RequiredInt(record, "siblingIndex");
                RequireBoolean(record, "activeSelf");
                _ = RequiredString(record, "path");
                if (record.Property("parentId") == null) throw new InvalidDataException($"Object {id} is missing parentId");
                var transformFieldCount = new[] { "localPosition", "localRotation", "localScale" }
                    .Count(field => record.Property(field) != null);
                if (transformFieldCount is not (0 or 3))
                    throw new InvalidDataException($"Object {id} must provide every transform field or none");
                ValidateOptionalVector(record["localPosition"], 3, "localPosition");
                ValidateOptionalVector(record["localRotation"], 4, "localRotation");
                ValidateOptionalVector(record["localScale"], 3, "localScale");
                parents.Add(id, record.Value<int?>("parentId"));
            }
            foreach (var (id, parent) in parents)
            {
                if (parent.HasValue && !parents.ContainsKey(parent.Value))
                    throw new InvalidDataException($"Object {id} references unknown parent {parent}");
                var seen = new HashSet<int> { id };
                var cursor = parent;
                while (cursor.HasValue)
                {
                    if (!seen.Add(cursor.Value)) throw new InvalidDataException($"Object hierarchy contains a cycle at {id}");
                    cursor = parents[cursor.Value];
                }
            }
            if (parents.Values.Count(parent => !parent.HasValue) != 1)
                throw new InvalidDataException("Input must contain exactly one root object");
            var sourceObjectIds = new HashSet<int>(objects.OfType<JObject>().Select(record => RequiredInt(record, "id")));
            var sourceObjectsById = objects.OfType<JObject>().ToDictionary(record => RequiredInt(record, "id"));
            var sourceAssetKinds = SourceRecords(RequiredArray(input, "assets")).OfType<JObject>()
                .ToDictionary(record => RequiredInt(record, "id"), record => RequiredString(record, "kind"));


            var selectorIds = new HashSet<int>();
            foreach (var selector in RequiredArray(input, "selectorPartitions"))
            {
                var record = selector as JObject ?? throw new InvalidDataException("Selector partition is not an object");
                RequireExactFields(record, "selector partition", "id", "kind");
                if (!selectorIds.Add(RequiredInt(record, "id")) || RequiredString(record, "kind") != "opaque")
                    throw new InvalidDataException("Invalid selector partition");
            }
            var referencedIds = ids;
            var menuParameters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in SourceRecords(RequiredArray(input, "components")))
            {
                var record = component as JObject ?? throw new InvalidDataException("Component record is not an object");
                ValidateComponentRecordFields(record);
                _ = RequiredInt(record, "id");
                if (!sourceObjectIds.Contains(RequiredInt(record, "objectId"))) throw new InvalidDataException("Component references a non-source object");
                _ = RequiredInt(record, "componentIndex");
                ValidateComponentKind(record);
                RequireBoolean(record, "enabled");
                ValidateComponentData(record);
                ValidateSourceReferences(record, sourceObjectsById, sourceAssetKinds);
                var kind = RequiredString(record, "kind");
                if (kind == nameof(ModularAvatarMenuItem))
                {
                    var menu = record["menu"] as JObject ?? throw new InvalidDataException("Menu item is missing portable control data");
                    RequireExactFields(menu, "menu item", "type", "parameter", "value", "isDefault", "isSaved", "isSynced",
                        "automaticValue");
                    if (!Enum.TryParse<PortableControlType>(RequiredString(menu, "type"), false, out var controlType)
                        || !Enum.IsDefined(typeof(PortableControlType), controlType))
                        throw new InvalidDataException($"Unsupported portable menu control type '{RequiredString(menu, "type")}'");
                    var menuParameter = RequiredString(menu, "parameter");
                    if (!string.IsNullOrEmpty(menuParameter)) menuParameters.Add(menuParameter);
                    _ = ReactiveDebugJson.ReadFiniteFloat(menu["value"] ?? throw new InvalidDataException("Menu value is missing"));
                    RequireBool(menu, "isDefault"); RequireBool(menu, "isSaved"); RequireBool(menu, "isSynced"); RequireBool(menu, "automaticValue");
                }
                if (kind == nameof(AudioSource))
                {
                    var audio = record["audio"] as JObject ?? throw new InvalidDataException("Audio source is missing audio data");
                    RequireExactFields(audio, "audio source", "enabled");
                    RequireBoolean(audio, "enabled");
                }
                if (kind is nameof(SkinnedMeshRenderer) or "Renderer")
                    ValidateRendererRecord(record, sourceAssetKinds);
                if (kind == nameof(ModularAvatarMeshCutter))
                {
                    if (record.Value<string>("selectorCaptureError") is { Length: > 0 } error)
                        throw new InvalidDataException($"Mesh cutter selector capture failed: {error}");
                    if (record["selectorPartitionId"] == null)
                        throw new InvalidDataException("Mesh cutter selector partition is missing");
                    if (record.Value<int?>("selectorPartitionId") is { } partition && !selectorIds.Contains(partition))
                        throw new InvalidDataException($"Mesh cutter references unknown selector partition {partition}");
                }
            }
            foreach (var asset in SourceRecords(RequiredArray(input, "assets")))
            {
                var record = asset as JObject ?? throw new InvalidDataException("Asset record is not an object");
                RequireExactFields(record, "asset record", "id", "source", "kind");
                if (RequiredString(record, "kind") is not ("Material" or "Mesh"))
                    throw new InvalidDataException($"Unsupported asset kind '{RequiredString(record, "kind")}'");
            }
            // References are checked after all source tables are known, including forward references.
            // The closed source schema validates every allowed source field and reference encoding.
            var activeObjectIds = new HashSet<int>();
            foreach (var record in RequiredArray(input, "activeStateAnimations"))
            {
                if (record is not JObject activeState) throw new InvalidDataException("Active-state animation record is not an object");
                RequireExactFields(activeState, "active-state animation", "objectId", "path");
                var objectId = RequiredInt(activeState, "objectId");
                if (!sourceObjectIds.Contains(objectId)) throw new InvalidDataException("Active-state animation references a non-source object");
                if (!activeObjectIds.Add(objectId)) throw new InvalidDataException("Active-state animations must be deduplicated by object ID");
                _ = RequiredString(activeState, "path");
            }
            var contradictoryExpressionParameters = new HashSet<string>(StringComparer.Ordinal);
            var expressionParameters = ValidateExpressionParameters(RequiredArray(input, "expressionParameters"), contradictoryExpressionParameters);
            var contradictoryAnimatorParameters = new HashSet<string>(StringComparer.Ordinal);
            var animatorParameters = ValidateAnimatorParameters(RequiredArray(input, "animatorParameters"), contradictoryAnimatorParameters);
            if (input.Property("initialParameterStates") != null)
                ValidateInitialParameterStates(RequiredArray(input, "initialParameterStates"), menuParameters,
                    expressionParameters, contradictoryExpressionParameters, animatorParameters, contradictoryAnimatorParameters);
            ValidateGraphKinds(document, referencedIds);
            var platform = RequiredString(input, "platform");
            if (platform is not ("nadena.dev.ndmf.vrchat.avatar3" or "nadena.dev.ndmf.generic"))
                throw new InvalidDataException($"Unsupported platform '{platform}'");
#if !MA_VRCSDK3_AVATARS
            if (platform == "nadena.dev.ndmf.vrchat.avatar3")
                throw new InvalidDataException("This dump requires VRChat SDK avatar support, which is not installed");
#endif
            return input;
        }

        private static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
        {
            "array", "object", "AvatarObjectReference", "enum", "object", "component", "asset", "opaque",
            "Constant", "ParameterExpression", "InternalParameterCondition", "ObjectActiveState", "AndNode", "OrNode", "NotNode",
            "DriveActiveState", "DriveParameter", "DriveInternalParameter", "FloatPropAction", "ObjectPropAction",
            "SetShapeKey", "SetMaterial", "HideMeshSection", "ReactiveMoveAction", "DeltaTimeControlAction",
            "AlreadyApplied", "TargetKeyOverride", "NullAction", "ObjectActiveTarget", "ParameterTarget",
            "InternalParameterTarget", "PropertyTarget", "ShapeKeyTarget", "MaterialSlotTarget", "ReactiveMoveTarget",
            "MeshSectionTarget", "DeltaTimeTarget", "VirtualBlendTree", "VirtualClip"
        };


        private static void ValidatePropertyData(JToken? token, ISet<int> knownIds, bool checkReferences)
        {
            if (token == null || token.Type == JTokenType.Null) return;
            if (token is JArray array)
            {
                foreach (var item in array) ValidatePropertyData(item, knownIds, checkReferences);
                return;
            }
            if (token is not JObject value) return;
            var kind = value.Value<string>("kind");
            if (kind != null && !AllowedKinds.Contains(kind))
                throw new InvalidDataException($"Unsupported discriminator '{kind}'");
            if (kind is "component" or "asset" || kind == "object" && value["id"] != null)
            {
                var id = RequiredInt(value, "id");
                if (checkReferences && !knownIds.Contains(id)) throw new InvalidDataException($"Reference to unknown dump ID {id}");
                return;
            }
            if (kind == "object" && value["fields"] is not JObject)
                throw new InvalidDataException("Serialized object value is missing fields");
            if (kind == "unsupported") throw new InvalidDataException("Unsupported serialized property");
            foreach (var property in value.Properties())
            {
                if (property.Name == "objectCurves")
                    throw new InvalidDataException("Object curves are not permitted in reactive debug dumps");
                ValidatePropertyData(property.Value, knownIds, checkReferences);
            }
        }

        private static void ValidateGraphKinds(JObject document, ISet<int> knownIds)
        {
            foreach (var section in new[] { "stages", "effectGroups" })
            {
                if (document[section] is not JArray records) throw new InvalidDataException($"Missing required array '{section}'");
                ValidatePropertyData(records, knownIds, true);
            }
        }

        private static void ValidateErrors(JArray errors)
        {
            foreach (var token in errors)
            {
                var error = token as JObject ?? throw new InvalidDataException("Dump error record must be an object");
                RequireExactFields(error, "dump error", "operation", "code", "exceptionType", "message");
                _ = RequiredNonEmptyString(error, "operation", "Dump error");
                if (RequiredString(error, "code") != "CaptureFailed")
                    throw new InvalidDataException($"Unsupported dump error code '{error["code"]}'");
                _ = RequiredNonEmptyString(error, "exceptionType", "Dump error");
                if (RequiredString(error, "message") != "Capture failed. See the Unity Console for details.")
                    throw new InvalidDataException("Unexpected dump error message");
            }
        }

        private static string CreateOutputDirectory(string prefabAssetPath)
        {
            var normalized = prefabAssetPath.Replace('\\', '/');
            var parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? "Assets";
            var stem = Path.GetFileNameWithoutExtension(normalized);
            var candidate = AssetDatabase.GenerateUniqueAssetPath($"{parent}/{stem}-ReactiveReplay");
            var directoryName = Path.GetFileName(candidate);
            if (string.IsNullOrEmpty(directoryName) || !AssetDatabase.IsValidFolder(parent))
                throw new ArgumentException($"The prefab destination must be below an existing project folder: {prefabAssetPath}",
                    nameof(prefabAssetPath));
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, directoryName)))
                throw new InvalidOperationException($"Unable to create replay directory {candidate}");
            return candidate;
        }

        private sealed class Importer
        {
            private readonly JObject _input;
            private readonly string _directory;
            private readonly List<string> _createdAssets;
            private readonly Dictionary<int, GameObject> _objects = new();
            private readonly Dictionary<int, UnityEngine.Object?> _assets = new();
            private readonly Dictionary<int, Component> _componentIds = new();
            private readonly List<(Component component, JObject record)> _components = new();

            internal Importer(JObject input, string directory, List<string> createdAssets)
            {
                _input = input;
                _directory = directory;
                _createdAssets = createdAssets;
            }

            internal GameObject CreateHierarchy()
            {
                foreach (var token in SourceRecords(RequiredArray(_input, "objects")))
                {
                    var record = (JObject)token!;
                    _objects.Add(RequiredInt(record, "id"), new GameObject(RequiredString(record, "name")));
                }
                foreach (var token in SourceRecords(RequiredArray(_input, "objects")))
                {
                    var record = (JObject)token!;
                    var gameObject = _objects[RequiredInt(record, "id")];
                    var parentId = record.Value<int?>("parentId");
                    if (parentId.HasValue) gameObject.transform.SetParent(_objects[parentId.Value].transform, false);
                    ApplyTransform(gameObject.transform, record);
                }
                foreach (var token in SourceRecords(RequiredArray(_input, "objects")))
                {
                    var record = (JObject)token!;
                    _objects[RequiredInt(record, "id")].transform.SetSiblingIndex(RequiredInt(record, "siblingIndex"));
                }
                var roots = _objects.Values.Where(o => o.transform.parent == null).ToArray();
                if (roots.Length != 1) throw new InvalidDataException("Input must contain exactly one root object");
                return roots[0];
            }

            internal void DestroyHierarchy()
            {
                foreach (var root in _objects.Values.Where(o => o.transform.parent == null).ToArray())
                    UnityEngine.Object.DestroyImmediate(root);
            }

            internal void CreateAssets()
            {
                foreach (var token in SourceRecords(RequiredArray(_input, "assets")))
                {
                    var record = (JObject)token!;
                    var id = RequiredInt(record, "id");
                    var kind = RequiredString(record, "kind");
                    UnityEngine.Object? asset = kind switch
                    {
                        "Material" => CreateMaterial(id),
                        "Mesh" => CreateMesh(id, record),
                        _ => throw new InvalidDataException($"Unsupported asset kind '{kind}'")
                    };
                    _assets.Add(id, asset);
                }
            }

            private Material CreateMaterial(int id)
            {
                var shader = Shader.Find("Sprites/Default") ?? throw new InvalidOperationException("Sprites/Default shader is unavailable");
                var material = new Material(shader) { name = $"ReactiveReplayMaterial-{id}" };
                CreateAsset(material, $"Material-{id}.mat");
                return material;
            }

            private Mesh CreateMesh(int id, JObject record)
            {
                var mesh = new Mesh { name = $"ReactiveReplayMesh-{id}" };
                mesh.vertices = new[] { new Vector3(1, 0, 0), new Vector3(2, 0, 0), new Vector3(1, 1, 0) };
                var subMeshCount = Math.Max(1, RequiredMaterialSlotCount(id));
                mesh.subMeshCount = subMeshCount;
                for (var index = 0; index < subMeshCount; index++) mesh.SetTriangles(new[] { 0, 1, 2 }, index);
                foreach (var shape in RequiredBlendShapeNames(id).Distinct(StringComparer.Ordinal))
                    mesh.AddBlendShapeFrame(shape, 100, new[] { new Vector3(SyntheticShapeDelta(), 0, 0), Vector3.zero, Vector3.zero },
                        new Vector3[3], new Vector3[3]);
                CreateAsset(mesh, $"Mesh-{id}.asset");
                return mesh;
            }

            private int RequiredMaterialSlotCount(int meshId) => SourceRecords(RequiredArray(_input, "components"))
                .OfType<JObject>()
                .Where(component => component["renderer"]?["sharedMesh"]?.Value<int?>("id") == meshId)
                .Select(component => (component["renderer"]?["sharedMaterials"] as JArray)?.Count ?? 0)
                .DefaultIfEmpty(0).Max();
            private float SyntheticShapeDelta()
            {
                var threshold = 0f;
                foreach (var component in SourceRecords(RequiredArray(_input, "components")).OfType<JObject>())
                {
                    if (component.Value<string>("kind") != nameof(ModularAvatarShapeChanger)) continue;
                    var encoded = (component["data"] as JObject)?["m_threshold"];
                    if (encoded == null) continue;
                    var value = ReactiveDebugJson.ReadFiniteFloat(encoded);
                    if (!float.IsNaN(value) && !float.IsInfinity(value)) threshold = Math.Max(threshold, Math.Abs(value));
                }
                return threshold < float.MaxValue / 2 ? Math.Max(1, threshold * 2) : 0;
            }


            private IEnumerable<string> RequiredBlendShapeNames(int meshId)
            {
                foreach (var component in SourceRecords(RequiredArray(_input, "components")))
                {
                    var renderer = component["renderer"] as JObject;
                    if (renderer?["sharedMesh"] is not JObject reference || reference.Value<int?>("id") != meshId) continue;
                    foreach (var shape in OptionalArray(renderer, "blendShapes").OfType<JObject>())
                        yield return RequiredString(shape, "name");
                }
            }

            internal void CreateComponents()
            {
                foreach (var token in SourceRecords(RequiredArray(_input, "components"))
                             .OfType<JObject>().OrderBy(record => RequiredInt(record, "objectId"))
                             .ThenBy(record => RequiredInt(record, "componentIndex")))
                {
                    var record = (JObject)token!;
                    var owner = _objects[RequiredInt(record, "objectId")];
                    var component = owner.AddComponent(ComponentType(record));
                    _componentIds.Add(RequiredInt(record, "id"), component);
                    _components.Add((component, record));
                }
            }

            internal void ApplyComponents()
            {
                foreach (var (component, record) in _components)
                {
                    ApplyComponent(component, record);
                    if (record.Value<bool?>("enabled") is { } enabled)
                    {
                        if (component is Behaviour behaviour) behaviour.enabled = enabled;
                        if (component is Renderer renderer) renderer.enabled = enabled;
                    }
                }
                CreateCutterProxies();
            }

            private void ApplyComponent(Component component, JObject record)
            {
                if (record["data"] is JObject data)
                    ReactiveDebugSourceSchema.ApplyData(component, data, ResolveSourceReference);
                if (component is Renderer renderer && record["renderer"] is JObject rendererData)
                {
                    if (renderer is SkinnedMeshRenderer skinnedRenderer)
                        skinnedRenderer.updateWhenOffscreen = RequireBool(rendererData, "updateWhenOffscreen");
                    renderer.sharedMaterials = (rendererData["sharedMaterials"] as JArray ?? new JArray())
                        .Select(reference => reference is JObject value && value.Value<int?>("id") is { } id
                            ? ResolveReference(id) as Material : null).ToArray();
                    if (rendererData["sharedMesh"] is JObject meshReference && meshReference.Value<int?>("id") is { } meshId)
                    {
                        var mesh = ResolveReference(meshId) as Mesh;
                        if (renderer is SkinnedMeshRenderer skinned)
                        {
                            skinned.sharedMesh = mesh;
                            if (mesh != null)
                            {
                                mesh.bindposes = new[] { Matrix4x4.identity };
                                mesh.boneWeights = new[]
                                {
                                    new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                                    new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                                    new BoneWeight { boneIndex0 = 0, weight0 = 1 }
                                };
                                var bone = new GameObject("ReactiveReplayBone");
                                bone.transform.SetParent(skinned.transform, false);
                                skinned.rootBone = bone.transform;
                                skinned.bones = new[] { bone.transform };
                            }
                            foreach (var shape in OptionalArray(rendererData, "blendShapes").OfType<JObject>())
                            {
                                var index = mesh == null ? -1 : mesh.GetBlendShapeIndex(RequiredString(shape, "name"));
                                if (index >= 0) skinned.SetBlendShapeWeight(index, ReactiveDebugJson.ReadFiniteFloat(shape["weight"]!));
                            }
                        }
                        else if (renderer is MeshRenderer)
                        {
                            var meshFilter = renderer.GetComponent<MeshFilter>();
                            if (meshFilter == null) meshFilter = renderer.gameObject.AddComponent<MeshFilter>();
                            meshFilter.sharedMesh = mesh;
                        }
                    }
                }
                if (component is ModularAvatarMenuItem menuItem && record["menu"] is JObject menu)
                {
                    menuItem.PortableControl.Type = Enum.Parse<PortableControlType>(RequiredString(menu, "type"), false);
                    menuItem.PortableControl.Parameter = RequiredString(menu, "parameter");
                    menuItem.PortableControl.Value = ReactiveDebugJson.ReadFiniteFloat(menu["value"]!);
                    menuItem.isDefault = menu.Value<bool?>("isDefault") ?? false;
                    menuItem.isSaved = menu.Value<bool?>("isSaved") ?? false;
                    menuItem.isSynced = menu.Value<bool?>("isSynced") ?? false;
                    menuItem.automaticValue = false;
                }
            }

            private UnityEngine.Object? ResolveReference(int id) => _objects.TryGetValue(id, out var gameObject)
                ? gameObject
                : _componentIds.TryGetValue(id, out var component) ? component
                : _assets.TryGetValue(id, out var asset) ? asset : null;

            private UnityEngine.Object? ResolveSourceReference(JObject reference) => ResolveReference(RequiredInt(reference, "id"));

            private void CreateCutterProxies()
            {
                // A shared owner can have cutters with unequal selector partitions. Isolate them on ordered children;
                // a single stock filter produces a real nonempty cut on the synthetic positive-X mesh.
                foreach (var group in _components.Where(x => x.component is ModularAvatarMeshCutter).GroupBy(x => x.component.gameObject))
                {
                    var cutters = group.ToArray();
                    if (cutters.Length == 1)
                    {
                        AddSyntheticFilter(cutters[0].component.gameObject, cutters[0].record.Value<int?>("selectorPartitionId"));
                        continue;
                    }
                    for (var index = 0; index < cutters.Length; index++)
                    {
                        var proxy = new GameObject($"ReactiveReplayCutter-{index}");
                        proxy.transform.SetParent(group.Key.transform, false);
                        proxy.transform.SetSiblingIndex(index);
                        var cutter = proxy.AddComponent<ModularAvatarMeshCutter>();
                        ReactiveDebugSourceSchema.ApplyData(cutter, cutters[index].record["data"] as JObject ?? new JObject(), ResolveSourceReference);
                        AddSyntheticFilter(proxy, cutters[index].record.Value<int?>("selectorPartitionId"));
                        UnityEngine.Object.DestroyImmediate(cutters[index].component);
                    }
                }
            }

            private static void AddSyntheticFilter(GameObject owner, int? partition)
            {
                if (!partition.HasValue) return;
                var filter = owner.AddComponent<VertexFilterByAxisComponent>();
                filter.Axis = Vector3.right;
                filter.Center = new Vector3(-partition.Value, 0, 0);
                filter.SelectionMode = VertexSelectionMode.AnyVertex;
            }

            internal void CreateAnimatorInputs(GameObject root)
            {
                AnimationClip? activeStateClip = null;
                foreach (var record in RequiredArray(_input, "activeStateAnimations").OfType<JObject>())
                {
                    activeStateClip ??= new AnimationClip { name = "ReactiveReplayActiveStateInputs" };
                    var target = _objects[RequiredInt(record, "objectId")];
                    AnimationUtility.SetEditorCurve(activeStateClip, EditorCurveBinding.FloatCurve(
                            AnimationUtility.CalculateTransformPath(target.transform, root.transform), typeof(GameObject), "m_IsActive"),
                        AnimationCurve.Constant(0, 1, target.activeSelf ? 1 : 0));
                }
                if (activeStateClip != null) CreateAsset(activeStateClip, "ReactiveReplayActiveStateInputs.anim");
                var controller = new AnimatorController { name = "ReactiveReplayFX" };
                var parameters = new List<AnimatorControllerParameter>();
                foreach (var parameter in OptionalArray(_input, "animatorParameters").OfType<JObject>())
                {
                    var parameterType = Enum.Parse<AnimatorControllerParameterType>(RequiredString(parameter, "type"), false);
                    var defaultValue = parameter["defaultValue"];
                    parameters.Add(new AnimatorControllerParameter
                    {
                        name = RequiredString(parameter, "name"),
                        type = parameterType,
                        defaultBool = (parameterType is AnimatorControllerParameterType.Bool or AnimatorControllerParameterType.Trigger)
                                      && defaultValue != null && ReactiveDebugJson.ReadFiniteFloat(defaultValue) != 0,
                        defaultInt = parameterType == AnimatorControllerParameterType.Int && defaultValue != null ? (int)ReactiveDebugJson.ReadFiniteFloat(defaultValue) : 0,
                        defaultFloat = parameterType == AnimatorControllerParameterType.Float && defaultValue != null ? ReactiveDebugJson.ReadFiniteFloat(defaultValue) : 0
                    });
                }
                controller.parameters = parameters.ToArray();
                CreateAsset(controller, "ReactiveReplayFX.controller");
                controller.AddLayer("ReactiveReplay");
                if (activeStateClip != null)
                    controller.layers[0].stateMachine.AddState(activeStateClip.name).motion = activeStateClip;
                if (_input.Value<string>("platform") == "nadena.dev.ndmf.generic" && root.GetComponent<NDMFAvatarRoot>() == null)
                    root.AddComponent<NDMFAvatarRoot>();
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
#if MA_VRCSDK3_AVATARS
                if (_input.Value<string>("platform") == "nadena.dev.ndmf.vrchat.avatar3")
                {
                    var descriptor = root.GetComponent<VRCAvatarDescriptor>();
                    if (descriptor == null) descriptor = root.AddComponent<VRCAvatarDescriptor>();
                    var layers = descriptor.baseAnimationLayers;
                    if (layers == null || layers.Length == 0)
                    {
                        layers = new[]
                        {
                            new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                        };
                        descriptor.customizeAnimationLayers = true;
                    }
                    var fxIndex = Array.FindIndex(layers, layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX);
                    if (fxIndex >= 0)
                    {
                        layers[fxIndex].isDefault = false;
                        layers[fxIndex].animatorController = controller;
                        descriptor.baseAnimationLayers = layers;
                    }
                    var expression = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    expression.name = "ReactiveReplayExpressionParameters";
                    expression.parameters = OptionalArray(_input, "expressionParameters").OfType<JObject>().Select(parameter =>
                        new VRCExpressionParameters.Parameter
                        {
                            name = RequiredString(parameter, "name"),
                            valueType = Enum.Parse<VRCExpressionParameters.ValueType>(RequiredString(parameter, "type"), false),
                            defaultValue = ReactiveDebugJson.ReadFiniteFloat(parameter["defaultValue"]!),
                            saved = parameter.Value<bool?>("saved") ?? false,
                            networkSynced = parameter.Value<bool?>("synced") ?? false
                        }).ToArray();
                    CreateAsset(expression, "ReactiveReplayExpressionParameters.asset");
                    descriptor.expressionParameters = expression;
                }
#endif
            }

            internal void ApplyActiveStates()
            {
                foreach (var token in SourceRecords(RequiredArray(_input, "objects")))
                {
                    var record = (JObject)token!;
                    _objects[RequiredInt(record, "id")].SetActive(record.Value<bool?>("activeSelf") ?? true);
                }
            }

            private void CreateAsset(UnityEngine.Object asset, string filename)
            {
                var path = $"{_directory}/{filename}";
                AssetDatabase.CreateAsset(asset, path);
                _createdAssets.Add(path);
            }
        }

        private static void ApplyTransform(Transform transform, JObject record)
        {
            transform.localPosition = Vector3Value(record["localPosition"]);
            transform.localRotation = QuaternionValue(record["localRotation"]);
            transform.localScale = Vector3Value(record["localScale"], Vector3.one);
            if (record.Value<int?>("siblingIndex") is { } siblingIndex) transform.SetSiblingIndex(siblingIndex);
        }

        private static Type ComponentType(JObject record)
        {
            var kind = RequiredString(record, "kind");
            var type = record.Value<string>("type");
            return kind switch
            {
                nameof(ModularAvatarObjectToggle) => typeof(ModularAvatarObjectToggle),
                nameof(ModularAvatarShapeChanger) => typeof(ModularAvatarShapeChanger),
                nameof(ModularAvatarMaterialSetter) => typeof(ModularAvatarMaterialSetter),
                nameof(ModularAvatarMaterialSwap) => typeof(ModularAvatarMaterialSwap),
                nameof(ModularAvatarMeshCutter) => typeof(ModularAvatarMeshCutter),
                nameof(ModularAvatarReactiveMove) => typeof(ModularAvatarReactiveMove),
                nameof(ModularAvatarBlendshapeSync) => typeof(ModularAvatarBlendshapeSync),
                nameof(ModularAvatarMenuItem) => typeof(ModularAvatarMenuItem),
                nameof(SkinnedMeshRenderer) => typeof(SkinnedMeshRenderer),
                "Renderer" when type == nameof(MeshRenderer) => typeof(MeshRenderer),
                "Renderer" when type == nameof(ParticleSystemRenderer) => typeof(ParticleSystemRenderer),
                "Renderer" when type == nameof(TrailRenderer) => typeof(TrailRenderer),
                "Renderer" when type == nameof(LineRenderer) => typeof(LineRenderer),
                "Renderer" when type == nameof(SpriteRenderer) => typeof(SpriteRenderer),
                nameof(AudioSource) => typeof(AudioSource),
                _ => throw new InvalidDataException($"Unsupported component kind/type '{kind}'")
            };
        }

        private static void ValidateComponentKind(JObject record) => _ = ComponentType(record);

        private readonly struct CapturedParameter
        {
            internal readonly string Type;
            internal readonly float DefaultValue;

            internal CapturedParameter(string type, float defaultValue)
            {
                Type = type;
                DefaultValue = defaultValue;
            }
        }

        private static Dictionary<string, CapturedParameter> ValidateExpressionParameters(JArray parameters,
            ISet<string> contradictoryParameters)
        {
            var result = new Dictionary<string, CapturedParameter>(StringComparer.Ordinal);
            foreach (var token in parameters)
            {
                var parameter = token as JObject ?? throw new InvalidDataException("Expression parameter is not an object");
                RequireExactFields(parameter, "expression parameter", "name", "type", "defaultValue", "saved", "synced");
                var name = RequiredNonEmptyString(parameter, "name", "expression parameter");
                var type = RequiredString(parameter, "type");
                if (type is not ("Int" or "Float" or "Bool"))
                    throw new InvalidDataException($"Unsupported expression parameter type '{type}'");
                var captured = new CapturedParameter(type, ReactiveDebugJson.ReadFiniteFloat(
                    parameter["defaultValue"] ?? throw new InvalidDataException("Expression parameter default is missing")));
                RequireBoolean(parameter, "saved");
                RequireBoolean(parameter, "synced");
                if (result.TryGetValue(name, out var existing))
                {
                    if (!string.Equals(existing.Type, captured.Type, StringComparison.Ordinal)
                        || !existing.DefaultValue.Equals(captured.DefaultValue))
                        contradictoryParameters.Add(name);
                }
                else result.Add(name, captured);
            }
            return result;
        }

        private static Dictionary<string, CapturedParameter> ValidateAnimatorParameters(JArray parameters,
            ISet<string> contradictoryParameters)
        {
            var result = new Dictionary<string, CapturedParameter>(StringComparer.Ordinal);
            foreach (var token in parameters)
            {
                var parameter = token as JObject ?? throw new InvalidDataException("Animator parameter is not an object");
                RequireExactFields(parameter, "animator parameter", "name", "type", "defaultValue");
                var name = RequiredNonEmptyString(parameter, "name", "animator parameter");
                var type = RequiredString(parameter, "type");
                if (!Enum.TryParse<AnimatorControllerParameterType>(type, false, out var animatorType)
                    || !Enum.IsDefined(typeof(AnimatorControllerParameterType), animatorType))
                    throw new InvalidDataException($"Unsupported animator parameter type '{type}'");
                var captured = new CapturedParameter(type, ReactiveDebugJson.ReadFiniteFloat(
                    parameter["defaultValue"] ?? throw new InvalidDataException("Animator parameter default is missing")));
                if (result.TryGetValue(name, out var existing))
                {
                    if (!string.Equals(existing.Type, captured.Type, StringComparison.Ordinal)
                        || !existing.DefaultValue.Equals(captured.DefaultValue))
                        contradictoryParameters.Add(name);
                }
                else result.Add(name, captured);
            }
            return result;
        }

        private static void ValidateInitialParameterStates(JArray states, ISet<string> menuParameters,
            IReadOnlyDictionary<string, CapturedParameter> expressionParameters, ISet<string> contradictoryExpressionParameters,
            IReadOnlyDictionary<string, CapturedParameter> animatorParameters, ISet<string> contradictoryAnimatorParameters)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in states)
            {
                var state = token as JObject ?? throw new InvalidDataException("Initial parameter state is not an object");
                RequireExactFields(state, "initial parameter state", "name", "expressionParameters", "fxLayer");
                var name = RequiredNonEmptyString(state, "name", "initial parameter state");
                if (!names.Add(name)) throw new InvalidDataException($"Duplicate initial parameter state '{name}'");

                ValidateInitialParameterSource(state["expressionParameters"], "expressionParameters", name,
                    expressionParameters, contradictoryExpressionParameters, type => type is "Int" or "Float" or "Bool");
                ValidateInitialParameterSource(state["fxLayer"], "fxLayer", name, animatorParameters,
                    contradictoryAnimatorParameters, type => Enum.TryParse<AnimatorControllerParameterType>(type, false, out var parsed)
                                                       && Enum.IsDefined(typeof(AnimatorControllerParameterType), parsed));
            }

            foreach (var name in menuParameters)
                if (!names.Contains(name)) throw new InvalidDataException($"Referenced menu parameter '{name}' has no initial parameter state");
            foreach (var name in expressionParameters.Keys)
                if (!names.Contains(name)) throw new InvalidDataException($"Expression parameter '{name}' has no initial parameter state");
            foreach (var name in animatorParameters.Keys)
                if (!names.Contains(name)) throw new InvalidDataException($"Animator parameter '{name}' has no initial parameter state");
        }

        private static void ValidateInitialParameterSource(JToken? token, string sourceName, string parameterName,
            IReadOnlyDictionary<string, CapturedParameter> sourceParameters, ISet<string> contradictoryParameters,
            Func<string, bool> validType)
        {
            var source = token as JObject ?? throw new InvalidDataException(
                $"Initial parameter state '{parameterName}' has invalid {sourceName} data");
            if (contradictoryParameters.Contains(parameterName))
                throw new InvalidDataException($"Initial parameter state '{parameterName}' has contradictory {sourceName} source records");
            var present = RequireBool(source, "present");
            if (!present)
            {
                RequireExactFields(source, $"{sourceName} initial parameter state", "present");
                if (sourceParameters.ContainsKey(parameterName))
                    throw new InvalidDataException($"Initial parameter state '{parameterName}' incorrectly marks {sourceName} absent");
                return;
            }

            RequireExactFields(source, $"{sourceName} initial parameter state", "present", "type", "defaultValue");
            var type = RequiredString(source, "type");
            if (!validType(type))
                throw new InvalidDataException($"Unsupported {sourceName} parameter type '{type}'");
            var defaultValue = ReactiveDebugJson.ReadFiniteFloat(
                source["defaultValue"] ?? throw new InvalidDataException($"Initial parameter state '{parameterName}' is missing {sourceName} default"));
            if (!sourceParameters.TryGetValue(parameterName, out var canonical))
                throw new InvalidDataException($"Initial parameter state '{parameterName}' incorrectly marks {sourceName} present");
            if (!string.Equals(type, canonical.Type, StringComparison.Ordinal) || !defaultValue.Equals(canonical.DefaultValue))
                throw new InvalidDataException($"Initial parameter state '{parameterName}' disagrees with the {sourceName} source");
        }

        private static string RequiredNonEmptyString(JObject value, string name, string recordName)
        {
            var result = RequiredString(value, name);
            if (string.IsNullOrEmpty(result)) throw new InvalidDataException($"{recordName} field '{name}' must not be empty");
            return result;
        }
        private static JArray RequiredArray(JObject value, string name) => value[name] as JArray ?? throw new InvalidDataException($"Missing required array '{name}'");
        private static JArray OptionalArray(JObject value, string name) => value[name] as JArray ?? new JArray();
        private static string RequiredString(JObject value, string name) => value.Value<string>(name) ?? throw new InvalidDataException($"Missing required string '{name}'");
        private static int RequiredInt(JObject value, string name) => value.Value<int?>(name) ?? throw new InvalidDataException($"Missing required integer '{name}'");
        private static bool RequireBool(JObject value, string name)
        {
            if (value[name]?.Type != JTokenType.Boolean)
                throw new InvalidDataException($"Missing required boolean '{name}'");
            return value.Value<bool>(name);
        }

        private static void RequireExactFields(JObject value, string recordName, params string[] names)
        {
            RequireOnlyFields(value, recordName, names);
            foreach (var name in names)
                if (value.Property(name) == null)
                    throw new InvalidDataException($"{recordName} is missing required field '{name}'");
        }

        private static void RequireOnlyFields(JObject value, string recordName, params string[] names)
        {
            var allowed = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var property in value.Properties())
                if (!allowed.Contains(property.Name))
                    throw new InvalidDataException($"{recordName} contains unsupported field '{property.Name}'");
        }

        private static IEnumerable<JToken> SourceRecords(JArray records) =>
            records.Where(record => record is JObject value && value.Value<bool?>("source") == true);

        private static void ValidateRendererRecord(JObject record, IReadOnlyDictionary<int, string> sourceAssetKinds)
        {
            var renderer = record["renderer"] as JObject ?? throw new InvalidDataException("Renderer record is missing renderer data");
            if (RequiredString(record, "kind") == nameof(SkinnedMeshRenderer))
            {
                RequireExactFields(renderer, "renderer", "sharedMaterials", "sharedMesh", "blendShapes", "updateWhenOffscreen");
                RequireBoolean(renderer, "updateWhenOffscreen");
            }
            else
            {
                RequireExactFields(renderer, "renderer", "sharedMaterials", "sharedMesh", "blendShapes");
            }
            var materials = renderer["sharedMaterials"] as JArray ?? throw new InvalidDataException("Renderer materials are missing");
            foreach (var material in materials)
            {
                if (material.Type == JTokenType.Null) continue;
                if (material is not JObject materialReference || materialReference.Value<string>("kind") != "asset"
                    || materialReference.Value<int?>("id") is not { } materialId
                    || !sourceAssetKinds.TryGetValue(materialId, out var materialKind) || materialKind != "Material")
                    throw new InvalidDataException("Renderer material reference is invalid");
            }
            var mesh = renderer["sharedMesh"];
            if (mesh?.Type != JTokenType.Null
                && (mesh is not JObject reference || reference.Value<string>("kind") != "asset"
                    || reference.Value<int?>("id") is not { } id || !sourceAssetKinds.TryGetValue(id, out var kind) || kind != "Mesh"))
                throw new InvalidDataException("Renderer mesh reference is invalid");
            foreach (var shape in RequiredArray(renderer, "blendShapes").OfType<JObject>())
            {
                _ = RequiredString(shape, "name");
                _ = ReactiveDebugJson.ReadFiniteFloat(shape["weight"] ?? throw new InvalidDataException("Blendshape weight is missing"));
            }
        }

        private static void RequireBoolean(JObject record, string name)
        {
            if (record[name]?.Type != JTokenType.Boolean)
                throw new InvalidDataException($"Missing or invalid boolean '{name}'");
        }

        private static void ValidateComponentData(JObject record)
        {
            var type = ComponentType(record);
            // These components have explicit portable records and intentionally omit raw source data.
            if (typeof(Renderer).IsAssignableFrom(type) || type == typeof(AudioSource) || type == typeof(ModularAvatarMenuItem))
            {
                if (record.Property("data") != null) throw new InvalidDataException($"Unexpected raw data for {type.Name}");
                return;
            }
            var data = record["data"] as JObject ?? throw new InvalidDataException("Component data is missing");
            ReactiveDebugSourceSchema.ValidateData(RequiredString(record, "kind"), data);
        }

        private static void ValidateComponentRecordFields(JObject record)
        {
            var fields = new List<string>
            {
                "id", "source", "objectId", "type", "enabled", "kind", "componentIndex"
            };
            switch (RequiredString(record, "kind"))
            {
                case nameof(ModularAvatarMenuItem):
                    fields.Add("menu");
                    break;
                case nameof(SkinnedMeshRenderer):
                case "Renderer":
                    fields.Add("renderer");
                    break;
                case nameof(AudioSource):
                    fields.Add("audio");
                    break;
                default:
                    fields.Add("data");
                    if (RequiredString(record, "kind") == nameof(ModularAvatarMeshCutter))
                    {
                        fields.Add("selectorPartitionId");
                        fields.Add("selectorCaptureError");
                    }
                    break;
            }
            RequireOnlyFields(record, "component record", fields.ToArray());
        }

        private static void ValidateSourceReferences(JObject record, IReadOnlyDictionary<int, JObject> objectRecords,
            IReadOnlyDictionary<int, string> assetKinds)
        {
            if (record["data"] is not JObject data) return;
            void ObjectReference(JToken token)
            {
                var reference = token as JObject ?? throw new InvalidDataException("Object reference is invalid");
                RequireExactFields(reference, "object reference", "path", "target");
                _ = RequiredString(reference, "path");
                if (reference["target"]?.Type == JTokenType.Null) return;
                if (reference["target"] is not JObject target || target.Value<string>("kind") != "object"
                    || target.Value<int?>("id") is not { } id || !objectRecords.ContainsKey(id))
                    throw new InvalidDataException("Object reference targets an unknown source object");
            }
            void MaterialReference(JToken token)
            {
                if (token.Type == JTokenType.Null) return;
                if (token is not JObject reference || reference.Value<string>("kind") != "asset"
                    || reference.Value<int?>("id") is not { } id || !assetKinds.TryGetValue(id, out var assetKind)
                    || assetKind != "Material")
                    throw new InvalidDataException("Material reference targets an unknown source material");
                RequireExactFields(reference, "material reference", "kind", "id");
            }
            void RequireMoveTransform(JToken token)
            {
                if (token["target"] is not JObject target || target.Value<int?>("id") is not { } id) return;
                while (true)
                {
                    var referencedObject = objectRecords[id];
                    if (referencedObject["localPosition"] == null || referencedObject["localRotation"] == null
                        || referencedObject["localScale"] == null)
                        throw new InvalidDataException("Reactive move references an object without captured local transform");
                    if (referencedObject.Value<int?>("parentId") is not { } parentId) break;
                    id = parentId;
                }
            }
            switch (RequiredString(record, "kind"))
            {
                case nameof(ModularAvatarObjectToggle):
                    foreach (var entry in (JArray)data["m_objects"]!) ObjectReference(entry["Object"]!);
                    break;
                case nameof(ModularAvatarShapeChanger):
                    foreach (var entry in (JArray)data["m_shapes"]!) ObjectReference(entry["Object"]!);
                    break;
                case nameof(ModularAvatarMaterialSetter):
                    foreach (var entry in (JArray)data["m_objects"]!)
                    {
                        ObjectReference(entry["Object"]!);
                        MaterialReference(entry["Material"]!);
                    }
                    break;
                case nameof(ModularAvatarMaterialSwap):
                    ObjectReference(data["m_root"]!);
                    foreach (var entry in (JArray)data["m_swaps"]!)
                    {
                        MaterialReference(entry["From"]!);
                        MaterialReference(entry["To"]!);
                    }
                    break;
                case nameof(ModularAvatarMeshCutter):
                    ObjectReference(data["m_object"]!);
                    break;
                case nameof(ModularAvatarReactiveMove):
                    ObjectReference(data["m_toMove"]!);
                    ObjectReference(data["m_whereTo"]!);
                    RequireMoveTransform(data["m_toMove"]!);
                    RequireMoveTransform(data["m_whereTo"]!);
                    break;
                case nameof(ModularAvatarBlendshapeSync):
                    foreach (var entry in (JArray)data["Bindings"]!)
                        ObjectReference(entry["ReferenceMesh"]!);
                    break;
            }
        }



        private static void ValidateVector(JToken? value, int count, string field)
        {
            if (value is not JArray vector || vector.Count != count)
                throw new InvalidDataException($"Object field '{field}' is invalid");
            foreach (var component in vector) _ = ReactiveDebugJson.ReadFiniteFloat(component);
        }

        private static void ValidateOptionalVector(JToken? value, int count, string field)
        {
            if (value != null) ValidateVector(value, count, field);
        }

        private static Vector3 Vector3Value(JToken? value, Vector3 fallback = default)
        {
            if (value is not JArray vector || vector.Count != 3) return fallback;
            return new Vector3(ReactiveDebugJson.ReadFiniteFloat(vector[0]!), ReactiveDebugJson.ReadFiniteFloat(vector[1]!),
                ReactiveDebugJson.ReadFiniteFloat(vector[2]!));
        }

        private static Quaternion QuaternionValue(JToken? value)
        {
            if (value is not JArray quaternion || quaternion.Count != 4) return Quaternion.identity;
            return new Quaternion(ReactiveDebugJson.ReadFiniteFloat(quaternion[0]!), ReactiveDebugJson.ReadFiniteFloat(quaternion[1]!),
                ReactiveDebugJson.ReadFiniteFloat(quaternion[2]!), ReactiveDebugJson.ReadFiniteFloat(quaternion[3]!));
        }
    }
}
