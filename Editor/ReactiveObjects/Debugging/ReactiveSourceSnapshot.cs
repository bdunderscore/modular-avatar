#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.preview;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class ReactiveSourceSnapshot
    {
        internal static JObject Capture(GameObject avatarRoot, AnimatorServicesContext animatorServices, ReactiveDebugJson codec)
        {
            var input = new JObject
            {
                ["platform"] = codec.Platform, ["avatarName"] = avatarRoot.name,
                ["objects"] = codec.Objects, ["components"] = codec.Components, ["assets"] = codec.Assets,
                ["animatorParameters"] = new JArray(), ["expressionParameters"] = new JArray(),
                ["initialParameterStates"] = new JArray(),
                ["activeStateAnimations"] = new JArray(), ["selectorPartitions"] = codec.SelectorPartitions,
            };
            var citedParameters = CitedMenuParameters(avatarRoot);
            var closure = BuildClosure(avatarRoot, animatorServices, codec, (JArray)input["activeStateAnimations"]!);
            foreach (var transform in avatarRoot.GetComponentsInChildren<Transform>(true))
                if (closure.Objects.Contains(transform.gameObject))
                    DescribeObject(transform.gameObject, avatarRoot, codec, closure.TransformObjects.Contains(transform.gameObject));
            foreach (var transform in avatarRoot.GetComponentsInChildren<Transform>(true))
            foreach (var component in transform.GetComponents<Component>())
                if (component != null && closure.Components.Contains(component))
                    DescribeComponent(component, codec, closure.RequiredBlendshapes);
            CaptureParameters(avatarRoot, animatorServices, codec, input, citedParameters);
            foreach (var asset in codec.Assets.OfType<JObject>()) asset["source"] = true;
            return input;
        }

        private sealed class Closure
        {
            internal readonly HashSet<GameObject> Objects = new();
            internal readonly HashSet<GameObject> TransformObjects = new();
            internal readonly HashSet<Component> Components = new();
            internal readonly HashSet<Renderer> Renderers = new();
            internal readonly HashSet<AudioSource> AudioSources = new();
            internal readonly Dictionary<SkinnedMeshRenderer, HashSet<string>> RequiredBlendshapes = new();
        }

        private static Closure BuildClosure(GameObject root, AnimatorServicesContext services, ReactiveDebugJson codec, JArray activeAnimations)
        {
            var closure = new Closure();
            AddObject(root, root, closure);
            var reactive = root.GetComponentsInChildren<Component>(true).Where(ReactiveDebugSourceSchema.IsSupported).ToList();
            foreach (var component in reactive)
            {
                AddObject(component.gameObject, root, closure);
                closure.Components.Add(component);
                foreach (var target in ReactiveDebugSourceSchema.ReferencedObjects(component)) AddObject(target, root, closure);
                if (component is ModularAvatarReactiveMove)
                    foreach (var target in ReactiveDebugSourceSchema.ReferencedObjects(component)) AddTransformPath(target, root, closure);
                var menu = NearestMenu(component.transform, root.transform);
                if (menu != null)
                {
                    closure.Components.Add(menu);
                    AddObject(menu.gameObject, root, closure);
                }
            }

            foreach (var sync in reactive.OfType<ModularAvatarBlendshapeSync>())
            {
                var renderer = sync.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null) AddRenderer(renderer, root, closure);
            }

            foreach (var swap in reactive.OfType<ModularAvatarMaterialSwap>())
            {
                var scope = ReactiveDebugSourceSchema.ReferencedObjects(swap).FirstOrDefault() ?? root;
                foreach (var renderer in scope.GetComponentsInChildren<Renderer>(true)) AddRenderer(renderer, root, closure);
            }
            foreach (var component in reactive)
            foreach (var target in ReactiveDebugSourceSchema.ReferencedObjects(component))
            {
                var renderer = target.GetComponent<Renderer>();
                if (renderer != null) AddRenderer(renderer, root, closure);
                if (component is ModularAvatarObjectToggle)
                    foreach (var audio in target.GetComponentsInChildren<AudioSource>(true))
                    {
                        closure.AudioSources.Add(audio);
                        AddObject(audio.gameObject, root, closure);
                    }
            }

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var path = services.ObjectPathRemapper.GetVirtualPathForObject(transform.gameObject);
                var binding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
                if (!services.AnimationIndex.GetClipsForBinding(binding).Any()) continue;
                AddObject(transform.gameObject, root, closure);
                activeAnimations.Add(new JObject { ["objectId"] = codec.ObjectId(transform.gameObject), ["path"] = path });
            }

            foreach (var renderer in closure.Renderers)
            {
                if (renderer is not SkinnedMeshRenderer skinned) continue;
                var shapes = RequiredShapes(skinned, reactive);
                if (shapes.Count > 0) closure.RequiredBlendshapes[skinned] = shapes;
            }
            foreach (var renderer in closure.Renderers) closure.Components.Add(renderer);
            foreach (var audio in closure.AudioSources) closure.Components.Add(audio);
            return closure;
        }

        private static HashSet<string> RequiredShapes(SkinnedMeshRenderer renderer, IEnumerable<Component> reactive)
        {
            var shapes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var changer in reactive.OfType<ModularAvatarShapeChanger>())
            foreach (var shape in changer.Shapes)
                if (shape.Object?.Get(changer) == renderer.gameObject && shape.ShapeName != null)
                    shapes.Add(shape.ShapeName);
            foreach (var sync in reactive.OfType<ModularAvatarBlendshapeSync>())
            {
                foreach (var binding in sync.Bindings)
                {
                    var source = binding.ReferenceMesh?.Get(sync);
                    if (source == renderer.gameObject && binding.Blendshape != null) shapes.Add(binding.Blendshape);
                    if (sync.gameObject == renderer.gameObject)
                    {
                        var localShape = string.IsNullOrWhiteSpace(binding.LocalBlendshape) ? binding.Blendshape : binding.LocalBlendshape;
                        if (localShape != null) shapes.Add(localShape);
                    }
                }
            }
            return shapes;
        }

        private static void AddRenderer(Renderer renderer, GameObject root, Closure closure)
        {
            closure.Renderers.Add(renderer);
            AddObject(renderer.gameObject, root, closure);
        }

        private static void AddTransformPath(GameObject target, GameObject root, Closure closure)
        {
            for (var current = target.transform; current != null; current = current.parent)
            {
                closure.TransformObjects.Add(current.gameObject);
                if (current.gameObject == root) break;
            }
        }

        private static void AddObject(GameObject go, GameObject root, Closure closure)
        {
            for (var current = go.transform; current != null; current = current.parent)
            {
                closure.Objects.Add(current.gameObject);
                if (current.gameObject == root) break;
                // Retain duplicate sibling names so AvatarObjectReference path resolution remains unambiguous.
                foreach (Transform sibling in current.parent)
                    if (sibling != current && sibling.name == current.name) closure.Objects.Add(sibling.gameObject);
            }
        }

        private static ModularAvatarMenuItem? NearestMenu(Transform transform, Transform root)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                var menu = current.GetComponent<ModularAvatarMenuItem>();
                if (menu != null) return menu;
                if (current == root) break;
            }
            return null;
        }

        private static void DescribeObject(GameObject go, GameObject avatarRoot, ReactiveDebugJson codec, bool includeTransform)
        {
            var id = codec.ObjectId(go);
            var record = codec.Objects.OfType<JObject>().First(o => o.Value<int>("id") == id);
            record["source"] = true;
            record["parentId"] = go == avatarRoot ? null : codec.ObjectId(go.transform.parent!.gameObject);
            record["siblingIndex"] = go.transform.GetSiblingIndex();
            record["activeSelf"] = go.activeSelf;
            record["path"] = go == avatarRoot ? "" : AnimationUtility.CalculateTransformPath(go.transform, avatarRoot.transform);
            if (includeTransform)
            {
                record["localPosition"] = Vector(codec, go.transform.localPosition);
                record["localRotation"] = new JArray { codec.FiniteFloat(go.transform.localRotation.x), codec.FiniteFloat(go.transform.localRotation.y), codec.FiniteFloat(go.transform.localRotation.z), codec.FiniteFloat(go.transform.localRotation.w) };
                record["localScale"] = Vector(codec, go.transform.localScale);
            }
        }

        private static void DescribeComponent(Component component, ReactiveDebugJson codec,
            IReadOnlyDictionary<SkinnedMeshRenderer, HashSet<string>> requiredBlendshapes)
        {
            var id = codec.ObjectId(component);
            var record = codec.Components.OfType<JObject>().First(c => c.Value<int>("id") == id);
            record["source"] = true;
            record["kind"] = ComponentKind(component);
            record["componentIndex"] = ComponentOrder(component);
            if (ReactiveDebugSourceSchema.IsSupported(component)) record["data"] = ReactiveDebugSourceSchema.Capture(component, codec);
            switch (component)
            {
                case Renderer renderer:
                    Mesh? mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    var shapeRecords = renderer is SkinnedMeshRenderer smr && mesh != null && requiredBlendshapes.TryGetValue(smr, out var required)
                        ? new JArray(Enumerable.Range(0, mesh.blendShapeCount).Where(i => required.Contains(mesh.GetBlendShapeName(i))).Select(i => new JObject { ["name"] = mesh.GetBlendShapeName(i), ["weight"] = codec.FiniteFloat(smr.GetBlendShapeWeight(i)) }))
                        : new JArray();
                    var rendererRecord = new JObject { ["sharedMaterials"] = new JArray(renderer.sharedMaterials.Select(codec.Reference)), ["sharedMesh"] = codec.Reference(mesh), ["blendShapes"] = shapeRecords };
                    if (renderer is SkinnedMeshRenderer skinnedRenderer) rendererRecord["updateWhenOffscreen"] = skinnedRenderer.updateWhenOffscreen;
                    record["renderer"] = rendererRecord;
                    break;
                case ModularAvatarMeshCutter cutter:
                    try { record["selectorPartitionId"] = SelectorPartition(cutter, codec); }
                    catch (Exception exception)
                    {
                        record["selectorCaptureError"] = "SelectorCaptureFailed";
                        Debug.LogWarning($"Unable to capture the mesh selector for {cutter.name}: {exception.Message}");
                    }
                    break;
                case AudioSource audio:
                    record["audio"] = new JObject { ["enabled"] = audio.enabled };
                    break;
                case ModularAvatarMenuItem menu:
                    record["menu"] = new JObject { ["type"] = menu.PortableControl.Type.ToString(), ["parameter"] = menu.PortableControl.Parameter, ["value"] = codec.FiniteFloat(menu.PortableControl.Value), ["isDefault"] = menu.isDefault, ["isSaved"] = menu.isSaved, ["isSynced"] = menu.isSynced, ["automaticValue"] = menu.automaticValue };
                    break;
            }
        }

        private static string ComponentKind(Component component) => component switch
        {
            ModularAvatarObjectToggle _ => nameof(ModularAvatarObjectToggle), ModularAvatarShapeChanger _ => nameof(ModularAvatarShapeChanger),
            ModularAvatarMaterialSetter _ => nameof(ModularAvatarMaterialSetter), ModularAvatarMaterialSwap _ => nameof(ModularAvatarMaterialSwap),
            ModularAvatarMeshCutter _ => nameof(ModularAvatarMeshCutter), ModularAvatarReactiveMove _ => nameof(ModularAvatarReactiveMove),
            ModularAvatarBlendshapeSync _ => nameof(ModularAvatarBlendshapeSync), ModularAvatarMenuItem _ => nameof(ModularAvatarMenuItem),
            SkinnedMeshRenderer _ => nameof(SkinnedMeshRenderer), Renderer _ => nameof(Renderer), AudioSource _ => nameof(AudioSource),
            _ => throw new ArgumentOutOfRangeException(nameof(component))
        };

        private static int? SelectorPartition(ModularAvatarMeshCutter cutter, ReactiveDebugJson codec)
        {
            var selectors = new List<IMeshSelector>();
            foreach (var behavior in cutter.GetComponents<IMeshSelectorBehavior>())
                if (VertexFilterRegistry.TryGetVertexFilter(behavior, ComputeContext.NullContext, out var selector) && selector != null) selectors.Add(selector);
            if (selectors.Count == 0) return null;
            var combined = selectors.Count == 1 ? selectors[0] : cutter.MultiMode == MeshCutterMultiMode.VertexUnion ? new ORFilter(selectors) : new ANDFilter(selectors);
            return codec.SelectorPartitionId(combined);
        }

        private static ISet<string> CitedMenuParameters(GameObject avatarRoot)
        {
            return avatarRoot.GetComponentsInChildren<ModularAvatarMenuItem>(true)
                .Select(menu => menu.PortableControl.Parameter)
                .Where(parameter => !string.IsNullOrEmpty(parameter))
                .ToHashSet(StringComparer.Ordinal);
        }

        private static void CaptureParameters(GameObject avatarRoot, AnimatorServicesContext services, ReactiveDebugJson codec, JObject input, ISet<string> citedParameters)
        {
            var fxStates = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var expressionStates = new Dictionary<string, JObject>(StringComparer.Ordinal);
#if MA_VRCSDK3_AVATARS
            if (services.ControllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fx))
                foreach (var (name, parameter) in fx.Parameters.Where(p => citedParameters.Contains(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    var state = AnimatorParameterState(parameter, codec);
                    ((JArray)input["animatorParameters"]!).Add(new JObject(state) { ["name"] = name });
                    fxStates[name] = state;
                }

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null && descriptor.expressionParameters != null && descriptor.expressionParameters.parameters != null)
                foreach (var parameter in descriptor.expressionParameters.parameters.Where(p => citedParameters.Contains(p.name)))
                {
                    var state = new JObject
                    {
                        ["type"] = parameter.valueType.ToString(),
                        ["defaultValue"] = codec.FiniteFloat(parameter.defaultValue)
                    };
                    ((JArray)input["expressionParameters"]!).Add(new JObject(state)
                    {
                        ["name"] = parameter.name,
                        ["saved"] = parameter.saved,
                        ["synced"] = parameter.networkSynced
                    });
                    // VRChat's graph preprocessing consumes matching expression parameters in source order.
                    if (!expressionStates.ContainsKey(parameter.name)) expressionStates.Add(parameter.name, state);
                }
#endif
            foreach (var name in citedParameters.OrderBy(name => name, StringComparer.Ordinal))
            {
                ((JArray)input["initialParameterStates"]!).Add(new JObject
                {
                    ["name"] = name,
                    ["expressionParameters"] = SourcePresence(expressionStates, name),
                    ["fxLayer"] = SourcePresence(fxStates, name)
                });
            }
        }

#if MA_VRCSDK3_AVATARS
        private static JObject AnimatorParameterState(AnimatorControllerParameter parameter, ReactiveDebugJson codec)
        {
            var defaultValue = parameter.type switch
            {
                AnimatorControllerParameterType.Bool or AnimatorControllerParameterType.Trigger => parameter.defaultBool ? 1 : 0,
                AnimatorControllerParameterType.Int => parameter.defaultInt,
                AnimatorControllerParameterType.Float => parameter.defaultFloat,
                _ => throw new ArgumentOutOfRangeException(nameof(parameter.type), parameter.type, "Unsupported animator parameter type")
            };
            return new JObject { ["type"] = parameter.type.ToString(), ["defaultValue"] = codec.FiniteFloat(defaultValue) };
        }
#endif

        private static JObject SourcePresence(IReadOnlyDictionary<string, JObject> states, string name)
        {
            if (!states.TryGetValue(name, out var state)) return new JObject { ["present"] = false };

            var record = new JObject { ["present"] = true };
            foreach (var property in state.Properties())
                record[property.Name] = property.Value.DeepClone();
            return record;
        }

        private static int ComponentOrder(Component component) => Array.IndexOf(component.gameObject.GetComponents<Component>(), component);
        private static string HierarchyOrder(GameObject go, GameObject root) => go == root ? "" : AnimationUtility.CalculateTransformPath(go.transform, root.transform);
        private static JArray Vector(ReactiveDebugJson codec, Vector3 value) => new() { codec.FiniteFloat(value.x), codec.FiniteFloat(value.y), codec.FiniteFloat(value.z) };
    }
}
