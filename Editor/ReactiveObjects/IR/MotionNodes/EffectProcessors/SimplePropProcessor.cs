#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     Processes a number of simple action types that turn into single animation curves:
    ///     DriveActiveState, DriveInternalParameter, DriveParameter, FloatPropAction, ObjectPropAction,
    ///     SetMaterial, SetShapeKey, and NullAction
    /// </summary>
    internal class SimplePropProcessor : IEffectProcessor
    {
        private struct PropInfo
        {
            public Object? Target;
            public string Name;
            public Type TargetType;
            public bool IsPPtr;
            public object? Value;
        }

        private PropInfo? ActionToProp(IAction action)
        {
            switch (action)
            {
                case DriveActiveState das:
                    return new PropInfo
                    {
                        Target = das.Target,
                        Name = "m_IsActive",
                        TargetType = typeof(GameObject),
                        IsPPtr = false,
                        Value = das.Active ? 1f : 0f
                    };
                case DriveParameter dp:
                    return new PropInfo
                    {
                        Target = null,
                        Name = dp.ParameterName,
                        TargetType = typeof(Animator),
                        IsPPtr = false,
                        Value = dp.Value
                    };
                case DriveInternalParameter dip:
                    return new PropInfo
                    {
                        Target = null,
                        Name = dip.ParameterName,
                        TargetType = typeof(Animator),
                        IsPPtr = false,
                        Value = dip.State ? 1f : 0f
                    };
                case FloatPropAction fp:
                {
                    var target = fp.Prop.TargetObject;
                    if (target == null) return null;
                    return new PropInfo
                    {
                        Target = target,
                        Name = fp.Prop.PropertyName,
                        TargetType = target.GetType(),
                        IsPPtr = false,
                        Value = fp.Value
                    };
                }
                case ObjectPropAction op:
                {
                    var target = op.Prop.TargetObject;
                    if (target == null) return null;
                    return new PropInfo
                    {
                        Target = target,
                        Name = op.Prop.PropertyName,
                        TargetType = target.GetType(),
                        IsPPtr = true,
                        Value = op.Value
                    };
                }
                case NullAction: // fall through
                default:
                    return null;
            }
        }

        private IEnumerable<PropInfo> ActionToProps(IEnumerable<IAction> actions)
        {
            foreach (var action in actions)
            {
                if (action is TargetKeyOverride tko)
                {
                    foreach (var inner in ActionToProps(tko.Actions))
                    {
                        yield return inner;
                    }
                }
                else
                {
                    var prop = ActionToProp(action);
                    if (prop.HasValue)
                    {
                        yield return prop.Value;
                    }
                }
            }
        }

        public EffectGroup? Accept(object targetKey, IReadOnlyList<ReactionNode> nodes)
        {
            switch (targetKey)
            {
                case MaterialSlotTarget:
                case InternalParameterTarget:
                case ObjectActiveTarget:
                case ParameterTarget:
                case PropertyTarget:
                case ShapeKeyTarget:
                    return BuildEffectGroup(targetKey, nodes);
                default:
                    // Special case - accept if all actions are NullActions
                    // (this helps making unit tests a bit easier)
                    if (nodes.All(n => n.Effects.All(a => a is NullAction)))
                    {
                        return BuildEffectGroup(targetKey, nodes);
                    }
                    
                    return null;
            }
        }

        public bool CanMerge(EffectGroup a, EffectGroup b)
        {
            return true;
        }

        private EffectGroup BuildEffectGroup(object targetKey, IEnumerable<ReactionNode> nodes)
        {
            var nodeList = nodes.ToList();
            return new EffectGroup(this, targetKey, nodeList, EffectProcessorHelpers.BranchLatency(nodeList.Count));
        }

        public IMotionNode ProcessEffectGroup(UnityBlendTreeBackend backend, EffectGroup group)
        {
            return EffectProcessorHelpers.GenerateBranch(
                group.Nodes.Select(node => (condition: node.Expression, node: GenerateMotion(backend, node))),
                group.DefaultNode
            );
        }

        private IMotionNode GenerateMotion(UnityBlendTreeBackend backend, ReactionNode node)
        {
            var clip = VirtualClip.Create("Effect");

            foreach (var prop in ActionToProps(node.Effects))
            {
                var targetObj = ResolveTarget(prop.Target);
                string targetPath;
                if (targetObj == null) targetPath = ""; // animator target
                else targetPath = backend.ObjectPathRemapper.GetVirtualPathForObject(targetObj);

                if (prop.IsPPtr)
                {
                    var ecb = EditorCurveBinding.PPtrCurve(
                        targetPath,
                        prop.TargetType,
                        prop.Name
                    );
                    clip.SetObjectCurve(ecb, new ObjectReferenceKeyframe[]
                    {
                        new()
                        {
                            time = 0,
                            value = (Object?)prop.Value
                        }
                    });
                }
                else
                {
                    var ecb = EditorCurveBinding.FloatCurve(
                        targetPath,
                        prop.TargetType,
                        prop.Name
                    );
                    clip.SetFloatCurve(ecb, AnimationCurve.Constant(0, 1, (float)prop.Value!));
                }
            }

            return new MotionNode(clip);
        }

        private GameObject? ResolveTarget(Object? propTarget)
        {
            if (propTarget == null) return null;
            if (propTarget is Component c) return c.gameObject;
            if (propTarget is GameObject go) return go;
            throw new ArgumentException($"Invalid target type {propTarget.GetType().FullName} for prop target");
        }
    }
}