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

            return s + "$$Internal_" + internalParamIndex++;
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

        private int internalParamIndex = 0;

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

                ResolvedParameter.saved = info.ResolvedParameter.saved;
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
                
                ResolvedParameter.saved = ResolvedParameter.saved || info.ResolvedParameter.saved;
                
                encounterOrder = Math.Min(encounterOrder, info.encounterOrder);
            }
        }

        public void OnPreprocessAvatar(GameObject avatar, BuildContext context)
        {
            _context = context;

            var syncParams = WalkTree(avatar, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty);

            SetExpressionParameters(avatar, syncParams);

            _context.PluginBuildContext.GetState<DefaultValues>().InitialValueOverrides
                = syncParams.Where(p =>
                        p.Value.ResolvedParameter.HasDefaultValue &&
                        p.Value.ResolvedParameter.OverrideAnimatorDefaults)
                    .ToImmutableDictionary(p => p.Key, p => p.Value.ResolvedParameter.defaultValue);
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
     
            if (expParams.CalcTotalCost() > VRCExpressionParameters.MAX_PARAMETER_COST)
            {
                BuildReport.LogFatal("error.rename_params.too_many_synced_params", new[]
                    {
                        "" + expParams.CalcTotalCost(),
                        "" + VRCExpressionParameters.MAX_PARAMETER_COST,
                    }
                );
            }

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
            newParameter.networkSynced = parameter.networkSynced;
            newParameter.saved = parameter.saved || info.ResolvedParameter.saved;
            
            return newParameter;
        }

        private ImmutableDictionary<string, ParameterInfo> WalkTree(
            GameObject obj,
            ImmutableDictionary<string, string> remaps,
            ImmutableDictionary<string, string> prefixRemaps
        )
        {
            ImmutableDictionary<string, ParameterInfo> rv = ImmutableDictionary<string, ParameterInfo>.Empty;
            
            var p = obj.GetComponent<ModularAvatarParameters>();
            if (p != null)
            {
                rv = BuildReport.ReportingObject(p, () => ApplyRemappings(p, ref remaps, ref prefixRemaps));
            }

            var willPurgeAnimators = false;
            foreach (var merger in obj.GetComponents<ModularAvatarMergeAnimator>())
            {
                if (merger.deleteAttachedAnimator)
                {
                    willPurgeAnimators = true;
                    break;
                }
            }

            foreach (var component in obj.GetComponents<Component>())
            {
                BuildReport.ReportingObject(component, () =>
                {
                    switch (component)
                    {
                        case VRCPhysBone bone:
                        {
                            if (bone.parameter != null && prefixRemaps.TryGetValue(bone.parameter, out var newVal))
                            {
                                bone.parameter = newVal;
                            }

                            break;
                        }

                        case VRCContactReceiver contact:
                        {
                            if (contact.parameter != null && remaps.TryGetValue(contact.parameter, out var newVal))
                            {
                                contact.parameter = newVal;
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

                            var controller = merger.animator as AnimatorController;
                            if (controller != null)
                            {
                                ProcessAnimator(ref controller, remaps);
                                merger.animator = controller;
                            }

                            break;
                        }

                        case ModularAvatarMergeBlendTree merger:
                        {
                            var bt = merger.BlendTree as BlendTree;
                            if (bt != null)
                            {
                                merger.BlendTree = bt = new DeepClone(_context.PluginBuildContext).DoClone(bt);
                                ProcessBlendtree(bt, remaps);
                            }

                            break;
                        }

                        case ModularAvatarMenuInstaller installer:
                        {
                            if (installer.menuToAppend != null && installer.enabled)
                            {
                                ProcessMenuInstaller(installer, remaps);
                            }

                            break;
                        }

                        case ModularAvatarMenuItem menuItem:
                        {
                            if (menuItem.Control.parameter?.name != null &&
                                remaps.TryGetValue(menuItem.Control.parameter.name, out var newVal))
                            {
                                menuItem.Control.parameter.name = newVal;
                            }

                            foreach (var subParam in menuItem.Control.subParameters ??
                                                     Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                            {
                                if (subParam?.name != null && remaps.TryGetValue(subParam.name, out var subNewVal))
                                {
                                    subParam.name = subNewVal;
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
                var childParams = WalkTree(child.gameObject, remaps, prefixRemaps);

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
                
                var remappedName = remap(remaps, name);
                info.ResolvedParameter.nameOrPrefix = remappedName;
                
                if (rv.TryGetValue(remappedName, out var priorInfo))
                {
                    priorInfo.MergeChild(info);
                }
                else
                {
                    rv = rv.SetItem(remappedName, info);
                }
            }

            return rv;
        }

        private void ProcessMenuInstaller(ModularAvatarMenuInstaller installer,
            ImmutableDictionary<string, string> remaps)
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

        private void ProcessAnimator(ref AnimatorController controller, ImmutableDictionary<string, string> remaps)
        {
            var visited = new HashSet<AnimatorStateMachine>();
            var queue = new Queue<AnimatorStateMachine>();

            // Deep clone the animator
            if (!_context.PluginBuildContext.IsTemporaryAsset(controller))
            {
                controller = _context.DeepCloneAnimator(controller);
            }

            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (remaps.TryGetValue(parameters[i].name, out var newName))
                {
                    parameters[i].name = newName;
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
        }

        private void ProcessState(AnimatorState state, ImmutableDictionary<string, string> remaps)
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

            if (state.motion is BlendTree blendTree)
            {
                ProcessBlendtree(blendTree, remaps);
            }
        }

        private void ProcessBlendtree(BlendTree blendTree, ImmutableDictionary<string, string> remaps)
        {
            blendTree.blendParameter = remap(remaps, blendTree.blendParameter);
            blendTree.blendParameterY = remap(remaps, blendTree.blendParameterY);

            var children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                var childMotion = children[i];
                if (childMotion.motion is BlendTree subTree)
                {
                    ProcessBlendtree(subTree, remaps);
                }

                childMotion.directBlendParameter = remap(remaps, childMotion.directBlendParameter);
                children[i] = childMotion;
            }

            blendTree.children = children;
        }

        private void ProcessDriver(VRCAvatarParameterDriver driver, ImmutableDictionary<string, string> remaps)
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

        private void ProcessTransition(AnimatorStateTransition t, ImmutableDictionary<string, string> remaps)
        {
            var conditions = t.conditions;

            for (int i = 0; i < conditions.Length; i++)
            {
                var cond = conditions[i];
                cond.parameter = remap(remaps, cond.parameter);
                conditions[i] = cond;
            }

            t.conditions = conditions;
        }

        private void ProcessTransition(AnimatorTransition t, ImmutableDictionary<string, string> remaps)
        {
            var conditions = t.conditions;

            for (int i = 0; i < conditions.Length; i++)
            {
                var cond = conditions[i];
                cond.parameter = remap(remaps, cond.parameter);
                conditions[i] = cond;
            }

            t.conditions = conditions;
        }

        private ImmutableDictionary<string, ParameterInfo> ApplyRemappings(ModularAvatarParameters p,
            ref ImmutableDictionary<string, string> remaps,
            ref ImmutableDictionary<string, string> prefixRemaps
        )
        {
            var remapper = ParameterRenameMappings.Get(_context.PluginBuildContext);
            
            ImmutableDictionary<string, ParameterInfo> parameterInfos = ImmutableDictionary<string, ParameterInfo>.Empty;
            
            foreach (var param in p.parameters)
            {
                bool doRemap = true;

                var remapTo = param.remapTo;
                if (param.internalParameter)
                {
                    remapTo = remapper.Remap(p,
                        param.isPrefix ? ParameterNamespace.PhysBonesPrefix : ParameterNamespace.Animator,
                        param.nameOrPrefix);
                }
                else if (string.IsNullOrWhiteSpace(remapTo))
                {
                    doRemap = false;
                    remapTo = param.nameOrPrefix;
                }
                // Apply outer scope remaps (only if not an internal parameter)
                // Note that this continues the else chain above.
                else if (param.isPrefix && prefixRemaps.TryGetValue(remapTo, out var outerScope))
                {
                    remapTo = outerScope;
                }
                else if (remaps.TryGetValue(remapTo, out outerScope))
                {
                    remapTo = outerScope;
                }

                if (doRemap)
                {
                    if (param.isPrefix)
                    {
                        prefixRemaps = prefixRemaps.Add(param.nameOrPrefix, remapTo);
                        foreach (var suffix in ParameterPolicy.PhysBoneSuffixes)
                        {
                            var suffixKey = param.nameOrPrefix + suffix;
                            var suffixValue = remapTo + suffix;
                            remaps = remaps.SetItem(suffixKey, suffixValue);
                        }
                    }
                    else
                    {
                        remaps = remaps.SetItem(param.nameOrPrefix, remapTo);
                    }
                }

                if (!param.isPrefix)
                {
                    ParameterConfig parameterConfig = param;
                    parameterConfig.nameOrPrefix = remapTo;
                    parameterConfig.remapTo = null;
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
            }

            return parameterInfos;
        }

        // This is generic to simplify remapping parameter driver fields, some of which are 'object's.
        private T remap<T>(ImmutableDictionary<string, string> remaps, T x)
            where T : class
        {
            bool tmp = false;
            return remap(remaps, x, ref tmp);
        }

        private T remap<T>(ImmutableDictionary<string, string> remaps, T x, ref bool anyRemapped)
            where T : class
        {
            if (x is string s && remaps.TryGetValue(s, out var newS))
            {
                anyRemapped = true;
                return (T) (object) newS;
            }

            return x;
        }
    }
}

#endif