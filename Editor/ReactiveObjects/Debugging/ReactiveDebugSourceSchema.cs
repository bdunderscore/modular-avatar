#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    /// Closed serialization contract for compiler inputs. This deliberately names every supported
    /// serialized field: it must never discover or export future component fields.
    /// </summary>
    internal static class ReactiveDebugSourceSchema
    {
        private const string Inverted = "m_inverted";

        internal static bool IsSupported(Component component) => component is ModularAvatarObjectToggle
            or ModularAvatarShapeChanger or ModularAvatarMaterialSetter or ModularAvatarMaterialSwap
            or ModularAvatarMeshCutter or ModularAvatarReactiveMove or ModularAvatarBlendshapeSync;

        internal static JObject Capture(Component component, ReactiveDebugJson json)
        {
            JObject Reference(AvatarObjectReference? reference) => AvatarReference(reference, component, json);
            JObject data = component switch
            {
                ModularAvatarObjectToggle toggle => new JObject
                {
                    [Inverted] = toggle.Inverted,
                    ["m_objects"] = new JArray((toggle.Objects ?? Enumerable.Empty<ToggledObject>()).Select(entry => new JObject
                    {
                        ["Object"] = Reference(entry.Object), ["Active"] = entry.Active
                    }))
                },
                ModularAvatarShapeChanger changer => new JObject
                {
                    [Inverted] = changer.Inverted,
                    ["m_shapes"] = new JArray((changer.Shapes ?? Enumerable.Empty<ChangedShape>()).Select(entry => new JObject
                    {
                        ["Object"] = Reference(entry.Object), ["ShapeName"] = entry.ShapeName ?? "",
                        ["ChangeType"] = entry.ChangeType.ToString(), ["Value"] = json.FiniteFloat(entry.Value)
                    })),
                    ["m_threshold"] = json.FiniteFloat(changer.Threshold)
                },
                ModularAvatarMaterialSetter setter => new JObject
                {
                    [Inverted] = setter.Inverted,
                    ["m_objects"] = new JArray((setter.Objects ?? Enumerable.Empty<MaterialSwitchObject>()).Select(entry => new JObject
                    {
                        ["Object"] = Reference(entry.Object), ["Material"] = json.Reference(entry.Material), ["MaterialIndex"] = entry.MaterialIndex
                    }))
                },
                ModularAvatarMaterialSwap swap => new JObject
                {
                    [Inverted] = swap.Inverted, ["m_root"] = Reference(swap.Root),
                    ["m_swaps"] = new JArray((swap.Swaps ?? Enumerable.Empty<MatSwap>()).Select(entry => new JObject
                    {
                        ["From"] = json.Reference(entry.From), ["To"] = json.Reference(entry.To)
                    }))
                },
                ModularAvatarMeshCutter cutter => new JObject
                {
                    [Inverted] = cutter.Inverted, ["m_object"] = Reference(cutter.Object),
                    ["m_multiMode"] = cutter.MultiMode.ToString()
                },
                ModularAvatarReactiveMove move => new JObject
                {
                    [Inverted] = move.Inverted, ["m_toMove"] = Reference(move.ToMove), ["m_whereTo"] = Reference(move.WhereTo),
                    ["m_fixToWorld"] = move.FixToWorld, ["m_transitionTime"] = json.FiniteFloat(move.TransitionTime),
                    ["m_setPosition"] = move.SetsPosition, ["m_setRotation"] = move.SetsRotation, ["m_setScale"] = move.SetsScale
                },
                ModularAvatarBlendshapeSync sync => new JObject
                {
                    ["Bindings"] = new JArray((sync.Bindings ?? Enumerable.Empty<BlendshapeBinding>()).Select(entry => new JObject
                    {
                        ["ReferenceMesh"] = Reference(entry.ReferenceMesh), ["Blendshape"] = entry.Blendshape ?? "",
                        ["LocalBlendshape"] = entry.LocalBlendshape ?? ""
                    }))
                },
                _ => throw new ArgumentException("Unsupported reactive debug source component", nameof(component))
            };
            return data;
        }

        internal static IEnumerable<GameObject> ReferencedObjects(Component component) => component switch
        {
            ModularAvatarObjectToggle toggle => (toggle.Objects ?? Enumerable.Empty<ToggledObject>())
                .Select(entry => entry.Object?.Get(toggle)).Where(target => target != null).Cast<GameObject>(),
            ModularAvatarShapeChanger changer => (changer.Shapes ?? Enumerable.Empty<ChangedShape>())
                .Select(entry => entry.Object?.Get(changer)).Where(target => target != null).Cast<GameObject>(),
            ModularAvatarMaterialSetter setter => (setter.Objects ?? Enumerable.Empty<MaterialSwitchObject>())
                .Select(entry => entry.Object?.Get(setter)).Where(target => target != null).Cast<GameObject>(),
            ModularAvatarMaterialSwap swap => Resolve(swap.Root, swap),
            ModularAvatarMeshCutter cutter => Resolve(cutter.Object, cutter),
            ModularAvatarReactiveMove move => Resolve(move.ToMove, move).Concat(Resolve(move.WhereTo, move)),
            ModularAvatarBlendshapeSync sync => (sync.Bindings ?? Enumerable.Empty<BlendshapeBinding>())
                .Select(entry => entry.ReferenceMesh?.Get(sync)).Where(target => target != null).Cast<GameObject>(),
            _ => Enumerable.Empty<GameObject>()
        };

        internal static void ValidateData(string kind, JObject data)
        {
            var fields = kind switch
            {
                nameof(ModularAvatarObjectToggle) => new[] { Inverted, "m_objects" },
                nameof(ModularAvatarShapeChanger) => new[] { Inverted, "m_shapes", "m_threshold" },
                nameof(ModularAvatarMaterialSetter) => new[] { Inverted, "m_objects" },
                nameof(ModularAvatarMaterialSwap) => new[] { Inverted, "m_root", "m_swaps" },
                nameof(ModularAvatarMeshCutter) => new[] { Inverted, "m_object", "m_multiMode" },
                nameof(ModularAvatarReactiveMove) => new[] { Inverted, "m_toMove", "m_whereTo", "m_fixToWorld", "m_transitionTime", "m_setPosition", "m_setRotation", "m_setScale" },
                nameof(ModularAvatarBlendshapeSync) => new[] { "Bindings" },
                _ => throw new ArgumentException($"Unsupported source component kind '{kind}'")
            };
            RejectUnknown(data, fields, kind);
            foreach (var field in fields) if (data[field] == null) throw new ArgumentException($"Missing {kind}.{field}");
            switch (kind)
            {
                case nameof(ModularAvatarObjectToggle):
                    RequireBoolean(data, Inverted);
                    foreach (var entry in Entries(data, "m_objects", "ModularAvatarObjectToggle.m_objects", "Object", "Active"))
                    {
                        ValidateAvatarReference(RequiredObject(entry["Object"], "Object"));
                        RequireBoolean(entry, "Active");
                    }
                    break;
                case nameof(ModularAvatarShapeChanger):
                    RequireBoolean(data, Inverted);
                    RequireFinite(data, "m_threshold");
                    foreach (var entry in Entries(data, "m_shapes", "ModularAvatarShapeChanger.m_shapes", "Object", "ShapeName", "ChangeType", "Value"))
                    {
                        ValidateAvatarReference(RequiredObject(entry["Object"], "Object"));
                        _ = RequiredString(entry, "ShapeName");
                        RequireEnum<ShapeChangeType>(entry, "ChangeType");
                        RequireFinite(entry, "Value");
                    }
                    break;
                case nameof(ModularAvatarMaterialSetter):
                    RequireBoolean(data, Inverted);
                    foreach (var entry in Entries(data, "m_objects", "ModularAvatarMaterialSetter.m_objects", "Object", "Material", "MaterialIndex"))
                    {
                        ValidateAvatarReference(RequiredObject(entry["Object"], "Object"));
                        ValidateReference(entry["Material"]);
                        _ = RequiredInt(entry, "MaterialIndex");
                    }
                    break;
                case nameof(ModularAvatarMaterialSwap):
                    RequireBoolean(data, Inverted);
                    ValidateAvatarReference(RequiredObject(data["m_root"], "m_root"));
                    foreach (var entry in Entries(data, "m_swaps", "ModularAvatarMaterialSwap.m_swaps", "From", "To"))
                    {
                        ValidateReference(entry["From"]);
                        ValidateReference(entry["To"]);
                    }
                    break;
                case nameof(ModularAvatarMeshCutter):
                    RequireBoolean(data, Inverted);
                    ValidateAvatarReference(RequiredObject(data["m_object"], "m_object"));
                    RequireEnum<MeshCutterMultiMode>(data, "m_multiMode");
                    break;
                case nameof(ModularAvatarReactiveMove):
                    RequireBoolean(data, Inverted);
                    ValidateAvatarReference(RequiredObject(data["m_toMove"], "m_toMove"));
                    ValidateAvatarReference(RequiredObject(data["m_whereTo"], "m_whereTo"));
                    RequireBoolean(data, "m_fixToWorld");
                    RequireFinite(data, "m_transitionTime");
                    RequireBoolean(data, "m_setPosition");
                    RequireBoolean(data, "m_setRotation");
                    RequireBoolean(data, "m_setScale");
                    break;
                case nameof(ModularAvatarBlendshapeSync):
                    foreach (var entry in Entries(data, "Bindings", "ModularAvatarBlendshapeSync.Bindings", "ReferenceMesh", "Blendshape", "LocalBlendshape"))
                    {
                        ValidateAvatarReference(RequiredObject(entry["ReferenceMesh"], "ReferenceMesh"));
                        _ = RequiredString(entry, "Blendshape");
                        _ = RequiredString(entry, "LocalBlendshape");
                    }
                    break;
            }
        }

        internal static void ApplyData(Component component, JObject data, Func<JObject, UnityEngine.Object?> resolveReference)
        {
            var kind = component.GetType().Name;
            ValidateData(kind, data);
            var serialized = new SerializedObject(component);
            if (component is ReactiveComponent) SetBool(serialized, Inverted, data[Inverted]!);
            switch (component)
            {
                case ModularAvatarObjectToggle:
                    SetArray(serialized, "m_objects", (JArray)data["m_objects"]!, (property, value) =>
                    {
                        var entry = RequiredObject(value, "ModularAvatarObjectToggle.m_objects");
                        RejectUnknown(entry, new[] { "Object", "Active" }, "ModularAvatarObjectToggle.m_objects");
                        SetAvatarReference(Required(property, "Object"), RequiredObject(entry["Object"], "Object"), resolveReference);
                        SetBool(property, "Active", entry["Active"]!);
                    });
                    break;
                case ModularAvatarShapeChanger:
                    SetArray(serialized, "m_shapes", (JArray)data["m_shapes"]!, (property, value) =>
                    {
                        var entry = RequiredObject(value, "ModularAvatarShapeChanger.m_shapes");
                        RejectUnknown(entry, new[] { "Object", "ShapeName", "ChangeType", "Value" }, "ModularAvatarShapeChanger.m_shapes");
                        SetAvatarReference(Required(property, "Object"), RequiredObject(entry["Object"], "Object"), resolveReference);
                        Required(property, "ShapeName").stringValue = RequiredString(entry, "ShapeName");
                        SetEnum(Required(property, "ChangeType"), RequiredString(entry, "ChangeType"));
                        Required(property, "Value").floatValue = ReactiveDebugJson.ReadFiniteFloat(entry["Value"]!);
                    });
                    Required(serialized, "m_threshold").floatValue = ReactiveDebugJson.ReadFiniteFloat(data["m_threshold"]!);
                    break;
                case ModularAvatarMaterialSetter:
                    SetArray(serialized, "m_objects", (JArray)data["m_objects"]!, (property, value) =>
                    {
                        var entry = RequiredObject(value, "ModularAvatarMaterialSetter.m_objects");
                        RejectUnknown(entry, new[] { "Object", "Material", "MaterialIndex" }, "ModularAvatarMaterialSetter.m_objects");
                        SetAvatarReference(Required(property, "Object"), RequiredObject(entry["Object"], "Object"), resolveReference);
                        Required(property, "Material").objectReferenceValue = Resolve(entry["Material"], resolveReference);
                        Required(property, "MaterialIndex").intValue = RequiredInt(entry, "MaterialIndex");
                    });
                    break;
                case ModularAvatarMaterialSwap:
                    SetAvatarReference(Required(serialized, "m_root"), RequiredObject(data["m_root"], "m_root"), resolveReference);
                    SetArray(serialized, "m_swaps", (JArray)data["m_swaps"]!, (property, value) =>
                    {
                        var entry = RequiredObject(value, "ModularAvatarMaterialSwap.m_swaps");
                        RejectUnknown(entry, new[] { "From", "To" }, "ModularAvatarMaterialSwap.m_swaps");
                        Required(property, "From").objectReferenceValue = Resolve(entry["From"], resolveReference);
                        Required(property, "To").objectReferenceValue = Resolve(entry["To"], resolveReference);
                    });
                    break;
                case ModularAvatarMeshCutter:
                    SetAvatarReference(Required(serialized, "m_object"), RequiredObject(data["m_object"], "m_object"), resolveReference);
                    SetEnum(Required(serialized, "m_multiMode"), RequiredString(data, "m_multiMode"));
                    break;
                case ModularAvatarReactiveMove:
                    SetAvatarReference(Required(serialized, "m_toMove"), RequiredObject(data["m_toMove"], "m_toMove"), resolveReference);
                    SetAvatarReference(Required(serialized, "m_whereTo"), RequiredObject(data["m_whereTo"], "m_whereTo"), resolveReference);
                    SetBool(serialized, "m_fixToWorld", data["m_fixToWorld"]!);
                    Required(serialized, "m_transitionTime").floatValue = ReactiveDebugJson.ReadFiniteFloat(data["m_transitionTime"]!);
                    SetBool(serialized, "m_setPosition", data["m_setPosition"]!);
                    SetBool(serialized, "m_setRotation", data["m_setRotation"]!);
                    SetBool(serialized, "m_setScale", data["m_setScale"]!);
                    break;
                case ModularAvatarBlendshapeSync:
                    SetArray(serialized, "Bindings", (JArray)data["Bindings"]!, (property, value) =>
                    {
                        var entry = RequiredObject(value, "ModularAvatarBlendshapeSync.Bindings");
                        RejectUnknown(entry, new[] { "ReferenceMesh", "Blendshape", "LocalBlendshape" }, "ModularAvatarBlendshapeSync.Bindings");
                        SetAvatarReference(Required(property, "ReferenceMesh"), RequiredObject(entry["ReferenceMesh"], "ReferenceMesh"), resolveReference);
                        Required(property, "Blendshape").stringValue = RequiredString(entry, "Blendshape");
                        Required(property, "LocalBlendshape").stringValue = RequiredString(entry, "LocalBlendshape");
                    });
                    break;
                default: throw new ArgumentException($"Unsupported source component kind '{kind}'");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }


        private static IEnumerable<GameObject> Resolve(AvatarObjectReference? reference, Component owner)
        {
            var target = reference?.Get(owner);
            return target == null ? Enumerable.Empty<GameObject>() : new[] { target };
        }

        private static JObject AvatarReference(AvatarObjectReference? reference, Component owner, ReactiveDebugJson json) => new()
        {
            ["path"] = reference?.referencePath ?? "",
            ["target"] = json.Reference(reference?.Get(owner))
        };

        private static bool Bool(SerializedObject parent, string name) => Bool(Required(parent, name));
        private static bool Bool(SerializedProperty parent, string name) => Bool(Required(parent, name));
        private static bool Bool(SerializedProperty property) => property.boolValue;
        private static int Integer(SerializedProperty parent, string name) => Required(parent, name).intValue;
        private static float Float(SerializedObject parent, string name) => Float(Required(parent, name));
        private static float Float(SerializedProperty parent, string name) => Float(Required(parent, name));
        private static float Float(SerializedProperty property) => property.floatValue;
        private static string String(SerializedProperty parent, string name) => Required(parent, name).stringValue;
        private static string Enum(SerializedObject parent, string name) => Enum(Required(parent, name));
        private static string Enum(SerializedProperty parent, string name) => Enum(Required(parent, name));
        private static string Enum(SerializedProperty property) => property.enumNames[property.enumValueIndex];
        private static UnityEngine.Object? Object(SerializedProperty parent, string name) => Required(parent, name).objectReferenceValue;
        private static SerializedProperty Required(SerializedObject parent, string name) => parent.FindProperty(name) ?? throw new ArgumentException($"Missing schema property {name}");
        private static SerializedProperty Required(SerializedProperty parent, string name) => parent.FindPropertyRelative(name) ?? throw new ArgumentException($"Missing schema property {parent.propertyPath}.{name}");
        private static void RejectUnknown(JObject data, IEnumerable<string> allowed, string kind)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (var property in data.Properties()) if (!set.Contains(property.Name)) throw new ArgumentException($"Unknown {kind} field '{property.Name}'");
        }
        private static IEnumerable<JObject> Entries(JObject data, string field, string kind, params string[] fields)
        {
            if (data[field] is not JArray entries) throw new ArgumentException($"Expected array {kind}");
            foreach (var token in entries)
            {
                var entry = RequiredObject(token, kind);
                RejectUnknown(entry, fields, kind);
                foreach (var required in fields) if (entry[required] == null) throw new ArgumentException($"Missing {kind}.{required}");
                yield return entry;
            }
        }

        private static void RequireBoolean(JObject data, string field)
        {
            if (data[field]?.Type != JTokenType.Boolean) throw new ArgumentException($"Expected boolean {field}");
        }

        private static void RequireFinite(JObject data, string field)
        {
            if (data[field] == null) throw new ArgumentException($"Missing numeric {field}");
            _ = ReactiveDebugJson.ReadFiniteFloat(data[field]!);
        }

        private static void RequireEnum<T>(JObject data, string field) where T : struct, Enum
        {
            if (!System.Enum.TryParse<T>(RequiredString(data, field), false, out var value)
                || !System.Enum.IsDefined(typeof(T), value))
                throw new ArgumentException($"Invalid {typeof(T).Name} {field}");
        }

        private static void ValidateReference(JToken? value)
        {
            if (value == null || value.Type == JTokenType.Null) return;
            var reference = RequiredObject(value, "reference");
            RejectUnknown(reference, new[] { "kind", "id" }, "reference");
            if (reference["kind"]?.Type != JTokenType.String || reference["id"]?.Type != JTokenType.Integer)
                throw new ArgumentException("Invalid reference");
        }
        private static void ValidateAvatarReference(JObject reference)
        {
            RejectUnknown(reference, new[] { "path", "target" }, "AvatarObjectReference");
            _ = RequiredString(reference, "path");
            if (reference["target"] is JObject target)
            {
                RejectUnknown(target, new[] { "kind", "id" }, "reference target");
                if (target["kind"]?.Type != JTokenType.String || target["id"]?.Type != JTokenType.Integer)
                    throw new ArgumentException("Invalid AvatarObjectReference target");
            }
            else if (reference["target"]?.Type != JTokenType.Null) throw new ArgumentException("Invalid AvatarObjectReference target");
        }

        private static void SetArray(SerializedObject parent, string name, JArray values, Action<SerializedProperty, JToken> apply)
        {
            var property = Required(parent, name);
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) apply(property.GetArrayElementAtIndex(i), values[i]);
        }

        private static void SetAvatarReference(SerializedProperty property, JObject value, Func<JObject, UnityEngine.Object?> resolveReference)
        {
            RejectUnknown(value, new[] { "path", "target" }, "AvatarObjectReference");
            property.FindPropertyRelative(nameof(AvatarObjectReference.referencePath))!.stringValue = RequiredString(value, "path");
            property.FindPropertyRelative("targetObject")!.objectReferenceValue = Resolve(value["target"], resolveReference);
        }

        private static UnityEngine.Object? Resolve(JToken? value, Func<JObject, UnityEngine.Object?> resolveReference)
        {
            if (value == null || value.Type == JTokenType.Null) return null;
            return resolveReference(RequiredObject(value, "reference"));
        }

        private static void SetBool(SerializedObject parent, string name, JToken value) => SetBool(Required(parent, name), value);
        private static void SetBool(SerializedProperty parent, string name, JToken value) => SetBool(Required(parent, name), value);
        private static void SetBool(SerializedProperty property, JToken value)
        {
            if (value.Type != JTokenType.Boolean) throw new ArgumentException($"Expected boolean for {property.propertyPath}");
            property.boolValue = value.Value<bool>();
        }

        private static void SetEnum(SerializedProperty property, string value)
        {
            var index = System.Array.IndexOf(property.enumNames, value);
            if (index < 0) throw new ArgumentException($"Invalid enum value '{value}' for {property.propertyPath}");
            property.enumValueIndex = index;
        }

        private static JObject RequiredObject(JToken? value, string name) =>
            value as JObject ?? throw new ArgumentException($"Expected object for {name}");
        private static string RequiredString(JObject value, string name) => RequiredString(value[name], name);
        private static string RequiredString(JToken? value, string name)
        {
            if (value?.Type != JTokenType.String) throw new ArgumentException($"Expected string for {name}");
            return value.Value<string>()!;
        }
        private static int RequiredInt(JObject value, string name)
        {
            if (value[name]?.Type != JTokenType.Integer) throw new ArgumentException($"Expected integer for {name}");
            return value.Value<int>(name);
        }
    }
}
