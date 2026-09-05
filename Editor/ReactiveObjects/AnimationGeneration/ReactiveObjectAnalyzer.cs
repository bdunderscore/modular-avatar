#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Performs analysis of reactive object rules prior to animation generation. This is used for debug
    /// displays/introspection as well.
    /// </summary>
    internal partial class ReactiveObjectAnalyzer
    {
        private readonly ComputeContext _computeContext;
        private readonly ndmf.BuildContext? _context;
        private readonly ReadablePropertyExtension? _rpe;

        private readonly Dictionary<string, float>? _simulationInitialStates;

        public const string BlendshapePrefix = "blendShape.";

        public bool OptimizeShapes = true;
        
        public ImmutableDictionary<string, float> ForcePropertyOverrides { get; set; } = ImmutableDictionary<string, float>.Empty;

        public ImmutableDictionary<string, ModularAvatarMenuItem?> ForceMenuItems { get; set; } =
            ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty;

        public static AnalysisResult NullAnalysis =>
            new()
            {
                Shapes = new Dictionary<object, AnimatedProperty>(),
                InitialActions = new Dictionary<object, IAction>()
            };

        public ReactiveObjectAnalyzer(ndmf.BuildContext context)
        {
            _computeContext = ComputeContext.NullContext;
            _context = context;
            _rpe = context.Extension<ReadablePropertyExtension>();
            _simulationInitialStates = null;
        }

        public ReactiveObjectAnalyzer(ComputeContext? computeContext = null)
        {
            _computeContext = computeContext ?? ComputeContext.NullContext;
            _context = null;
            _rpe = null;
            _simulationInitialStates = new();
        }

        public string GetGameObjectStateProperty(GameObject obj)
        {
            return GetActiveSelfProxy(obj);
        }

        public struct AnalysisResult
        {
            public Dictionary<object, AnimatedProperty> Shapes;
            public Dictionary<object, IAction> InitialActions;
        }

        private static PropCache<GameObject, AnalysisResult>? _analysisCache;

        public static AnalysisResult CachedAnalyze(ComputeContext context, GameObject root)
        {
            if (_analysisCache == null)
            {
                _analysisCache = new PropCache<GameObject, AnalysisResult>("ROAnalyzer", (ctx, root) =>
                {
                    if (!ctx.Observe(root, obj => obj.activeInHierarchy))
                    {
                        return NullAnalysis;
                    }

                    var analysis = new ReactiveObjectAnalyzer(ctx);
                    analysis.ForcePropertyOverrides = ctx.Observe(ROSimulator.PropertyOverrides, a=>a, (a,b) => false)
                        ?? ImmutableDictionary<string, float>.Empty;
                    analysis.ForceMenuItems = ctx.Observe(ROSimulator.MenuItemOverrides, a => a, (a, b) => false)
                                              ?? ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty;
                    return analysis.Analyze(root);
                });
            }
            
            return _analysisCache.Get(context, root);
        }

        /// <summary>
        /// Find all reactive object rules
        /// </summary>
        /// <param name="root">The avatar root</param>
        /// <param name="initialActions">The last initially-active action for each semantic target</param>
        /// <returns></returns>
        public AnalysisResult Analyze(
            GameObject? root
        )
        {
            AnalysisResult result = new();

            if (root == null)
            {
                result.Shapes = new();
                result.InitialActions = new Dictionary<object, IAction>();
                return result;
            }

            LocateBlendshapeSyncs(root);

            Dictionary<object, AnimatedProperty> shapes = FindShapes(root);
            FindMeshCutter(shapes, root);
            FindObjectToggles(shapes, root);
            FindMaterialChangers(shapes, root);


            ApplyInitialStateOverrides(shapes);
            AnalyzeConstants(shapes); 
            ResolveToggleInitialStates(shapes);
            PreprocessShapes(shapes, out result.InitialActions);
            result.Shapes = shapes;

            return result;
        }


        private void ApplyInitialStateOverrides(Dictionary<object, AnimatedProperty> shapes)
        {
            foreach (var prop in shapes.Values)
            {
                foreach (var rule in prop.actionGroups)
                {
                    foreach (var cond in rule.ControllingConditions)
                    {
                        var paramName = cond.Parameter;
                        if (ForcePropertyOverrides?.TryGetValue(paramName, out var value) == true)
                        {
                            cond.InitialValue = value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines which animated properties have a constant state, and prunes the set of rules appropriately.
        /// No-op if there is not build context (as animations cannot be determined)
        /// </summary>
        /// <param name="shapes"></param>
        internal void AnalyzeConstants(Dictionary<object, AnimatedProperty> shapes)
        {
            var asc = _context?.Extension<AnimatorServicesContext>();
            HashSet<GameObject> toggledObjects = new();

            if (asc == null) return;

            foreach (var targetKey in shapes.Keys)
                if (targetKey is ObjectActiveTarget { Target: var go })
                    toggledObjects.Add(go);

            foreach (var group in shapes.Values)
            {
                foreach (var actionGroup in group.actionGroups)
                {
                    foreach (var condition in actionGroup.ControllingConditions)
                        if (condition.ReferenceObject is { } referenceObject &&
                            !toggledObjects.Contains(referenceObject))
                        {
                            var virtualPath = asc.ObjectPathRemapper.GetVirtualPathForObject(referenceObject);

                            condition.IsConstant = !asc.AnimationIndex.GetClipsForBinding(
                                EditorCurveBinding.FloatCurve(
                                    virtualPath,
                                    typeof(GameObject),
                                    "m_IsActive"
                                )).Any();
                        }

                    // Remove redundant active conditions.
                    actionGroup.ControllingConditions.RemoveAll(c => c.IsConstant && c.InitiallyActive);
                }

                // Remove any action groups with always-unsatisfied conditions
                group.actionGroups.RemoveAll(agk => agk.IsConstant && !agk.InitiallyActive);
                
                // Remove all action groups up until the last one where we're always on
                var lastAlwaysOnGroup = group.actionGroups.FindLastIndex(ag => ag.IsConstantActive);
                if (lastAlwaysOnGroup > 0)
                    group.actionGroups.RemoveRange(0, lastAlwaysOnGroup);
            }

            // Remove shapes with no action groups.
            foreach (var kvp in shapes.ToList())
                if (kvp.Value.actionGroups.Count == 0)
                    shapes.Remove(kvp.Key);
        }

        /// <summary>
        /// Resolves the initial active state of all GameObjects
        /// </summary>
        /// <param name="groups"></param>
        private void ResolveToggleInitialStates(Dictionary<object, AnimatedProperty> groups)
        {
            Dictionary<string, float> propStates = new();
            Dictionary<string, float> nextPropStates = new();
            int loopLimit = 5;
            var forceOverrides = ForcePropertyOverrides ?? ImmutableDictionary<string, float>.Empty;
            
            foreach (var kvp in forceOverrides)
            {
                propStates[kvp.Key] = kvp.Value;
            }

            bool unsettled = true;
            while (unsettled && loopLimit-- > 0)
            {
                unsettled = false;

                foreach (var group in groups.Values)
                {
                    var activeRules = group.actionGroups
                        .Where(rule => rule.Action is DriveActiveState)
                        .Select(rule => (Rule: rule, Action: (DriveActiveState)rule.Action))
                        .ToList();
                    if (activeRules.Count == 0) continue;

                    var targetObject = activeRules[0].Action.Target;
                    var pathKey = GetActiveSelfProxy(targetObject);
                    
                    float state;
                    if (!propStates.TryGetValue(pathKey, out state)) state = targetObject.activeSelf ? 1 : 0;

                    foreach (var (actionGroup, action) in activeRules)
                    {
                        bool evaluated = true;
                        foreach (var condition in actionGroup.ControllingConditions)
                        {
                            if (!propStates.TryGetValue(condition.Parameter, out var propCondition))
                            {
                                propCondition = condition.InitiallyActive ? 1 : 0;
                            }

                            if (propCondition < 0.5f)
                            {
                                evaluated = false;
                                break;
                            }
                        }
                        
                        if (actionGroup.Inverted) evaluated = !evaluated;

                        if (evaluated)
                        {
                            state = action.Active ? 1f : 0f;
                        }
                    }

                    nextPropStates[pathKey] = state;

                    if (!propStates.TryGetValue(pathKey, out var oldState) || !Mathf.Approximately(oldState, state))
                    {
                        unsettled = true;
                    }
                }
                
                foreach (var kvp in forceOverrides)
                {
                    nextPropStates[kvp.Key] = kvp.Value;
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
                            condition.InitialValue = state;
                    }
                }
            }
        }

        /// <summary>
        /// Determine initial state for all properties
        /// </summary>
        /// <param name="shapes"></param>
        /// <param name="initialActions">The last initially-active action for each semantic target</param>
        private void PreprocessShapes(Dictionary<object, AnimatedProperty> shapes,
            out Dictionary<object, IAction> initialActions)
        {
            initialActions = new Dictionary<object, IAction>();
            
            foreach (var (key, info) in shapes.ToList())
            {
                if (info.actionGroups.Count == 0)
                {
                    // Never-active controls do not need an animation binding.
                    shapes.Remove(key);
                    continue;
                }

                var initialAction = info.actionGroups.LastOrDefault(agk => agk.InitiallyActive)?.Action;
                if (initialAction != null)
                {
                    initialActions[key] = initialAction;
                }

                // Retained mesh sections do not need an animation binding.
                if (info.actionGroups.All(ag => ag.Action is HideMeshSection { ShouldHide: false }))
                {
                    shapes.Remove(key);
                }
            }
        }



    }
}
