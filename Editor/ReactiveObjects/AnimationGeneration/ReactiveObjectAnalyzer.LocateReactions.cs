using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    partial class ReactiveObjectAnalyzer
    {
        private ReactionRule ObjectRule(TargetProp key, Component controllingObject, float value)
        {
            var rule = new ReactionRule(key, value);

            BuildConditions(controllingObject, rule);
            
            return rule;
        }

        private ReactionRule ObjectRule(TargetProp key, Component controllingObject, Object value)
        {
            var rule = new ReactionRule(key, value);

            BuildConditions(controllingObject, rule);
            
            return rule;
        }

        private string GetActiveSelfProxy(GameObject obj)
        {
            if (_asc != null)
            {
                return _asc.GetActiveSelfProxy(obj);
            }
            else
            {
                var param = "__ActiveSelfProxy/" + obj.GetInstanceID();
                _simulationInitialStates[param] = obj.activeSelf ? 1.0f : 0.0f;
                return param;
            }
        }
        
        private void BuildConditions(Component controllingComponent, ReactionRule rule)
        {
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

                    _computeContext.Observe(mami, c => (c.Control?.parameter, c.Control?.type, c.Control?.value, c.isDefault));
                    
                    var mami_condition = ParameterAssignerPass.AssignMenuItemParameter(mami, _simulationInitialStates);

                    if (mami_condition != null &&
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
                        PropertyName = "blendShape." + shape.ShapeName,
                    };

                    var value = shape.ChangeType == ShapeChangeType.Delete ? 100 : shape.Value;
                    if (!shapeKeys.TryGetValue(key, out var info))
                    {
                        info = new AnimatedProperty(key, renderer.GetBlendShapeWeight(shapeId));
                        shapeKeys[key] = info;

                        // Add initial state
                        var agk = new ReactionRule(key, value);
                        agk.Value = renderer.GetBlendShapeWeight(shapeId);
                        info.actionGroups.Add(agk);
                    }

                    var action = ObjectRule(key, changer, value);
                    action.Inverted = _computeContext.Observe(changer, c => c.Inverted);
                    var isCurrentlyActive = changer.gameObject.activeInHierarchy;

                    if (shape.ChangeType == ShapeChangeType.Delete)
                    {
                        action.IsDelete = true;
                        
                        if (isCurrentlyActive) info.currentState = 100;

                        info.actionGroups.Add(action); // Never merge

                        continue;
                    }

                    if (changer.gameObject.activeInHierarchy) info.currentState = action.Value;

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

            return shapeKeys;
        }
        
        private void FindMaterialSetters(Dictionary<TargetProp, AnimatedProperty> objectGroups, GameObject root)
        {
            var materialSetters = _computeContext.GetComponentsInChildren<ModularAvatarMaterialSetter>(root, true);

            foreach (var setter in materialSetters)
            {
                if (setter.Objects == null) continue;

                foreach (var obj in _computeContext.Observe(setter, c => c.Objects.Select(o => o.Clone()).ToList(),
                             Enumerable.SequenceEqual))
                {
                    var renderer = _computeContext.GetComponent<Renderer>(obj.Object.Get(setter));
                    if (renderer == null || renderer.sharedMaterials.Length <= obj.MaterialIndex) continue;

                    var key = new TargetProp
                    {
                        TargetObject = renderer,
                        PropertyName = "m_Materials.Array.data[" + obj.MaterialIndex + "]",
                    };

                    if (!objectGroups.TryGetValue(key, out var group))
                    {
                        group = new AnimatedProperty(key, renderer.sharedMaterials[obj.MaterialIndex]);
                        objectGroups[key] = group;
                    }
                    
                    var action = ObjectRule(key, setter, obj.Material);
                    action.Inverted = _computeContext.Observe(setter, c => c.Inverted);
                    
                    if (group.actionGroups.Count == 0)
                        group.actionGroups.Add(action);
                    else if (!group.actionGroups[^1].TryMerge(action)) group.actionGroups.Add(action);
                }
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
                        group = new AnimatedProperty(key, active ? 1 : 0);
                        objectGroups[key] = group;
                    }

                    var value = obj.Active ? 1 : 0;
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