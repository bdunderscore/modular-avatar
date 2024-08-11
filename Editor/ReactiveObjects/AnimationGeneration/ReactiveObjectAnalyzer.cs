using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Performs analysis of reactive object rules prior to animation generation. This is used for debug
    /// displays/introspection as well.
    /// </summary>
    internal partial class ReactiveObjectAnalyzer
    {
        private readonly ndmf.BuildContext context;
        
        public ReactiveObjectAnalyzer(ndmf.BuildContext context)
        {
            this.context = context;
        }

        public ReactiveObjectAnalyzer()
        {
            context = null;
        }

        /// <summary>
        /// Find all reactive object rules
        /// </summary>
        /// <param name="root">The avatar root</param>
        /// <param name="initialStates">A dictionary of target property to initial state (float or UnityEngine.Object)</param>
        /// <param name="deletedShapes">A hashset of blendshape properties which are always deleted</param>
        /// <returns></returns>
        public Dictionary<TargetProp, AnimatedProperty> Analyze(
            GameObject root,
            out Dictionary<TargetProp, object> initialStates, 
            out HashSet<TargetProp> deletedShapes
        )
        {
            Dictionary<TargetProp, AnimatedProperty> shapes = FindShapes(root);
            FindObjectToggles(shapes, root);
            FindMaterialSetters(shapes, root);

            AnalyzeConstants(shapes);
            ResolveToggleInitialStates(shapes);
            PreprocessShapes(shapes, out initialStates, out deletedShapes);

            return shapes;
        }

        /// <summary>
        /// Determines which animated properties have a constant state, and prunes the set of rules appropriately.
        /// No-op if there is not build context (as animations cannot be determined)
        /// </summary>
        /// <param name="shapes"></param>
        private void AnalyzeConstants(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var asc = context?.Extension<AnimationServicesContext>();
            HashSet<GameObject> toggledObjects = new();

            if (asc == null) return;

            foreach (var targetProp in shapes.Keys)
                if (targetProp is { TargetObject: GameObject go, PropertyName: "m_IsActive" })
                    toggledObjects.Add(go);

            foreach (var group in shapes.Values)
            {
                foreach (var actionGroup in group.actionGroups)
                {
                    foreach (var condition in actionGroup.ControllingConditions)
                        if (condition.ReferenceObject != null && !toggledObjects.Contains(condition.ReferenceObject))
                            condition.IsConstant = asc.AnimationDatabase.ClipsForPath(asc.PathMappings.GetObjectIdentifier(condition.ReferenceObject)).IsEmpty;

                    var i = 0;
                    // Remove redundant active conditions.
                    int retain = 0;
                    actionGroup.ControllingConditions.RemoveAll(c => c.IsConstant && c.InitiallyActive);
                }

                // Remove any action groups with always-unsatisfied conditions
                group.actionGroups.RemoveAll(agk => agk.IsConstant && !agk.InitiallyActive);
                
                // Remove all action groups up until the last one where we're always on
                var lastAlwaysOnGroup = group.actionGroups.FindLastIndex(ag => ag.IsConstantOn);
                if (lastAlwaysOnGroup > 0)
                    group.actionGroups.RemoveRange(0, lastAlwaysOnGroup - 1);
            }

            // Remove shapes with no action groups
            foreach (var kvp in shapes.ToList())
                if (kvp.Value.actionGroups.Count == 0)
                    shapes.Remove(kvp.Key);
        }

        /// <summary>
        /// Resolves the initial active state of all GameObjects
        /// </summary>
        /// <param name="groups"></param>
        private void ResolveToggleInitialStates(Dictionary<TargetProp, AnimatedProperty> groups)
        {
            var asc = context?.Extension<AnimationServicesContext>();
            
            Dictionary<string, bool> propStates = new Dictionary<string, bool>();
            Dictionary<string, bool> nextPropStates = new Dictionary<string, bool>();
            int loopLimit = 5;

            bool unsettled = true;
            while (unsettled && loopLimit-- > 0)
            {
                unsettled = false;

                foreach (var group in groups.Values)
                {
                    if (group.TargetProp.PropertyName != "m_IsActive") continue;
                    if (!(group.TargetProp.TargetObject is GameObject targetObject)) continue;

                    var pathKey = asc?.GetActiveSelfProxy(targetObject) ?? RuntimeUtil.AvatarRootPath(targetObject);

                    bool state;
                    if (!propStates.TryGetValue(pathKey, out state)) state = targetObject.activeSelf;

                    foreach (var actionGroup in group.actionGroups)
                    {
                        bool evaluated = true;
                        foreach (var condition in actionGroup.ControllingConditions)
                        {
                            if (!propStates.TryGetValue(condition.Parameter, out var propCondition))
                            {
                                propCondition = condition.InitiallyActive;
                            }

                            if (!propCondition)
                            {
                                evaluated = false;
                                break;
                            }
                        }
                        
                        if (actionGroup.Inverted) evaluated = !evaluated;

                        if (evaluated)
                        {
                            state = (float) actionGroup.Value > 0.5f;
                        }
                    }

                    nextPropStates[pathKey] = state;

                    if (!propStates.TryGetValue(pathKey, out var oldState) || oldState != state)
                    {
                        unsettled = true;
                    }
                }
                
                propStates = nextPropStates;
                nextPropStates = new();
            }

            foreach (var group in groups.Values)
            {
                foreach (var action in group.actionGroups)
                {
                    foreach (var condition in action.ControllingConditions)
                    {
                        if (propStates.TryGetValue(condition.Parameter, out var state))
                            condition.InitialValue = state ? 1.0f : 0.0f;
                    }
                }
            }
        }

        /// <summary>
        /// Determine initial state and deleted shapes for all properties
        /// </summary>
        /// <param name="shapes"></param>
        /// <param name="initialStates"></param>
        /// <param name="deletedShapes"></param>
        private void PreprocessShapes(Dictionary<TargetProp, AnimatedProperty> shapes, out Dictionary<TargetProp, object> initialStates, out HashSet<TargetProp> deletedShapes)
        {
            // For each shapekey, determine 1) if we can just set an initial state and skip and 2) if we can delete the
            // corresponding mesh. If we can't, delete ops are merged into the main list of operations.
            
            initialStates = new Dictionary<TargetProp, object>();
            deletedShapes = new HashSet<TargetProp>();

            foreach (var (key, info) in shapes.ToList())
            {
                if (info.actionGroups.Count == 0)
                {
                    // never active control; ignore it entirely
                    shapes.Remove(key);
                    continue;
                }
                
                var deletions = info.actionGroups.Where(agk => agk.IsDelete).ToList();
                if (deletions.Any(d => d.ControllingConditions.All(c => c.IsConstantActive)))
                {
                    // always deleted
                    shapes.Remove(key);
                    deletedShapes.Add(key);
                    continue;
                }
                
                // Move deleted shapes to the end of the list, so they override all Set actions
                info.actionGroups = info.actionGroups.Where(agk => !agk.IsDelete).Concat(deletions).ToList();

                var initialState = info.actionGroups.Where(agk => agk.InitiallyActive)
                    .Select(agk => agk.Value)
                    .Prepend(info.currentState) // use scene state if everything is disabled
                    .Last();

                initialStates[key] = initialState;
                
                // If we're now constant-on, we can skip animation generation
                if (info.actionGroups[^1].IsConstant)
                {
                    shapes.Remove(key);
                }
            }
        }
    }
}