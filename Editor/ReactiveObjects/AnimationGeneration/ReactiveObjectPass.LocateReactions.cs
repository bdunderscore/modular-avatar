using System.Collections.Generic;
using nadena.dev.modular_avatar.animation;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    partial class ReactiveObjectPass
    {
        
        private Dictionary<TargetProp, AnimatedProperty> FindShapes(ndmf.BuildContext context)
        {
            var asc = context.Extension<AnimationServicesContext>();

            var changers = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarShapeChanger>(true);

            Dictionary<TargetProp, AnimatedProperty> shapeKeys = new();

            foreach (var changer in changers)
            {
                var renderer = changer.targetRenderer.Get(changer)?.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) continue;

                var mesh = renderer.sharedMesh;

                if (mesh == null) continue;

                foreach (var shape in changer.Shapes)
                {
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
                        var agk = new ReactionData(context, key, null, value);
                        agk.Value = renderer.GetBlendShapeWeight(shapeId);
                        info.actionGroups.Add(agk);
                    }

                    var action = new ReactionData(context, key, changer.gameObject, value);
                    var isCurrentlyActive = changer.gameObject.activeInHierarchy;

                    if (shape.ChangeType == ShapeChangeType.Delete)
                    {
                        action.IsDelete = true;
                        
                        if (isCurrentlyActive) info.currentState = 100;

                        info.actionGroups.Add(action); // Never merge

                        continue;
                    }

                    if (changer.gameObject.activeInHierarchy) info.currentState = action.Value;

                    Debug.Log("Trying merge: " + action);
                    if (info.actionGroups.Count == 0)
                    {
                        info.actionGroups.Add(action);
                    }
                    else if (!info.actionGroups[^1].TryMerge(action))
                    {
                        Debug.Log("Failed merge");
                        info.actionGroups.Add(action);
                    }
                    else
                    {
                        Debug.Log("Post merge: " + info.actionGroups[^1]);
                    }
                }
            }

            return shapeKeys;
        }
        
        private void FindMaterialSetters(Dictionary<TargetProp, AnimatedProperty> objectGroups, ndmf.BuildContext buildContext)
        {
            var asc = buildContext.Extension<AnimationServicesContext>();
            var materialSetters = buildContext.AvatarRootObject.GetComponentsInChildren<ModularAvatarMaterialSetter>(true);

            foreach (var setter in materialSetters)
            {
                if (setter.Objects == null) continue;

                foreach (var obj in setter.Objects)
                {
                    var target = obj.Object.Get(setter);
                    if (target == null) continue;
                    var renderer = target.GetComponent<Renderer>();
                    if (renderer == null || renderer.sharedMaterials.Length < obj.MaterialIndex) continue;

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
                    
                    var action = new ReactionData(context, key, setter.gameObject, obj.Material);

                    if (group.actionGroups.Count == 0)
                        group.actionGroups.Add(action);
                    else if (!group.actionGroups[^1].TryMerge(action)) group.actionGroups.Add(action);
                }
            }
        }
        
        private void FindObjectToggles(Dictionary<TargetProp, AnimatedProperty> objectGroups, ndmf.BuildContext context)
        {
            var asc = context.Extension<AnimationServicesContext>();

            var toggles = this.context.AvatarRootObject.GetComponentsInChildren<ModularAvatarObjectToggle>(true);

            foreach (var toggle in toggles)
            {
                if (toggle.Objects == null) continue;

                foreach (var obj in toggle.Objects)
                {
                    var target = obj.Object.Get(toggle);
                    if (target == null) continue;

                    // Make sure we generate an animator prop for each controlled object, as we intend to generate
                    // animations for them.
                    asc.GetActiveSelfProxy(target);

                    var key = new TargetProp
                    {
                        TargetObject = target,
                        PropertyName = "m_IsActive"
                    };

                    if (!objectGroups.TryGetValue(key, out var group))
                    {
                        group = new AnimatedProperty(key, target.activeSelf ? 1 : 0);
                        objectGroups[key] = group;
                    }

                    var value = obj.Active ? 1 : 0;
                    var action = new ReactionData(context, key, toggle.gameObject, value);

                    if (group.actionGroups.Count == 0)
                        group.actionGroups.Add(action);
                    else if (!group.actionGroups[^1].TryMerge(action)) group.actionGroups.Add(action);
                }
            }
        }
    }
}