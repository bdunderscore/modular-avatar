#if MA_VRCSDK3_AVATARS

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

using UnityObject = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ParameterRenameMappings
    {
        public static ParameterRenameMappings Get(ndmf.BuildContext ctx)
        {
            return ctx.GetState<ParameterRenameMappings>();
        }

        public Dictionary<(ModularAvatarParameters, ParameterNamespace, string), string> Remappings =
            new Dictionary<(ModularAvatarParameters, ParameterNamespace, string), string>();

        private int internalParamIndex;

        public string Remap(ModularAvatarParameters p, ParameterNamespace ns, string s)
        {
            var tuple = (p, ns, s);

            if (Remappings.TryGetValue(tuple, out var mapping)) return mapping;

            mapping = s + "$$Internal_" + internalParamIndex++;
            Remappings[tuple] = mapping;
            
            return mapping;
        }
    }

    
    internal class DefaultValues
    {
        public ImmutableDictionary<string, float> InitialValueOverrides;
    }
    
    internal class RenameParametersHook
    {
        private const string DEFAULT_EXP_PARAMS_ASSET_GUID = "03a6d797deb62f0429471c4e17ea99a7";

        private BuildContext _context;

        // TODO: Move into NDMF
        private ImmutableList<string> PhysBoneSuffixes = ImmutableList<string>.Empty
            .Add("_IsGrabbed")
            .Add("_IsPosed")
            .Add("_Angle")
            .Add("_Stretch")
            .Add("_Squish");
        
        class ParameterInfo
        {
            private static long encounterOrderCounter;
            
            public ParameterConfig ResolvedParameter;
            public List<UnityObject> TypeSources = new List<UnityObject>();
            public List<UnityObject> DefaultSources = new List<UnityObject>();
            public ImmutableHashSet<float> ConflictingValues = ImmutableHashSet<float>.Empty;
            public ImmutableHashSet<ParameterSyncType> ConflictingSyncTypes = ImmutableHashSet<ParameterSyncType>.Empty;
            
            public bool TypeConflict, DefaultValueConflict;
            public long encounterOrder = encounterOrderCounter++;

            public VRCExpressionParameters.ValueType? ValueType
            {
                get
                {
                    switch (ResolvedParameter.syncType)
                    {
                        case ParameterSyncType.Bool: return VRCExpressionParameters.ValueType.Bool;
                        case ParameterSyncType.Float: return VRCExpressionParameters.ValueType.Float;
                        case ParameterSyncType.Int: return VRCExpressionParameters.ValueType.Int;
                        default: return null;
                    }
                }
            }
            
            public void MergeSibling(ParameterInfo info)
            {
                MergeCommon(info);

                ResolvedParameter.m_overrideAnimatorDefaults =
                    (ResolvedParameter.m_overrideAnimatorDefaults && ResolvedParameter.HasDefaultValue) ||
                    (info.ResolvedParameter.m_overrideAnimatorDefaults && info.ResolvedParameter.HasDefaultValue);
                
                if (ResolvedParameter.HasDefaultValue && info.ResolvedParameter.HasDefaultValue)
                {
                    if (Math.Abs(ResolvedParameter.defaultValue - info.ResolvedParameter.defaultValue) > ParameterConfig.VALUE_EPSILON)
                    {
                        DefaultValueConflict = true;
                        ConflictingValues = ConflictingValues.Add(ResolvedParameter.defaultValue);
                        ConflictingValues = ConflictingValues.Add(info.ResolvedParameter.defaultValue);
                    }
                }
                
                ResolvedParameter.saved |= info.ResolvedParameter.saved;
                ResolvedParameter.localOnly &= info.ResolvedParameter.localOnly;
            }

            public void MergeChild(ParameterInfo info)
            {
                MergeCommon(info);

                if (!ResolvedParameter.HasDefaultValue && info.ResolvedParameter.HasDefaultValue)
                {
                    ResolvedParameter.defaultValue = info.ResolvedParameter.defaultValue;
                    ResolvedParameter.hasExplicitDefaultValue = info.ResolvedParameter.hasExplicitDefaultValue;
                    ResolvedParameter.m_overrideAnimatorDefaults = info.ResolvedParameter.m_overrideAnimatorDefaults;
                }
            }
            
            void MergeCommon(ParameterInfo info)
            {
                if (ResolvedParameter.syncType == ParameterSyncType.NotSynced)
                {
                    ResolvedParameter.syncType = info.ResolvedParameter.syncType;
                } else if (ResolvedParameter.syncType != info.ResolvedParameter.syncType && info.ResolvedParameter.syncType != ParameterSyncType.NotSynced)
                {
                    TypeConflict = true;
                    ConflictingSyncTypes = ConflictingSyncTypes
                        .Add(ResolvedParameter.syncType)
                        .Add(info.ResolvedParameter.syncType);
                }
                
                TypeSources.AddRange(info.TypeSources);
                DefaultSources.AddRange(info.DefaultSources);

                TypeConflict = TypeConflict || info.TypeConflict;
                DefaultValueConflict = DefaultValueConflict || info.DefaultValueConflict;
                
                ConflictingValues = ConflictingValues.Union(info.ConflictingValues);
                ConflictingSyncTypes = ConflictingSyncTypes.Union(info.ConflictingSyncTypes);
                
                encounterOrder = Math.Min(encounterOrder, info.encounterOrder);
            }
        }

        public void OnPreprocessAvatar(GameObject avatar, BuildContext context)
        {
            _context = context;

            var syncParams = WalkTree(avatar);

            SetExpressionParameters(avatar, syncParams);

            _context.PluginBuildContext.GetState<DefaultValues>().InitialValueOverrides
                = syncParams.Where(p =>
                        p.Value.ResolvedParameter.HasDefaultValue &&
                        p.Value.ResolvedParameter.OverrideAnimatorDefaults)
                    .ToImmutableDictionary(p => p.Key, p => p.Value.ResolvedParameter.defaultValue);

            // clean up all parameters objects before the ParameterAssignerPass runs
            foreach (var p in avatar.GetComponentsInChildren<ModularAvatarParameters>())
                UnityObject.DestroyImmediate(p);
        }

        private void SetExpressionParameters(GameObject avatarRoot, ImmutableDictionary<string, ParameterInfo> allParams)
        {
            var syncParams = allParams.Where(kvp => kvp.Value.ResolvedParameter.syncType != ParameterSyncType.NotSynced)
                .ToImmutableDictionary();
            
            var avatar = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var expParams = avatar.expressionParameters;

            if (expParams == null)
            {
                var path = AssetDatabase.GUIDToAssetPath(DEFAULT_EXP_PARAMS_ASSET_GUID);
                expParams = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(path);
            }

            if (expParams == null)
            {
                // Can't find the defaults???
                expParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            }

            expParams = Object.Instantiate(expParams);
            _context.SaveAsset(expParams);

            var knownParams = expParams.parameters.Select(p => p.name).ToImmutableHashSet();
            var parameters = expParams.parameters
                .Select(p => ResolveParameter(p, syncParams))
                .ToList();

            foreach (var kvp in syncParams.OrderBy(kvp => kvp.Value.encounterOrder))
            {
                var name = kvp.Key;
                var param = kvp.Value;
                
                if (param.TypeConflict)
                {
                    var t1 = param.ConflictingSyncTypes.First();
                    var t2 = param.ConflictingSyncTypes.Skip(1).First();

                    List<object> paramList = new List<object> { name, t1, t2 };
                    paramList.AddRange(param.TypeSources.Cast<object>());
                    
                    BuildReport.Log(ErrorSeverity.Error, "error.rename_params.type_conflict", paramList.ToArray());
                }
                
                if (param.DefaultValueConflict)
                {
                    var v1 = param.ConflictingValues.First();
                    var v2 = param.ConflictingValues.Skip(1).First();
                    
                    List<object> paramList = new List<object> { name, v1, v2 };
                    paramList.AddRange(param.DefaultSources.Cast<object>());
                    
                    BuildReport.Log(ErrorSeverity.NonFatal, "error.rename_params.default_value_conflict", paramList.ToArray());
                }
                
                if (!knownParams.Contains(name) && param.ResolvedParameter.syncType != ParameterSyncType.NotSynced)
                {
                    var converted = new VRCExpressionParameters.Parameter();
                    converted.name = name;
                    switch (param.ResolvedParameter.syncType)
                    {
                        case ParameterSyncType.Bool:
                            converted.valueType = VRCExpressionParameters.ValueType.Bool;
                            break;
                        case ParameterSyncType.Float:
                            converted.valueType = VRCExpressionParameters.ValueType.Float;
                            break;
                        case ParameterSyncType.Int:
                            converted.valueType = VRCExpressionParameters.ValueType.Int;
                            break;
                        default:
                            throw new ArgumentException("Unknown parameter sync type " +
                                                        param.ResolvedParameter.syncType);
                    }
                    converted.networkSynced = !param.ResolvedParameter.localOnly;
                    converted.saved = param.ResolvedParameter.saved;
                    converted.defaultValue = param.ResolvedParameter.defaultValue;
                    
                    parameters.Add(converted);
                }
            }

            expParams.parameters = parameters.ToArray();

            /*
            if (expParams.CalcTotalCost() > VRCExpressionParameters.MAX_PARAMETER_COST)
            {
                BuildReport.LogFatal("error.rename_params.too_many_synced_params", new[]
                    {
                        "" + expParams.CalcTotalCost(),
                        "" + VRCExpressionParameters.MAX_PARAMETER_COST,
                    }
                );
            }
            */

            avatar.expressionParameters = expParams;
        }

        private VRCExpressionParameters.Parameter ResolveParameter(
            VRCExpressionParameters.Parameter parameter, 
            ImmutableDictionary<string, ParameterInfo> syncParams
        )
        {
            if (!syncParams.TryGetValue(parameter.name, out var info))
            {
                return parameter;
            }

            if (parameter.valueType != info.ValueType && info.ValueType != null)
            {
                var list = new List<object>
                {
                    parameter.name,
                    parameter.valueType,
                    info.ValueType,
                    _context.AvatarDescriptor.expressionParameters,
                };
                list.AddRange(info.TypeSources);
                
                BuildReport.Log(ErrorSeverity.Error, "error.rename_params.type_conflict", 
                    parameter.name,
                    list
                );
            }
            
            var newParameter = new VRCExpressionParameters.Parameter();
            newParameter.defaultValue = info.ResolvedParameter.HasDefaultValue ? info.ResolvedParameter.defaultValue : parameter.defaultValue;
            newParameter.name = parameter.name;
            newParameter.valueType = parameter.valueType;
            newParameter.networkSynced = parameter.networkSynced || !info.ResolvedParameter.localOnly;
            newParameter.saved = parameter.saved || info.ResolvedParameter.saved;
            
            return newParameter;
        }

        private ImmutableDictionary<string, ParameterInfo> WalkTree(
            GameObject obj
        )
        {
            var paramInfo = ndmf.ParameterInfo.ForContext(_context.PluginBuildContext);
            
            ImmutableDictionary<string, ParameterInfo> rv = ImmutableDictionary<string, ParameterInfo>.Empty;
            var p = obj.GetComponent<ModularAvatarParameters>();
            if (p != null)
            {
                rv = BuildReport.ReportingObject(p, () => CollectParameters(p, paramInfo.GetParameterRemappingsAt(p, true)));
            }

            foreach (var merger in obj.GetComponents<ModularAvatarMergeAnimator>())
            {
                if (merger.deleteAttachedAnimator)
                {
                    break;
                }
            }

            // Note: To match prior behavior, we use all mappings that apply to this gameobject when updating components
            // other than MA Parameters, not just ones from components listed prior.
            foreach (var component in obj.GetComponents<Component>())
            {
                BuildReport.ReportingObject(component, () =>
                {
                    switch (component)
                    {
                        case VRCPhysBone bone:
                        {
                            var remaps = paramInfo.GetParameterRemappingsAt(obj);
                            if (bone.parameter != null && remaps.TryGetValue((ParameterNamespace.PhysBonesPrefix, bone.parameter), out var newVal))
                            {
                                bone.parameter = newVal.ParameterName;
                            }

                            break;
                        }

                        case VRCContactReceiver contact:
                        {
                            if (contact.parameter != null && paramInfo.GetParameterRemappingsAt(obj)
                                    .TryGetValue((ParameterNamespace.Animator, contact.parameter), out var newVal))
                            {
                                contact.parameter = newVal.ParameterName;
                            }

                            break;
                        }

                        case ModularAvatarMergeAnimator merger:
                        {
                            // RuntimeAnimatorController may be AnimatorOverrideController, convert in case of AnimatorOverrideController
                            if (merger.animator is AnimatorOverrideController overrideController)
                            {
                                merger.animator = _context.ConvertAnimatorController(overrideController);
                            }

                            var mappings = paramInfo.GetParameterRemappingsAt(obj);
                            var remap = mappings.SelectMany(item =>
                            {
                                if (item.Key.Item1 == ParameterNamespace.Animator) return new[] { item };

                                return PhysBoneSuffixes.Select(suffix =>
                                    new KeyValuePair<(ParameterNamespace, string), ParameterMapping>(
                                        (ParameterNamespace.Animator, item.Key.Item2 + suffix),
                                        new ParameterMapping(item.Value.ParameterName + suffix, item.Value.IsHidden)
                                    )
                                );
                            }).ToImmutableDictionary();

                            if (merger.animator != null)
                            {
                                Profiler.BeginSample("DeepCloneAnimator");
                                merger.animator = new DeepClone(_context.PluginBuildContext).DoClone(merger.animator);
                                Profiler.EndSample();

                                ProcessRuntimeAnimatorController(merger.animator, remap);
                            }

                            break;
                        }

                        case ModularAvatarMergeBlendTree merger:
                        {
                            var bt = merger.BlendTree as BlendTree;
                            if (bt != null)
                            {
                                merger.BlendTree = bt = new DeepClone(_context.PluginBuildContext).DoClone(bt);
                                ProcessBlendtree(bt, paramInfo.GetParameterRemappingsAt(obj));
                            }

                            break;
                        }

                        case ModularAvatarMenuInstaller installer:
                        {
                            if (installer.menuToAppend != null && installer.enabled)
                            {
                                ProcessMenuInstaller(installer, paramInfo.GetParameterRemappingsAt(obj));
                            }

                            break;
                        }

                        case ModularAvatarMenuItem menuItem:
                        {
                            var remaps = paramInfo.GetParameterRemappingsAt(obj);
                            if (menuItem.Control.parameter?.name != null &&
                                remaps.TryGetValue((ParameterNamespace.Animator, menuItem.Control.parameter.name), out var newVal))
                            {
                                menuItem.Control.parameter.name = newVal.ParameterName;
                            }

                            foreach (var subParam in menuItem.Control.subParameters ??
                                                     Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                            {
                                if (subParam?.name != null && remaps.TryGetValue((ParameterNamespace.Animator, subParam.name), out var subNewVal))
                                {
                                    subParam.name = subNewVal.ParameterName;
                                }
                            }

                            break;
                        }
                    }
                });
            }

            var mergedChildParams = ImmutableDictionary<string, ParameterInfo>.Empty;
            foreach (Transform child in obj.transform)
            {
                var childParams = WalkTree(child.gameObject);

                foreach (var kvp in childParams)
                {
                    var name = kvp.Key;
                    var info = kvp.Value;
                    if (mergedChildParams.TryGetValue(name, out var priorInfo))
                    {
                        priorInfo.MergeSibling(info);
                    }
                    else
                    {
                        mergedChildParams = mergedChildParams.SetItem(name, info);
                    }
                }
            }
            
            foreach (var kvp in mergedChildParams)
            {
                var name = kvp.Key;
                var info = kvp.Value;
                
                info.ResolvedParameter.nameOrPrefix = name;
                
                if (rv.TryGetValue(name, out var priorInfo))
                {
                    priorInfo.MergeChild(info);
                }
                else
                {
                    rv = rv.SetItem(name, info);
                }
            }

            return rv;
        }

        private void ProcessRuntimeAnimatorController(RuntimeAnimatorController controller,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remap)
        {
            if (controller is AnimatorController ac)
            {
                ProcessAnimator(ac, remap);
            }
            else if (controller is AnimatorOverrideController aoc)
            {
                var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(list);

                for (var i = 0; i < list.Count; i++)
                {
                    var kvp = list[i];
                    if (kvp.Value != null) ProcessClip(kvp.Value, remap);
                }

                ProcessRuntimeAnimatorController(aoc.runtimeAnimatorController, remap);
            }
        }

        private void ProcessMenuInstaller(ModularAvatarMenuInstaller installer,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> remapped =
                new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

            if (installer.menuToAppend == null) return;

            _context.PostProcessControls.Add(installer, control =>
            {
                control.parameter.name = remap(remaps, control.parameter.name);
                foreach (var subParam in control.subParameters)
                {
                    subParam.name = remap(remaps, subParam.name);
                }
            });
        }

        private void ProcessAnimator(AnimatorController controller,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            if (remaps.IsEmpty) return;
            
            var visited = new HashSet<AnimatorStateMachine>();
            var queue = new Queue<AnimatorStateMachine>();


            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (remaps.TryGetValue((ParameterNamespace.Animator, parameters[i].name), out var newName))
                {
                    parameters[i].name = newName.ParameterName;
                }
            }

            controller.parameters = parameters;

            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine != null)
                {
                    queue.Enqueue(layer.stateMachine);
                }
            }

            Profiler.BeginSample("Walk animator graph");
            while (queue.Count > 0)
            {
                var sm = queue.Dequeue();
                if (visited.Contains(sm)) continue;
                visited.Add(sm);

                foreach (var behavior in sm.behaviours)
                {
                    if (behavior is VRCAvatarParameterDriver driver)
                    {
                        ProcessDriver(driver, remaps);
                    }
                }

                foreach (var t in sm.anyStateTransitions)
                {
                    ProcessTransition(t, remaps);
                }

                foreach (var t in sm.entryTransitions)
                {
                    ProcessTransition(t, remaps);
                }

                foreach (var sub in sm.stateMachines)
                {
                    queue.Enqueue(sub.stateMachine);


                    foreach (var t in sm.GetStateMachineTransitions(sub.stateMachine))
                    {
                        ProcessTransition(t, remaps);
                    }
                }

                foreach (var st in sm.states)
                {
                    ProcessState(st.state, remaps);
                }
            }
            Profiler.EndSample();
        }

        private void ProcessState(AnimatorState state, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            state.mirrorParameter = remap(remaps, state.mirrorParameter);
            state.timeParameter = remap(remaps, state.timeParameter);
            state.speedParameter = remap(remaps, state.speedParameter);
            state.cycleOffsetParameter = remap(remaps, state.cycleOffsetParameter);

            foreach (var t in state.transitions)
            {
                ProcessTransition(t, remaps);
            }

            foreach (var behavior in state.behaviours)
            {
                if (behavior is VRCAvatarParameterDriver driver)
                {
                    ProcessDriver(driver, remaps);
                }
            }

            ProcessMotion(state.motion, remaps);
        }

        private void ProcessMotion(Motion motion,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            if (motion is BlendTree blendTree) ProcessBlendtree(blendTree, remaps);

            if (motion is AnimationClip clip) ProcessClip(clip, remaps);
        }

        private void ProcessClip(AnimationClip clip,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            var curveBindings = AnimationUtility.GetCurveBindings(clip);

            var bindingsToUpdate = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();

            foreach (var binding in curveBindings)
            {
                if (binding.path != "" || binding.type != typeof(Animator)) continue;
                if (remaps.TryGetValue((ParameterNamespace.Animator, binding.propertyName), out var newBinding))
                {
                    var curCurve = AnimationUtility.GetEditorCurve(clip, binding);

                    bindingsToUpdate.Add(binding);
                    newCurves.Add(null);

                    bindingsToUpdate.Add(new EditorCurveBinding
                    {
                        path = "",
                        type = typeof(Animator),
                        propertyName = newBinding.ParameterName
                    });
                    newCurves.Add(curCurve);
                }
            }

            if (bindingsToUpdate.Any())
            {
                AnimationUtility.SetEditorCurves(clip, bindingsToUpdate.ToArray(), newCurves.ToArray());

                // Workaround apparent unity bug where the clip's curves are not deleted
                for (var i = 0; i < bindingsToUpdate.Count; i++)
                    if (newCurves[i] == null && AnimationUtility.GetEditorCurve(clip, bindingsToUpdate[i]) != null)
                        AnimationUtility.SetEditorCurve(clip, bindingsToUpdate[i], newCurves[i]);
            }
        }

        private void ProcessBlendtree(BlendTree blendTree, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            blendTree.blendParameter = remap(remaps, blendTree.blendParameter);
            blendTree.blendParameterY = remap(remaps, blendTree.blendParameterY);

            var children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                var childMotion = children[i];
                ProcessMotion(childMotion.motion, remaps);

                childMotion.directBlendParameter = remap(remaps, childMotion.directBlendParameter);
                children[i] = childMotion;
            }

            blendTree.children = children;
        }

        private void ProcessDriver(VRCAvatarParameterDriver driver, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            var parameters = driver.parameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                p.name = remap(remaps, p.name);
                p.source = remap(remaps, p.source);
                p.destParam = remap(remaps, p.destParam);
                p.sourceParam = remap(remaps, p.sourceParam);
            }
        }

        private void ProcessTransition(AnimatorTransitionBase t, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            bool dirty = false;
            var conditions = t.conditions;

            for (int i = 0; i < conditions.Length; i++)
            {
                var cond = conditions[i];
                cond.parameter = remap(remaps, cond.parameter, ref dirty);
                conditions[i] = cond;
            }

            if (dirty) t.conditions = conditions;
        }

        private ImmutableDictionary<string, ParameterInfo> CollectParameters(ModularAvatarParameters p,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps
        )
        {
            var remapper = ParameterRenameMappings.Get(_context.PluginBuildContext);
            
            ImmutableDictionary<string, ParameterInfo> parameterInfos = ImmutableDictionary<string, ParameterInfo>.Empty;
            
            foreach (var param in p.parameters)
            {
                if (param.isPrefix) continue;
                
                var remapTo = param.nameOrPrefix;
                
                if (remaps.TryGetValue((ParameterNamespace.Animator, param.nameOrPrefix), out var mapping))
                {
                    remapTo = mapping.ParameterName;
                }

                ParameterConfig parameterConfig = param;
                parameterConfig.nameOrPrefix = remapTo;
                parameterConfig.remapTo = remapTo;
                var info = new ParameterInfo()
                {
                    ResolvedParameter = parameterConfig,
                };
                    
                if (parameterConfig.syncType != ParameterSyncType.NotSynced)
                {
                    info.TypeSources.Add(p);
                }
                    
                if (parameterConfig.HasDefaultValue)
                {
                    info.DefaultSources.Add(p);
                }
                    
                if (parameterInfos.TryGetValue(remapTo, out var existing))
                {
                    existing.MergeSibling(info);
                }
                else
                {
                    parameterInfos = parameterInfos.SetItem(remapTo, info);
                }
            }

            return parameterInfos;
        }

        // This is generic to simplify remapping parameter driver fields, some of which are 'object's.
        private T remap<T>(ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps, T x)
            where T : class
        {
            bool tmp = false;
            return remap(remaps, x, ref tmp);
        }

        private T remap<T>(ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps, T x, ref bool anyRemapped)
            where T : class
        {
            if (x is string s && remaps.TryGetValue((ParameterNamespace.Animator, s), out var newS))
            {
                anyRemapped = true;
                return (T) (object) newS.ParameterName;
            }

            return x;
        }
    }
}

#endif