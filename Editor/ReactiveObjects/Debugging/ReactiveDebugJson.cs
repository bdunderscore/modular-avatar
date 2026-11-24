#nullable enable

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>Shared, explicit JSON representation for reactive-component diagnostics.</summary>
    internal sealed class ReactiveDebugJson
    {
        private readonly Dictionary<UnityEngine.Object, int> _objectIds = new();
        private readonly Dictionary<UnityEngine.Object, int> _assetIds = new();
        private readonly List<(IMeshSelector Selector, int Id)> _selectorIds = new();
        private int _nextId = 1;

        internal readonly JArray Objects = new();
        internal readonly JArray Components = new();
        internal readonly JArray Assets = new();
        internal readonly JArray SelectorPartitions = new();

        internal string Platform { get; set; } = "Generic";

        internal int ObjectId(UnityEngine.Object obj)
        {
            if (obj == null) return 0;
            if (obj is not GameObject && obj is not Component) return AssetId(obj)!.Value;
            if (_objectIds.TryGetValue(obj, out var id)) return id;

            id = _nextId++;
            _objectIds[obj] = id;
            switch (obj)
            {
                case GameObject go:
                    Objects.Add(new JObject
                    {
                        ["id"] = id, ["source"] = false, ["name"] = go.name, ["type"] = "GameObject"
                    });
                    break;
                case Component component when IsSupportedIdentityComponent(component):
                    Components.Add(new JObject
                    {
                        ["id"] = id, ["source"] = false, ["objectId"] = ObjectId(component.gameObject),
                        ["type"] = component.GetType().Name, ["enabled"] = IsEnabled(component)
                    });
                    break;
                case Component component:
                    throw new ArgumentException($"Unsupported debug component identity {component.GetType().Name}", nameof(obj));
                default: throw new ArgumentException($"Unsupported debug identity {obj.GetType().Name}", nameof(obj));
            }
            return id;
        }

        internal int? AssetId(UnityEngine.Object? obj)
        {
            if (obj == null) return null;
            if (obj is not Material && obj is not Mesh)
                throw new ArgumentException($"Unsupported debug asset identity {obj.GetType().Name}", nameof(obj));
            if (_assetIds.TryGetValue(obj, out var id)) return id;
            id = _nextId++;
            _assetIds[obj] = id;
            Assets.Add(new JObject { ["id"] = id, ["source"] = false, ["kind"] = obj is Material ? "Material" : "Mesh" });
            return id;
        }

        internal int SelectorPartitionId(IMeshSelector selector)
        {
            foreach (var (existing, id) in _selectorIds)
                if (existing.Equals(selector)) return id;

            var partitionId = _selectorIds.Count + 1;
            _selectorIds.Add((selector, partitionId));
            SelectorPartitions.Add(new JObject { ["id"] = partitionId, ["kind"] = "opaque" });
            return partitionId;
        }

        internal JToken Reference(UnityEngine.Object? obj)
        {
            if (obj == null) return JValue.CreateNull();
            if (obj is Material material)
                obj = ObjectRegistry.GetReference(material)?.Object as Material ?? material;
            return new JObject
            {
                ["kind"] = obj is GameObject ? "object" : obj is Component ? "component" : "asset",
                ["id"] = obj is GameObject or Component ? ObjectId(obj) : AssetId(obj)
            };
        }

        internal JToken FiniteFloat(float value) => FiniteFloat((double)value);
        internal JToken FiniteFloat(double value)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsPositiveInfinity(value)) return "Infinity";
            if (double.IsNegativeInfinity(value)) return "-Infinity";
            return value;
        }

        internal static float ReadFiniteFloat(JToken token)
        {
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) return token.Value<float>();
            return token.Value<string>() switch
            {
                "NaN" => float.NaN,
                "Infinity" => float.PositiveInfinity,
                "-Infinity" => float.NegativeInfinity,
                _ => throw new ArgumentException("Invalid finite-float value", nameof(token))
            };
        }

        internal JObject SerializeFloatCurve(AnimationCurve? curve)
        {
            if (curve == null) return new JObject { ["null"] = true };
            var keys = new JArray();
            foreach (var key in curve.keys)
            {
                keys.Add(new JObject
                {
                    ["time"] = FiniteFloat(key.time), ["value"] = FiniteFloat(key.value),
                    ["inTangent"] = FiniteFloat(key.inTangent), ["outTangent"] = FiniteFloat(key.outTangent),
                    ["inWeight"] = FiniteFloat(key.inWeight), ["outWeight"] = FiniteFloat(key.outWeight),
                    ["weightedMode"] = (int)key.weightedMode
                });
            }
            return new JObject { ["keys"] = keys, ["preWrapMode"] = (int)curve.preWrapMode, ["postWrapMode"] = (int)curve.postWrapMode };
        }

        private static bool IsEnabled(Component component) => component is Behaviour b ? b.enabled : component is Renderer r ? r.enabled : true;

        private static bool IsSupportedIdentityComponent(Component component) =>
            component is Transform or Animator or Renderer or AudioSource
            or ModularAvatarObjectToggle or ModularAvatarShapeChanger or ModularAvatarMaterialSetter
            or ModularAvatarMaterialSwap or ModularAvatarMeshCutter or ModularAvatarReactiveMove
            or ModularAvatarBlendshapeSync or ModularAvatarMenuItem
            || component.GetType().FullName is
                "VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint" or
                "VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint" or
                "VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint";
    }
}
