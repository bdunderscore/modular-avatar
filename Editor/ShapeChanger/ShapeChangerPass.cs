#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ShapeChangerPass
    {
        struct ShapeKey
        {
            public int RendererInstanceId;
            public int ShapeIndex;
            public string ShapeKeyName; // not equated

            public bool Equals(ShapeKey other)
            {
                return RendererInstanceId == other.RendererInstanceId && ShapeIndex == other.ShapeIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is ShapeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RendererInstanceId * 397) ^ ShapeIndex;
                }
            }
        }

        class ShapeKeyInfo
        {
            public ShapeKey ShapeKey { get; }
            public string SetParam { get; set; }
            public string DeleteParam { get; set; }

            public bool alwaysDeleted;

            // Objects which trigger deletion of this shape key. 
            public List<GameObject> deletionObjects = new List<GameObject>();
            public List<ActionGroupKey> setObjects = new List<ActionGroupKey>();

            public ShapeKeyInfo(ShapeKey key)
            {
                ShapeKey = key;
            }
        }
        
        class ActionGroupKey
        {
            public ActionGroupKey(AnimationServicesContext asc, ShapeKey key, GameObject controllingObject,
                ChangedShape shape)
            {
                ShapeKey = key;
                InitiallyActive = controllingObject?.activeInHierarchy == true;

                var origControlling = controllingObject?.name ?? "<null>";
                while (controllingObject != null && !asc.TryGetActiveSelfProxy(controllingObject, out _))
                {
                    controllingObject = controllingObject.transform.parent?.gameObject;
                }

                var newControlling = controllingObject?.name ?? "<null>";
                Debug.Log("AGK: Controlling object " + origControlling + " => " + newControlling);

                ControllingObject = controllingObject;
                IsDelete = shape.ChangeType == ShapeChangeType.Delete;
                Value = IsDelete ? 100 : shape.Value;
            }

            public ShapeKey ShapeKey;
            public bool IsDelete;
            public float Value;

            public float ConditionKey;
            // When constructing the 1D blend tree to interpret the sum-of-condition-keys value, we need to ensure that
            // all valid values are solidly between two control points with the same animation clip, to avoid undesired
            // interpolation. This is done by constructing a "guard band":
            //   [ valid range ] [ guard band ] [ valid range ]
            //
            // The valid range must contain all values that could be created by valid summations. We therefore reserve
            // a "guard band" in between; by reserving the exponent below each valid stop, we can put our guard bands
            // in there.
            //  [ valid ] [ guard ] [ valid ]
            //  ^-r0      ^-g0    ^-g1
            //                      ^- r1
            // g0 = r1 / 2 = r0 * 2
            // g1 = BitDecrement(r1) (we don't actually use this currently as r0-g0 is enough)

            public float Guard => ConditionKey * 2;

            public bool ConditionKeyIsValid => float.IsFinite(ConditionKey)
                                               && float.IsFinite(Guard)
                                               && ConditionKey > 0;

            public GameObject ControllingObject;
            public bool InitiallyActive;

            public override string ToString()
            {
                var obj = ControllingObject?.name ?? "<null>";

                return $"AGK: {ShapeKey.RendererInstanceId}:{ShapeKey.ShapeIndex} ({ShapeKey.ShapeKeyName})={Value} " +
                       $"range={ConditionKey}/{Guard} controlling object={obj}";
            }

            public bool TryMerge(ActionGroupKey other)
            {
                if (!ShapeKey.Equals(other.ShapeKey)) return false;
                if (Mathf.Abs(Value - other.Value) > 0.001f) return false;
                if (ControllingObject != other.ControllingObject) return false;

                IsDelete |= other.IsDelete;

                return true;
            }
        }

        private readonly ndmf.BuildContext context;
        private Dictionary<string, float> initialValues = new();

        public ShapeChangerPass(ndmf.BuildContext context)
        {
            this.context = context;
        }

        internal void Execute()
        {
            Dictionary<ShapeKey, ShapeKeyInfo> shapes = FindShapes(context);

            foreach (var groups in shapes.Values)
            {
                ProcessShapeKey(groups);
            }

            ProcessMeshDeletion(shapes);
        }

        #region Mesh processing

        private void ProcessMeshDeletion(Dictionary<ShapeKey, ShapeKeyInfo> shapes)
        {
            ImmutableDictionary<int /* renderer */, List<ShapeKeyInfo>> renderers = shapes.Values.GroupBy(
                v => v.ShapeKey.RendererInstanceId
            ).ToImmutableDictionary(
                g => g.Key,
                g => g.ToList()
            );

            foreach (var (rendererId, infos) in renderers)
            {
                var renderer = (SkinnedMeshRenderer)EditorUtility.InstanceIDToObject(rendererId);
                if (renderer == null) continue;

                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                renderer.sharedMesh = RemoveBlendShapeFromMesh.RemoveBlendshapes(
                    mesh,
                    infos.Where(i => i.alwaysDeleted).Select(i => i.ShapeKey.ShapeIndex)
                );
            }
        }

        #endregion

        private void ProcessShapeKey(ShapeKeyInfo info)
        {
            // TODO: prune non-animated keys

            // Check if this is non-animated and skip most processing if so
            if (info.alwaysDeleted) return;
            if (info.setObjects[^1].ControllingObject == null)
            {
                var renderer = (SkinnedMeshRenderer)EditorUtility.InstanceIDToObject(info.ShapeKey.RendererInstanceId);
                renderer.SetBlendShapeWeight(info.ShapeKey.ShapeIndex, info.setObjects[0].Value);
                return;
            }

            // This value is the first non-subnormal float32
            float shift = BitConverter.Int32BitsToSingle(0x00800000);

            foreach (var group in info.setObjects)
            {
                group.ConditionKey = shift;
                shift *= 4;

                if (!group.ConditionKeyIsValid)
                {
                    throw new ArithmeticException("Floating point overflow - too many shape key controls");
                }
            }

            info.SetParam =
                $"_MA/ShapeChanger/{info.ShapeKey.RendererInstanceId}/{info.ShapeKey.ShapeIndex}/set";
            info.DeleteParam = $"_MA/ShapeChanger/{info.ShapeKey.RendererInstanceId}/{info.ShapeKey.ShapeIndex}/delete";

            var summationTree = BuildSummationTree(info);
            var applyTree = BuildApplyTree(info);
            var merged = BuildMergeTree(summationTree, applyTree);

            ApplyController(merged, "ShapeChanger Apply: " + info.ShapeKey.ShapeKeyName);
        }

        private BlendTree BuildMergeTree(BlendTree summationTree, BlendTree applyTree)
        {
            var bt = new BlendTree();
            bt.blendType = BlendTreeType.Direct;
            bt.blendParameter = MergeBlendTreePass.ALWAYS_ONE;
            bt.useAutomaticThresholds = false;

            bt.children = new[]
            {
                new ChildMotion()
                {
                    motion = summationTree,
                    directBlendParameter = MergeBlendTreePass.ALWAYS_ONE,
                    timeScale = 1,
                },
                new ChildMotion()
                {
                    motion = applyTree,
                    directBlendParameter = MergeBlendTreePass.ALWAYS_ONE,
                    timeScale = 1,
                },
            };

            return bt;
        }

        private BlendTree BuildApplyTree(ShapeKeyInfo info)
        {
            var groups = info.setObjects;

            var setTree = new BlendTree();
            setTree.blendType = BlendTreeType.Simple1D;
            setTree.blendParameter = info.SetParam;
            setTree.useAutomaticThresholds = false;

            var childMotions = new List<ChildMotion>();

            childMotions.Add(new ChildMotion()
            {
                motion = AnimResult(groups.First().ShapeKey, 0),
                timeScale = 1,
                threshold = 0,
            });

            foreach (var group in groups)
            {
                var lo = group.ConditionKey;
                var hi = group.Guard;

                Debug.Log("Threshold: [" + lo + ", " + hi + "]: " + group);

                childMotions.Add(new ChildMotion()
                {
                    motion = AnimResult(group.ShapeKey, group.Value),
                    timeScale = 1,
                    threshold = lo,
                });
                childMotions.Add(new ChildMotion()
                {
                    motion = AnimResult(group.ShapeKey, group.Value),
                    timeScale = 1,
                    threshold = hi,
                });
            }

            setTree.children = childMotions.ToArray();

            var delTree = new BlendTree();
            delTree.blendType = BlendTreeType.Simple1D;
            delTree.blendParameter = info.DeleteParam;
            delTree.useAutomaticThresholds = false;

            delTree.children = new[]
            {
                new ChildMotion()
                {
                    motion = setTree,
                    timeScale = 1,
                    threshold = 0
                },
                new ChildMotion()
                {
                    motion = setTree,
                    timeScale = 1,
                    threshold = 0.4f
                },
                new ChildMotion()
                {
                    motion = AnimResult(info.ShapeKey, 100),
                    timeScale = 1,
                    threshold = 0.6f
                },
                new ChildMotion()
                {
                    motion = AnimResult(info.ShapeKey, 100),
                    timeScale = 1,
                    threshold = 1
                },
            };

            return delTree;
        }

        private Motion AnimResult(ShapeKey key, float value)
        {
            var renderer = EditorUtility.InstanceIDToObject(key.RendererInstanceId) as SkinnedMeshRenderer;
            if (renderer == null) throw new InvalidOperationException("Failed to get object");

            var obj = renderer.gameObject;
            var path = RuntimeUtil.RelativePath(context.AvatarRootObject, obj);

            var clip = new AnimationClip();
            clip.name = $"Set {obj.name}:{key.ShapeKeyName}={value}";

            var curve = new AnimationCurve();
            curve.AddKey(0, value);
            curve.AddKey(1, value);

            var binding =
                EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{key.ShapeKeyName}");
            AnimationUtility.SetEditorCurve(clip, binding, curve);

            return clip;
        }

        private BlendTree BuildSummationTree(ShapeKeyInfo info)
        {
            var groups = info.setObjects;

            var setParam = info.SetParam;
            var delParam = info.DeleteParam;
            
            var asc = context.Extension<AnimationServicesContext>();

            BlendTree bt = new BlendTree();
            bt.blendType = BlendTreeType.Direct;

            HashSet<string> paramNames = new HashSet<string>();

            var childMotions = new List<ChildMotion>();

            // TODO eliminate excess motion field
            var initMotion = new ChildMotion()
            {
                motion = AnimParam((setParam, 0), (delParam, 0)),
                directBlendParameter = MergeBlendTreePass.ALWAYS_ONE,
                timeScale = 1,
            };
            childMotions.Add(initMotion);
            paramNames.Add(MergeBlendTreePass.ALWAYS_ONE);
            initialValues[MergeBlendTreePass.ALWAYS_ONE] = 1;
            initialValues[setParam] = 0;
            initialValues[delParam] = 0;

            foreach (var group in groups)
            {
                Debug.Log("Group: " + group);
                string controllingParam;
                if (group.ControllingObject == null)
                {
                    controllingParam = MergeBlendTreePass.ALWAYS_ONE;
                }
                else
                {
                    // TODO: path evaluation
                    if (!asc.TryGetActiveSelfProxy(group.ControllingObject, out controllingParam))
                    {
                        throw new InvalidOperationException("Failed to get active self proxy");
                    }

                    initialValues[controllingParam] = group.ControllingObject.activeSelf ? 1 : 0;
                }

                var childMotion = new ChildMotion()
                {
                    motion = AnimParam(setParam, group.ConditionKey),
                    directBlendParameter = controllingParam,
                    timeScale = 1,
                };
                childMotions.Add(childMotion);
                paramNames.Add(controllingParam);
            }

            foreach (var delController in info.deletionObjects)
            {
                if (!asc.TryGetActiveSelfProxy(delController, out var controllingParam))
                {
                    throw new InvalidOperationException("Failed to get active self proxy");
                }

                initialValues[controllingParam] = delController.activeSelf ? 1 : 0;

                var childMotion = new ChildMotion()
                {
                    motion = AnimParam(delParam, 1),
                    directBlendParameter = controllingParam,
                    timeScale = 1,
                };

                childMotions.Add(childMotion);
                paramNames.Add(controllingParam);
            }

            bt.children = childMotions.ToArray();

            return bt;
        }

        private void ApplyController(BlendTree bt, string stateName)
        {
            var fx = context.AvatarDescriptor.baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (fx.animatorController == null)
            {
                throw new InvalidOperationException("No FX layer found");
            }

            if (!context.IsTemporaryAsset(fx.animatorController))
            {
                throw new InvalidOperationException("FX layer is not a temporary asset");
            }

            if (!(fx.animatorController is AnimatorController animController))
            {
                throw new InvalidOperationException("FX layer is not an animator controller");
            }

            var stateMachine = new AnimatorStateMachine();
            var layers = animController.layers.ToList();
            layers.Add(
                new AnimatorControllerLayer() { defaultWeight = 1, name = stateName, stateMachine = stateMachine }
            );
            var state = new AnimatorState();
            state.name = stateName;
            state.motion = bt;
            state.writeDefaultValues = true;
            stateMachine.states = new[] { new ChildAnimatorState() { state = state } };
            stateMachine.defaultState = state;

            var paramList = animController.parameters.ToList();
            var paramSet = paramList.Select(p => p.name).ToHashSet();

            foreach (var paramName in initialValues.Keys.Except(paramSet))
            {
                paramList.Add(new AnimatorControllerParameter()
                {
                    name = paramName,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = initialValues[paramName], // TODO
                });
                paramSet.Add(paramName);
            }

            animController.parameters = paramList.ToArray();

            animController.layers = layers.ToArray();
        }

        private static IEnumerable<string> FindParams(BlendTree bt)
        {
            if (bt == null) yield break;

            if (bt.blendType == BlendTreeType.Direct)
            {
                foreach (var child in bt.children)
                {
                    yield return child.directBlendParameter;
                }
            }
            else
            {
                yield return bt.blendParameter;
            }

            foreach (var child in bt.children)
            {
                foreach (var param in FindParams(child.motion as BlendTree))
                {
                    yield return param;
                }
            }
        }


        private AnimationClip AnimParam(string param, float val)
        {
            return AnimParam((param, val));
        }

        private AnimationClip AnimParam(params (string param, float val)[] pairs)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = "Set " + string.Join(", ", pairs.Select(p => $"{p.param}={p.val}"));

            // TODO - check property syntax
            foreach (var (param, val) in pairs)
            {
                var curve = new AnimationCurve();
                curve.AddKey(0, val);
                curve.AddKey(1, val);
                clip.SetCurve("", typeof(Animator), "" + param, curve);
            }

            return clip;
        }

        private Dictionary<ShapeKey, ShapeKeyInfo> FindShapes(ndmf.BuildContext context)
        {
            var asc = context.Extension<AnimationServicesContext>();

            var changers = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarShapeChanger>(true);

            Dictionary<ShapeKey, ShapeKeyInfo> shapeKeys = new();

            foreach (var changer in changers)
            {
                var renderer = changer.targetRenderer.Get(changer)?.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) continue;

                int rendererInstanceId = renderer.GetInstanceID();
                var mesh = renderer.sharedMesh;

                if (mesh == null) continue;

                foreach (var shape in changer.Shapes)
                {
                    var shapeId = mesh.GetBlendShapeIndex(shape.ShapeName);
                    if (shapeId < 0) continue;

                    var key = new ShapeKey
                    {
                        RendererInstanceId = rendererInstanceId,
                        ShapeIndex = shapeId,
                        ShapeKeyName = shape.ShapeName,
                    };

                    if (!shapeKeys.TryGetValue(key, out var info))
                    {
                        info = new ShapeKeyInfo(key);
                        shapeKeys[key] = info;

                        // Add initial state
                        var agk = new ActionGroupKey(asc, key, null, shape);
                        agk.IsDelete = false;
                        agk.InitiallyActive = true;
                        agk.Value = renderer.GetBlendShapeWeight(shapeId);
                        info.setObjects.Add(agk);
                    }

                    var action = new ActionGroupKey(asc, key, changer.gameObject, shape);

                    if (action.IsDelete)
                    {
                        if (action.ControllingObject == null)
                        {
                            // always active?
                            info.alwaysDeleted |= changer.gameObject.activeInHierarchy;
                        }
                        else
                        {
                            info.deletionObjects.Add(action.ControllingObject);
                        }

                        continue;
                    }
                    
                    // TODO: lift controlling object resolution out of loop?
                    if (action.ControllingObject == null)
                    {
                        if (action.InitiallyActive)
                        {
                            // always active control
                            info.setObjects.Clear();
                        }
                        else
                        {
                            // never active control
                            continue;
                        }
                    }

                    Debug.Log("Trying merge: " + action);
                    if (info.setObjects.Count == 0)
                    {
                        info.setObjects.Add(action);
                    }
                    else if (!info.setObjects[^1].TryMerge(action))
                    {
                        Debug.Log("Failed merge");
                        info.setObjects.Add(action);
                    }
                    else
                    {
                        Debug.Log("Post merge: " + info.setObjects[^1]);
                    }
                }
            }

            return shapeKeys;
        }
    }
}