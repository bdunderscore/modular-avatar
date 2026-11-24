#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;


namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class ReactiveDebugGraphSnapshot
    {

        private const string MaterialSlotPropertyPrefix = "m_Materials.Array.data[";
        private const string MaterialSlotPropertySuffix = "]";

        private static readonly HashSet<string> VrcConstraintTypes = new()
        {
            "VRC.SDK3.Dynamics.Constraint.Components.VRCPositionConstraint",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCRotationConstraint",
            "VRC.SDK3.Dynamics.Constraint.Components.VRCScaleConstraint",
        };
        private static readonly FieldInfo ParameterCounter = typeof(ReactionParameters)
            .GetField("_counter", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ReactionParameters).FullName, "_counter");

        internal static JObject SerializeGraph(ReactionGraph graph, ReactiveDebugJson json)
        {
            var defaults = new JArray(graph.Parameters.ParameterDefaults.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new JObject { ["name"] = pair.Key, ["value"] = json.FiniteFloat(pair.Value) }));
            return new JObject
            {
                ["parameterDefaults"] = defaults,
                ["parameterCounter"] = (int)ParameterCounter.GetValue(graph.Parameters)!,
                ["nodes"] = new JArray(graph.Nodes.Select(node => SerializeNode(node, json))),
            };
        }

        internal static JObject SerializeEffectGroup(EffectGroup group, VirtualMotion? motion, ReactiveDebugJson json,
            ref int nextMotionId)
        {
            JToken serializedMotion;
            if (motion == null)
            {
                serializedMotion = JValue.CreateNull();
            }
            else
            {
                var snapshot = new MotionSnapshot(json, nextMotionId);
                serializedMotion = snapshot.Serialize(motion);
                nextMotionId = snapshot.NextId;
            }

            return new JObject
            {
                ["targetKey"] = SerializeTargetKey(group.TargetKey, json),
                ["nodes"] = new JArray(group.Nodes.Select(node => SerializeNode(node, json))),
                ["processor"] = group.Processor.GetType().FullName,
                ["latency"] = group.Latency,
                ["depth"] = group.Depth == null ? JValue.CreateNull() : group.Depth.Value,
                ["alignedGraphLatency"] = group.AlignedGraphLatency,
                ["defaultNode"] = group.DefaultNode == null ? JValue.CreateNull() : group.DefaultNode.Value,
                ["motion"] = serializedMotion,
            };
        }

        private static JObject SerializeNode(ReactionNode node, ReactiveDebugJson json)
        {
            return new JObject
            {
                ["priority"] = node.Priority,
                ["expression"] = SerializeExpression(node.Expression, json),
                ["effects"] = new JArray(node.Effects.Select(effect => SerializeAction(effect, json))),
            };
        }

        private static JObject SerializeExpression(IExpression expression, ReactiveDebugJson json)
        {
            return expression switch
            {
                Constant constant => new JObject { ["kind"] = "Constant", ["value"] = constant.Value },
                ParameterExpression parameter => new JObject
                {
                    ["kind"] = "ParameterExpression", ["parameterName"] = parameter.ParameterName,
                    ["mode"] = parameter.Mode.ToString(), ["threshold"] = json.FiniteFloat(parameter.Threshold)
                },
                InternalParameterCondition parameter => new JObject
                {
                    ["kind"] = "InternalParameterCondition", ["parameterName"] = parameter.ParameterName
                },
                ObjectActiveState state => new JObject
                {
                    ["kind"] = "ObjectActiveState", ["objectId"] = json.ObjectId(state.TargetObject),
                    ["stateMode"] = state.StateMode.ToString()
                },
                AndNode and => new JObject
                {
                    ["kind"] = "AndNode", ["children"] = new JArray(and.Children.Select(child => SerializeExpression(child, json)))
                },
                OrNode or => new JObject
                {
                    ["kind"] = "OrNode", ["children"] = new JArray(or.Children.Select(child => SerializeExpression(child, json)))
                },
                NotNode not => new JObject { ["kind"] = "NotNode", ["inner"] = SerializeExpression(not.Inner, json) },
                _ => throw new NotSupportedException($"Unsupported reactive expression variant {expression.GetType().FullName}")
            };
        }

        private static JObject SerializeAction(IAction action, ReactiveDebugJson json)
        {
            JObject Result(string kind) => new() { ["kind"] = kind, ["targetKey"] = SerializeTargetKey(action.TargetKey, json) };
            switch (action)
            {
                case DriveActiveState active:
                {
                    var value = Result("DriveActiveState"); value["objectId"] = json.ObjectId(active.Target); value["active"] = active.Active; return value;
                }
                case DriveParameter parameter:
                {
                    var value = Result("DriveParameter"); value["parameterName"] = parameter.ParameterName; value["value"] = json.FiniteFloat(parameter.Value); return value;
                }
                case DriveInternalParameter parameter:
                {
                    var value = Result("DriveInternalParameter"); value["parameterName"] = parameter.ParameterName; value["state"] = parameter.State; return value;
                }
                case FloatPropAction property:
                {
                    var value = Result("FloatPropAction"); value["propertyTarget"] = SerializeFloatPropertyTarget(property.Prop, json); value["value"] = json.FiniteFloat(property.Value); return value;
                }
                case ObjectPropAction property:
                {
                    var value = Result("ObjectPropAction"); value["propertyTarget"] = SerializeObjectPropertyTarget(property.Prop, property.Value, json); value["value"] = property.Value == null ? JValue.CreateNull() : json.Reference(property.Value); return value;
                }
                case SetShapeKey shape:
                {
                    var value = Result("SetShapeKey"); value["rendererId"] = json.ObjectId(shape.Renderer); value["shapeName"] = shape.ShapeName; value["value"] = json.FiniteFloat(shape.Value); return value;
                }
                case SetMaterial material:
                {
                    var value = Result("SetMaterial"); value["rendererId"] = json.ObjectId(material.Renderer); value["slot"] = material.MaterialIndex; value["material"] = json.Reference(material.Material); return value;
                }
                case HideMeshSection hide:
                {
                    var value = Result("HideMeshSection"); value["meshSectionTarget"] = SerializeTargetKey(hide.Target, json); value["shouldHide"] = hide.ShouldHide; return value;
                }
                case ReactiveMoveAction move:
                {
                    var value = Result("ReactiveMoveAction"); value["toMoveId"] = json.ObjectId(move.ToMove); value["destinationId"] = move.WhereTo == null ? JValue.CreateNull() : json.ObjectId(move.WhereTo); value["fixedToWorld"] = move.FixToWorld; value["channel"] = move.Channel.ToString(); value["transitionTime"] = json.FiniteFloat(move.TransitionTime); return value;
                }
                case DeltaTimeControlAction delta:
                {
                    ValidateDeltaTimeControlAction(delta);
                    var value = Result("DeltaTimeControlAction"); value["objectId"] = json.ObjectId(delta.TargetObject); value["duration"] = json.FiniteFloat(delta.TransitionDuration); value["selectedProperty"] = delta.SelectedProp == null ? JValue.CreateNull() : delta.SelectedProp; value["antiSelectedProperty"] = delta.AntiSelectedProp;
                    value["constantProperties"] = new JObject(delta.ConstProps.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new JProperty(pair.Key, json.FiniteFloat(pair.Value)))); return value;
                }
                case AlreadyApplied applied:
                {
                    var value = Result("AlreadyApplied"); value["inner"] = SerializeAction(applied.Inner, json); return value;
                }
                case TargetKeyOverride overridden:
                {
                    var value = Result("TargetKeyOverride"); value["actions"] = new JArray(overridden.Actions.Select(inner => SerializeAction(inner, json))); return value;
                }
                case NullAction:
                    return Result("NullAction");
                default:
                    throw new NotSupportedException($"Unsupported reactive action variant {action.GetType().FullName}");
            }
        }

        private static JToken SerializeTargetKey(object? targetKey, ReactiveDebugJson json)
        {
            return targetKey switch
            {
                null => JValue.CreateNull(),
                ObjectActiveTarget active => new JObject { ["kind"] = "ObjectActiveTarget", ["objectId"] = json.ObjectId(active.Target) },
                ParameterTarget parameter => new JObject { ["kind"] = "ParameterTarget", ["parameterName"] = parameter.ParameterName },
                InternalParameterTarget parameter => new JObject { ["kind"] = "InternalParameterTarget", ["parameterName"] = parameter.ParameterName },
                PropertyTarget property => SerializePropertyTarget(property, json),
                ShapeKeyTarget shape => new JObject { ["kind"] = "ShapeKeyTarget", ["rendererId"] = json.ObjectId(shape.Renderer), ["shapeName"] = shape.ShapeName },
                MaterialSlotTarget material => new JObject { ["kind"] = "MaterialSlotTarget", ["rendererId"] = json.ObjectId(material.Renderer), ["slot"] = material.MaterialIndex },
                ReactiveMoveTarget move => new JObject { ["kind"] = "ReactiveMoveTarget", ["transformId"] = json.ObjectId(move.ToMove), ["channel"] = move.Channel.ToString() },
                MeshSectionTarget section => SerializeMeshSectionTarget(section, json),
                DeltaTimeControlAction.Target delta => new JObject { ["kind"] = "DeltaTimeTarget", ["objectId"] = json.ObjectId(delta.TargetObject), ["antiSelectedProperty"] = delta.AntiSelectedProp },
                _ => throw new NotSupportedException($"Unsupported reactive target key variant {targetKey.GetType().FullName}")
            };
        }

        private static JObject SerializePropertyTarget(PropertyTarget property, ReactiveDebugJson json)
        {
            if (!IsKnownPropertyTarget(property))
                throw new NotSupportedException($"Unsupported reactive property target {property.TargetObject?.GetType().FullName}.{property.PropertyName}");

            return new JObject
            {
                ["kind"] = "PropertyTarget",
                ["target"] = json.Reference(property.TargetObject),
                ["targetType"] = property.TargetObject.GetType().FullName,
                ["propertyName"] = property.PropertyName,
            };
        }

        private static JObject SerializeFloatPropertyTarget(PropertyTarget property, ReactiveDebugJson json)
        {
            if (!IsKnownFloatPropertyTarget(property))
                throw new NotSupportedException($"Unsupported reactive float property target {property.TargetObject?.GetType().FullName}.{property.PropertyName}");

            return SerializePropertyTarget(property, json);
        }

        private static bool IsKnownFloatPropertyTarget(PropertyTarget property)
        {
            var target = property.TargetObject;
            if (target == null) return false;

            if (target is GameObject) return property.PropertyName == "m_IsActive";
            if (target is SkinnedMeshRenderer)
                return property.PropertyName == "m_UpdateWhenOffscreen" ||
                       IsBlendShapeProperty(property.PropertyName);
            if (target is Transform) return property.PropertyName is "m_LocalScale.x" or "m_LocalScale.y" or "m_LocalScale.z";
            if (target is AudioSource) return property.PropertyName == "m_Enabled";

            return IsVrcConstraintType(target.GetType()) && IsConstraintProperty(property.PropertyName);
        }

        private static void ValidateDeltaTimeControlAction(DeltaTimeControlAction action)
        {
            if (action.TargetObject is not Component target || !IsVrcConstraintType(target.GetType()) ||
                !IsConstraintProperty(action.AntiSelectedProp) ||
                (action.SelectedProp != null && !IsConstraintProperty(action.SelectedProp)) ||
                action.ConstProps.Keys.Any(property => !IsConstraintProperty(property)))
                throw new NotSupportedException($"Unsupported reactive delta-time target {action.TargetObject?.GetType().FullName}");
        }

        private static JObject SerializeObjectPropertyTarget(PropertyTarget property, Object? value, ReactiveDebugJson json)
        {
            if (property.TargetObject is not Renderer || !IsMaterialSlotProperty(property.PropertyName))
                throw new NotSupportedException($"Unsupported reactive object property target {property.TargetObject?.GetType().FullName}.{property.PropertyName}");
            if (value != null && value is not Material)
                throw new NotSupportedException($"Unsupported reactive object property value {value.GetType().FullName}");

            return SerializePropertyTarget(property, json);
        }

        private static bool IsKnownPropertyTarget(PropertyTarget property) =>
            IsKnownFloatPropertyTarget(property) ||
            property.TargetObject is Renderer && IsMaterialSlotProperty(property.PropertyName);

        private static bool IsBlendShapeProperty(string propertyName) =>
            propertyName.StartsWith("blendShape.", StringComparison.Ordinal) &&
            propertyName.Length > "blendShape.".Length;

        private static bool IsMaterialSlotProperty(string propertyName) =>
            IsIndexedProperty(propertyName, MaterialSlotPropertyPrefix, MaterialSlotPropertySuffix);

        private static bool IsConstraintProperty(string propertyName) =>
            propertyName is "IsActive" or "GlobalWeight" or "FreezeToWorld" ||
            IsIndexedProperty(propertyName, "Sources.source", ".Weight") ||
            IsIndexedProperty(propertyName, "Sources.overflowList.Array.data[", "].Weight");

        private static bool IsIndexedProperty(string propertyName, string prefix, string suffix)
        {
            if (!propertyName.StartsWith(prefix, StringComparison.Ordinal) ||
                !propertyName.EndsWith(suffix, StringComparison.Ordinal)) return false;

            var indexStart = prefix.Length;
            var indexLength = propertyName.Length - indexStart - suffix.Length;
            if (indexLength <= 0) return false;
            for (var i = indexStart; i < indexStart + indexLength; i++)
            {
                if (!char.IsDigit(propertyName[i])) return false;
            }

            return true;
        }

        private static bool IsVrcConstraintType(Type type) =>
            type.FullName != null && VrcConstraintTypes.Contains(type.FullName);

        private static void ValidateFloatCurveBinding(EditorCurveBinding binding)
        {
            var type = binding.type;
            var property = binding.propertyName;
            var allowed = !binding.isPPtrCurve && (
                type == typeof(Animator) && !string.IsNullOrEmpty(property) ||
                type == typeof(GameObject) && property == "m_IsActive" ||
                type == typeof(SkinnedMeshRenderer) &&
                (property == "m_UpdateWhenOffscreen" || IsBlendShapeProperty(property)) ||
                type == typeof(Transform) && property is "m_LocalScale.x" or "m_LocalScale.y" or "m_LocalScale.z" ||
                type == typeof(AudioSource) && property == "m_Enabled" ||
                IsVrcConstraintType(type) && IsConstraintProperty(property)
            );
            if (!allowed)
                throw new NotSupportedException($"Unsupported generated float curve binding {type?.FullName}.{property}");
        }

        private static JObject SerializeMeshSectionTarget(MeshSectionTarget target, ReactiveDebugJson json)
        {
            var value = new JObject { ["kind"] = "MeshSectionTarget", ["rendererId"] = json.ObjectId(target.Renderer) };
            if (target.IsShape)
            {
                value["sectionKind"] = "Shape";
                value["shapeName"] = target.ShapeName;
            }
            else
            {
                value["sectionKind"] = "Mask";
                value["selectorPartitionId"] = json.SelectorPartitionId(target.Selector
                    ?? throw new InvalidOperationException("Mesh section mask target has no selector"));
            }
            return value;
        }

        private sealed class MotionSnapshot
        {
            private readonly ReactiveDebugJson _json;
            private readonly Dictionary<VirtualMotion, int> _ids = new(ReferenceEqualityComparer.Instance);
            private int _nextId;

            internal int NextId => _nextId;

            internal MotionSnapshot(ReactiveDebugJson json, int firstId)
            {
                _json = json;
                _nextId = firstId;
            }

            internal JToken Serialize(VirtualMotion motion)
            {
                if (_ids.TryGetValue(motion, out var id)) return new JObject { ["ref"] = id };
                id = _nextId++;
                _ids[motion] = id;
                return motion switch
                {
                    VirtualBlendTree tree => SerializeBlendTree(tree, id),
                    VirtualClip clip => SerializeClip(clip, id),
                    _ => throw new NotSupportedException($"Unsupported virtual motion variant {motion.GetType().FullName}")
                };
            }

            private JObject SerializeBlendTree(VirtualBlendTree tree, int id)
            {
                return new JObject
                {
                    ["id"] = id, ["kind"] = "VirtualBlendTree", ["name"] = tree.Name,
                    ["blendType"] = tree.BlendType.ToString(), ["blendParameter"] = tree.BlendParameter,
                    ["blendParameterY"] = tree.BlendParameterY, ["minThreshold"] = _json.FiniteFloat(tree.MinThreshold),
                    ["maxThreshold"] = _json.FiniteFloat(tree.MaxThreshold), ["useAutomaticThresholds"] = tree.UseAutomaticThresholds,
                    ["normalizedBlendValues"] = tree.NormalizedBlendValues,
                    ["children"] = new JArray(tree.Children.Select(child => new JObject
                    {
                        ["motion"] = child.Motion == null ? JValue.CreateNull() : Serialize(child.Motion),
                        ["threshold"] = _json.FiniteFloat(child.Threshold), ["directBlendParameter"] = child.DirectBlendParameter,
                        ["position"] = new JArray(_json.FiniteFloat(child.Position.x), _json.FiniteFloat(child.Position.y)),
                        ["timeScale"] = _json.FiniteFloat(child.TimeScale), ["cycleOffset"] = _json.FiniteFloat(child.CycleOffset), ["mirror"] = child.Mirror
                    }))
                };
            }

            private JObject SerializeClip(VirtualClip clip, int id)
            {
                return new JObject
                {
                    ["id"] = id, ["kind"] = "VirtualClip", ["name"] = clip.Name, ["frameRate"] = _json.FiniteFloat(clip.FrameRate),
                    ["wrapMode"] = clip.WrapMode.ToString(), ["legacy"] = clip.Legacy,
                    ["floatCurves"] = new JArray(clip.GetFloatCurveBindings().Select(binding => SerializeFloatBinding(binding, clip)))
                };
            }

            private JObject SerializeFloatBinding(EditorCurveBinding binding, VirtualClip clip)
            {
                ValidateFloatCurveBinding(binding);
                return new JObject
                {
                    ["path"] = binding.path, ["type"] = binding.type.FullName, ["propertyName"] = binding.propertyName,
                    ["isDiscreteCurve"] = binding.isDiscreteCurve, ["isPPtrCurve"] = binding.isPPtrCurve,
                    ["isSerializeReferenceCurve"] = binding.isSerializeReferenceCurve,
                    ["curve"] = _json.SerializeFloatCurve(clip.GetFloatCurve(binding))
                };
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<VirtualMotion>
        {
            internal static readonly ReferenceEqualityComparer Instance = new();

            public bool Equals(VirtualMotion? left, VirtualMotion? right) => ReferenceEquals(left, right);

            public int GetHashCode(VirtualMotion value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
