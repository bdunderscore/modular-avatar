using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    partial class ReactiveObjectAnalyzer
    {
        private ReactionRule ObjectRule(TargetProp key, Component controllingObject, object value, GameObject affectedObject = null)
        {
            var rule = new ReactionRule(key, value);

            BuildConditions(controllingObject, rule, affectedObject);
            
            return rule;
        }

        private string GetActiveSelfProxy(GameObject obj)
        {
            if (_asc != null)
            {
                return _rpe.GetActiveSelfProxy(obj);
            }
            else
            {
                var param = "__ActiveSelfProxy/" + obj.GetInstanceID();
                _simulationInitialStates[param] = obj.activeSelf ? 1.0f : 0.0f;
                return param;
            }
        }

        private readonly Dictionary<(SkinnedMeshRenderer, string), HashSet<(SkinnedMeshRenderer, string)>>
            _blendshapeSyncMappings = new();

        private void LocateBlendshapeSyncs(GameObject root)
        {
            var components = _computeContext.GetComponentsInChildren<ModularAvatarBlendshapeSync>(root, true);

            foreach (var bss in components)
            {
                var localMesh = _computeContext.GetComponent<SkinnedMeshRenderer>(bss.gameObject);
                if (localMesh == null) continue;

                foreach (var entry in _computeContext.Observe(bss, bss_ => bss_.Bindings.ToImmutableList(),
                             Enumerable.SequenceEqual))
                {
                    var src = entry.ReferenceMesh.Get(bss);
                    if (src == null) continue;

                    var srcMesh = _computeContext.GetComponent<SkinnedMeshRenderer>(src);

                    var localBlendshape = entry.LocalBlendshape;
                    if (string.IsNullOrWhiteSpace(localBlendshape))
                    {
                        localBlendshape = entry.Blendshape;
                    }

                    var srcBinding = (srcMesh, entry.Blendshape);
                    var dstBinding = (localMesh, localBlendshape);

                    if (!_blendshapeSyncMappings.TryGetValue(srcBinding, out var dstSet))
                    {
                        dstSet = new HashSet<(SkinnedMeshRenderer, string)>();
                        _blendshapeSyncMappings[srcBinding] = dstSet;
                    }

                    dstSet.Add(dstBinding);
                }
            }

            // For recursive blendshape syncs, we need to precompute the full set of affected blendshapes.
            foreach (var (src, dsts) in _blendshapeSyncMappings)
            {
                var visited = new HashSet<(SkinnedMeshRenderer, string)>();
                foreach (var item in Visit(src, visited).ToList())
                {
                    dsts.Add(item);
                }
            }

            IEnumerable<(SkinnedMeshRenderer, string)> Visit(
                (SkinnedMeshRenderer, string) key,
                HashSet<(SkinnedMeshRenderer, string)> visited
            )
            {
                if (!visited.Add(key)) yield break;

                if (_blendshapeSyncMappings.TryGetValue(key, out var children))
                {
                    foreach (var child in children)
                    {
                        foreach (var item in Visit(child, visited))
                        {
                            yield return item;
                        }
                    }
                }

                yield return key;
            }
        }
        
        private void BuildConditions(Component controllingComponent, ReactionRule rule, GameObject affectedObject)
        {
            var affectedTransform = affectedObject?.transform;
            
            rule.ControllingObject = controllingComponent;

            var conditions = new List<ControlCondition>();

            var cursor = controllingComponent?.transform;

            bool did_mami = false;

            _computeContext.ObservePath(controllingComponent.transform);

            while (cursor != null && !RuntimeUtil.IsAvatarRoot(cursor))
            {
                // Only look at the menu item closest to the object we're directly attached to, to avoid submenus
                // causing issues...
                var mami = _computeContext.GetComponent<ModularAvatarMenuItem>(cursor.gameObject);
                if (mami != null && !did_mami)
                {
                    did_mami = true;

                    _computeContext.Observe(mami, c => (c.PortableControl?.Parameter, c.PortableControl?.Type, c.PortableControl?.Value, c.isDefault));
                    
                    var mami_condition = ParameterAssignerPass.AssignMenuItemParameter(mami, _simulationInitialStates);

                    if (mami_condition != null && ForceMenuItems != null &&
                        ForceMenuItems.TryGetValue(mami_condition.Parameter, out var forcedMenuItem))
                    {
                        var enable = forcedMenuItem == mami;
                        mami_condition.InitialValue = 0.5f;
                        mami_condition.ParameterValueLo = enable ? 0 : 999f;
                        mami_condition.ParameterValueHi = 1000;
                        mami_condition.IsConstant = true;
                    }
                    
                    if (mami_condition != null) conditions.Add(mami_condition);
                }

                // We don't need to disable or enable this rule based on parents of the affected object, since
                // the rule is irrelevant when the affected object itself is disabled.
                var cursorTransform = cursor.transform;
                if (affectedObject == null ||
                    (affectedTransform != cursorTransform && !affectedTransform.IsChildOf(cursorTransform)))
                {
                    conditions.Add(new ControlCondition
                    {
                        Parameter = GetActiveSelfProxy(cursor.gameObject),
                        DebugName = cursor.gameObject.name,
                        IsConstant = false,
                        InitialValue = _computeContext.Observe(cursor.gameObject, go => go.activeSelf) ? 1.0f : 0.0f,
                        ParameterValueLo = 0.5f,
                        ParameterValueHi = float.PositiveInfinity,
                        ReferenceObject = cursor.gameObject,
                        DebugReference = cursor.gameObject,
                    });
                }

                cursor = cursor.parent;
            }

            rule.ControllingConditions = conditions;
        }

        private Dictionary<TargetProp, AnimatedProperty> FindShapes(GameObject root)
        {
            var changers = _computeContext.GetComponentsInChildren<ModularAvatarShapeChanger>(root, true);

            Dictionary<TargetProp, AnimatedProperty> shapeKeys = new();

            foreach (var changer in changers)
            {
                if (changer.Shapes == null) continue;
                var shapes = _computeContext.Observe(changer, 
                    c => c.Shapes.Select(s => s.Clone()).ToList(), 
                    (a,b) => a.SequenceEqual(b)
                    );

                var threshold = _computeContext.Observe(changer, c => c.Threshold);

                foreach (var shape in shapes)
                {
                    var renderer = _computeContext.GetComponent<SkinnedMeshRenderer>(shape.Object.Get(changer));
                    if (renderer == null) continue;
                    
                    var mesh = renderer.sharedMesh;
                    _computeContext.Observe(mesh);
                    if (mesh == null) continue;

                    var shapeId = mesh.GetBlendShapeIndex(shape.ShapeName);
                    if (shapeId < 0) continue;

                    var key = new TargetProp
                    {
                        TargetObject = renderer,
                        PropertyName = BlendshapePrefix + shape.ShapeName
                    };

                    var currentValue = renderer.GetBlendShapeWeight(shapeId);
                    var value = shape.ChangeType == ShapeChangeType.Delete ? 100 : shape.Value;

                    var deleteKey = new TargetProp
                    {
                        TargetObject = renderer,
                        PropertyName = DeletedShapePrefix + shape.ShapeName
                    };

                    if (shape.ChangeType != ShapeChangeType.Delete)
                    {
                        RegisterAction(key, currentValue, value, changer);
                        RegisterAction(deleteKey, null, null, changer);

                        if (_blendshapeSyncMappings.TryGetValue((renderer, shape.ShapeName), out var bindings))
                        {
                            // Propagate the new value through any Blendshape Syncs we might have.
                            // Note that we don't propagate deletes; it's common to e.g. want to delete breasts from the
                            // base model while retaining outerwear that matches the breast size.
                            foreach (var binding in bindings)
                            {
                                var bindingKey = new TargetProp
                                {
                                    TargetObject = binding.Item1,
                                    PropertyName = BlendshapePrefix + binding.Item2
                                };
                                var bindingRenderer = binding.Item1;

                                var bindingMesh = bindingRenderer.sharedMesh;
                                if (bindingMesh == null) continue;

                                var bindingShapeIndex = bindingMesh.GetBlendShapeIndex(binding.Item2);
                                if (bindingShapeIndex < 0) continue;

                                var bindingInitialState = bindingRenderer.GetBlendShapeWeight(bindingShapeIndex);

                                RegisterAction(bindingKey, bindingInitialState, value, changer);
                            }
                        }
                    }

                    if (shape.ChangeType == ShapeChangeType.Delete)
                    {
                        RegisterAction(deleteKey, null, new VertexFilterByShape(shape.ShapeName, threshold), changer);
                    }
                }
            }

            return shapeKeys;

            void RegisterAction(TargetProp key, float? currentValue, object value, ModularAvatarShapeChanger changer)
            {
                if (!shapeKeys.TryGetValue(key, out var info))
                {
                    info = new AnimatedProperty(key, currentValue);
                    shapeKeys[key] = info;
                }

                var action = ObjectRule(key, changer, value);
                action.Inverted = _computeContext.Observe(changer, c => c.Inverted);
                
                info.currentState = currentValue;

                if (info.actionGroups.Count == 0)
                {
                    info.actionGroups.Add(action);
                }
                else if (!info.actionGroups[^1].TryMerge(action))
                {
                    info.actionGroups.Add(action);
                }
            }
        }
        
        private void FindMeshCutter(Dictionary<TargetProp, AnimatedProperty> objectGroups, GameObject root)
        {
            var deleters = _computeContext.GetComponentsInChildren<ModularAvatarMeshCutter>(root, true);

            foreach (var deleter in deleters)
            {
                if (deleter.Object == null) continue;

                var obj = _computeContext.Observe(deleter, c => c.Object.Clone());
                var renderer = _computeContext.GetComponent<SkinnedMeshRenderer>(obj.Get(deleter));
                if (renderer == null) continue;

                if (_computeContext.Observe(renderer.sharedMesh) == null) continue;

                var filterComponents = _computeContext.GetComponents<IVertexFilterBehavior>(deleter.gameObject);

                var filterList = new List<IVertexFilter>();
                foreach (var component in filterComponents)
                {
                    if (VertexFilterRegistry.TryGetVertexFilter(component, _computeContext, out var filter))
                    {
                        filterList.Add(filter);
                    }
                }

                if (filterList.Count == 0) continue;

                IVertexFilter vertexFilter;
                if (filterList.Count == 1)
                {
                    vertexFilter = filterList[0];
                }
                else
                {
                    switch (_computeContext.Observe(deleter, d => d.MultiMode))
                    {
                        case MeshCutterMultiMode.VertexUnion:
                            vertexFilter = new ORFilter(filterList);
                            break;

                        case MeshCutterMultiMode.VertexIntersection:
                        default:
                            vertexFilter = new ANDFilter(filterList);
                            break;
                    }
                }
                vertexFilter.Observe(_computeContext);

                var key = new TargetProp
                {
                    TargetObject = renderer,
                    PropertyName = "deletedMeshByMask." + vertexFilter
                };

                if (!objectGroups.TryGetValue(key, out var group))
                {
                    group = new AnimatedProperty(key, null);
                    objectGroups[key] = group;
                }

                var action = ObjectRule(key, deleter, vertexFilter, renderer.gameObject);
                action.Inverted = _computeContext.Observe(deleter, c => c.Inverted);

                if (group.actionGroups.Count == 0 || !group.actionGroups[^1].TryMerge(action))
                {
                    group.actionGroups.Add(action);
                }
            }
        }

        private void FindMaterialChangers(Dictionary<TargetProp, AnimatedProperty> objectGroups, GameObject root)
        {
            var changers = _computeContext.GetComponentsInChildren<IModularAvatarMaterialChanger>(root, true);
            var renderers = _computeContext.GetComponentsInChildren<Renderer>(root, true);
            var interestingRenderers = new HashSet<Renderer>();

            foreach (var changer in changers)
            {
                switch (changer)
                {
                    case ModularAvatarMaterialSetter setter: RegisterMaterialSetter(setter); break;
                    case ModularAvatarMaterialSwap swap: RegisterMaterialSwap(swap); break;
                }
            }

            ObserveMaterialSetters(changers.OfType<ModularAvatarMaterialSetter>());
            ObserveMaterialSwaps(changers.OfType<ModularAvatarMaterialSwap>());

            void ObserveMaterialSetters(IEnumerable<ModularAvatarMaterialSetter> setters)
            {
            }

            void ObserveMaterialSwaps(IEnumerable<ModularAvatarMaterialSwap> swaps)
            {
                foreach (var renderer in interestingRenderers)
                {
                    // Observe whether any of the renderer’s sharedMaterials have changed
                    var currentRegistry = ObjectRegistry.ActiveRegistry;

                    _computeContext.Observe(renderer, r => r.sharedMaterials, SequenceEqualByOriginalMaterial);

                    bool SequenceEqualByOriginalMaterial(Material[] ax, Material[] bx)
                    {
                        if (ax.Length != bx.Length) return false;

                        var activeRegistry = ObjectRegistry.ActiveRegistry;

                        for (var i = 0; i < ax.Length; i++)
                        {
                            Object a = ax[i];
                            Object b = bx[i];

                            a = currentRegistry?.GetReference(a, false)?.Object
                                ?? activeRegistry?.GetReference(a, false)?.Object
                                ?? a;
                            b = currentRegistry?.GetReference(b, false)?.Object
                                ?? activeRegistry?.GetReference(b, false)?.Object
                                ?? b;
                            if (a != b) return false;
                        }

                        return true;
                    }
                    
                    // Re-evaluate the context if the hierarchy changes. Note that this will force the above Observe
                    // to be re-evaluated as well.
                    _computeContext.ObservePath(renderer.transform);
                }
            }

            void RegisterMaterialSetter(ModularAvatarMaterialSetter setter)
            {
                if (setter.Objects == null) return;

                foreach (var obj in _computeContext.Observe(setter, c => c.Objects.Select(o => o.Clone()).ToList(),
                    Enumerable.SequenceEqual))
                {
                    var renderer = _computeContext.GetComponent<Renderer>(obj.Object.Get(setter));
                    if (renderer == null || renderer.sharedMaterials.Length <= obj.MaterialIndex) continue;

                    RegisterAction(setter, renderer, obj.MaterialIndex, obj.Material);

                    interestingRenderers.Add(renderer);
                }
            }

            void RegisterMaterialSwap(ModularAvatarMaterialSwap swap)
            {
                if (swap.Swaps == null) return;

                var swapRoot = _computeContext.Observe(swap, c => c.Root.Get(swap));
                foreach (var obj in _computeContext.Observe(swap, c => c.Swaps.Select(o => o.Clone()).ToList(),
                    Enumerable.SequenceEqual))
                {
                    var objFrom =
                        obj.From ? ObjectRegistry.GetReference(obj.From)?.Object as Material ?? obj.From : null;

                    foreach (var renderer in renderers)
                    {
                        if (swapRoot != null && !renderer.transform.IsChildOf(swapRoot.transform)) continue;

                        interestingRenderers.Add(renderer);

                        var mats = renderer.sharedMaterials;
                        for (var i = 0; i < mats.Length; i++)
                        {
                            var m = mats[i];
                            var originalMaterial = ObjectRegistry.GetReference(m)?.Object as Material ?? m;
                            if (originalMaterial == objFrom)
                            {
                                RegisterAction(swap, renderer, i, obj.To);
                            }
                        }
                    }
                }
            }

            void RegisterAction(ReactiveComponent component, Renderer renderer, int index, Material material)
            {
                var key = new TargetProp
                {
                    TargetObject = renderer,
                    PropertyName = "m_Materials.Array.data[" + index + "]",
                };

                if (!objectGroups.TryGetValue(key, out var group))
                {
                    group = new(key, renderer.sharedMaterials[index]);
                    objectGroups[key] = group;
                }

                var action = ObjectRule(key, component, material);
                action.Inverted = _computeContext.Observe(component, c => c.Inverted);

                if (group.actionGroups.Count == 0)
                    group.actionGroups.Add(action);
                else if (!group.actionGroups[^1].TryMerge(action)) group.actionGroups.Add(action);
            }
        }
        
        private void FindObjectToggles(Dictionary<TargetProp, AnimatedProperty> objectGroups, GameObject root)
        {
            var toggles = _computeContext.GetComponentsInChildren<ModularAvatarObjectToggle>(root, true);

            foreach (var toggle in toggles)
            {
                if (toggle.Objects == null) continue;

                foreach (var obj in _computeContext.Observe(toggle, c => c.Objects.Select(o => o.Clone()).ToList(),
                             Enumerable.SequenceEqual))
                {
                    var target = obj.Object.Get(toggle);
                    if (target == null) continue;
                    
                    var key = new TargetProp
                    {
                        TargetObject = target,
                        PropertyName = "m_IsActive"
                    };

                    if (!objectGroups.TryGetValue(key, out var group))
                    {
                        var active = _computeContext.Observe(target, t => t.activeSelf);
                        group = new AnimatedProperty(key, active ? 1f : 0f);
                        objectGroups[key] = group;
                    }

                    var value = obj.Active ? 1f : 0f;
                    var action = ObjectRule(key, toggle, value);
                    action.Inverted = _computeContext.Observe(toggle, c => c.Inverted);

                    if (group.actionGroups.Count == 0)
                        group.actionGroups.Add(action);
                    else if (!group.actionGroups[^1].TryMerge(action)) group.actionGroups.Add(action);
                }
            }
        }
    }
}