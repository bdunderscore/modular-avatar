#if MA_VRCSDK3_AVATARS

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ParameterRenameMappings
    {
        public static ParameterRenameMappings Get(ndmf.BuildContext ctx)
        {
            return ctx.GetState<ParameterRenameMappings>();
        }

        private readonly HashSet<string> usedNames = new();
        public Dictionary<(Component, ParameterNamespace, string), string> Remappings = new();

        private int internalParamIndex;

        public string Remap(Component p, ParameterNamespace ns, string s)
        {
            var tuple = (p, ns, s);

            if (Remappings.TryGetValue(tuple, out var mapping)) return mapping;

            var path = RuntimeUtil.AvatarRootPath(p.gameObject)!;
            string pathHash;
            using (var sha = SHA256.Create())
            {
                var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(path));

                StringBuilder sb = new();
                for (var i = 0; i < 6; i++)
                {
                    sb.AppendFormat("{0:x2}", hashBytes[i]);
                }

                pathHash = sb.ToString();
            }

            mapping = s + "$" + pathHash;

            for (var i = 0; !usedNames.Add(mapping); i++)
            {
                mapping = s + "$" + mapping + "." + i;
            }
            
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
            public List<Object> TypeSources = new List<Object>();
            public List<Object> DefaultSources = new List<Object>();
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
                    ResolvedParameter.localOnly = info.ResolvedParameter.localOnly;
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
            if (!context.AvatarDescriptor) return;

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
                Object.DestroyImmediate(p);
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
            var animServices = _context.PluginBuildContext.Extension<AnimatorServicesContext>();
            
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

                        case IVirtualizeAnimatorController virtualized:
                        {
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

                            var controller = animServices.ControllerContext.Controllers[virtualized];
                            if (controller != null)
                            {
                                ProcessVirtualAnimatorController(controller, remap);
                            }

                            break;
                        }

                        case ModularAvatarMergeBlendTree merger:
                        {
                            var motion = animServices.ControllerContext.GetVirtualizedMotion(merger);
                            if (motion is VirtualBlendTree bt)
                            {
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

                            if (menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                            {
                                _context.PostProcessControls.Add(menuItem, control =>
                                {
                                    control.parameter.name = remap(remaps, control.parameter.name);
                                    foreach (var subParam in control.subParameters)
                                    {
                                        subParam.name = remap(remaps, subParam.name);
                                    }
                                });
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

        private void ProcessVirtualAnimatorController(VirtualAnimatorController controller,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remap)
        {
            foreach (var node in controller.AllReachableNodes())
            {
                switch (node)
                {
                    case VirtualStateMachine vsm: ProcessStateMachine(vsm, remap); break;
                    case VirtualState vs: ProcessState(vs, remap); break;
                    case VirtualTransitionBase vt: ProcessTransition(vt, remap); break;
                    case VirtualClip vc: ProcessClip(vc, remap); break;
                    case VirtualBlendTree bt: ProcessBlendtree(bt, remap); break;
                }
            }

            var newParameters = controller.Parameters.Clear();

            foreach (var (name, parameter) in controller.Parameters)
            {
                if (remap.TryGetValue((ParameterNamespace.Animator, name), out var newParam))
                {
                    newParameters = newParameters.Add(newParam.ParameterName, parameter);
                }
                else
                {
                    newParameters = newParameters.Add(name, parameter);
                }
            }
            
            controller.Parameters = newParameters;
        }

        private void ProcessStateMachine(VirtualStateMachine vsm,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            foreach (var behavior in vsm.Behaviours)
            {
                if (behavior is VRCAvatarParameterDriver driver)
                {
                    ProcessDriver(driver, remaps);
                }
            }
        }

        private void ProcessState(VirtualState state, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            state.MirrorParameter = remap(remaps, state.MirrorParameter);
            state.TimeParameter = remap(remaps, state.TimeParameter);
            state.SpeedParameter = remap(remaps, state.SpeedParameter);
            state.CycleOffsetParameter = remap(remaps, state.CycleOffsetParameter);

            foreach (var behavior in state.Behaviours)
            {
                if (behavior is VRCAvatarParameterDriver driver)
                {
                    ProcessDriver(driver, remaps);
                }
            }
        }

        private void ProcessClip(VirtualClip clip,
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            var curveBindings = clip.GetFloatCurveBindings();

            var bindingsToUpdate = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();

            foreach (var binding in curveBindings)
            {
                if (binding.path != "" || binding.type != typeof(Animator)) continue;
                if (remaps.TryGetValue((ParameterNamespace.Animator, binding.propertyName), out var newBinding))
                {
                    var curCurve = clip.GetFloatCurve(binding);
                    var newECB = new EditorCurveBinding
                    {
                        path = "",
                        type = typeof(Animator),
                        propertyName = newBinding.ParameterName
                    };
                    
                    clip.SetFloatCurve(binding, null);
                    clip.SetFloatCurve(newECB, curCurve);
                }
            }
        }

        private void ProcessBlendtree(VirtualBlendTree blendTree, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            blendTree.BlendParameter = remap(remaps, blendTree.BlendParameter);
            blendTree.BlendParameterY = remap(remaps, blendTree.BlendParameterY);

            var children = blendTree.Children;
            foreach (var child in children)
            {
                child.DirectBlendParameter = remap(remaps, child.DirectBlendParameter);
            }
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

        private void ProcessTransition(VirtualTransitionBase t, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> remaps)
        {
            bool dirty = false;
            var conditions = t.Conditions
                .Select(cond =>
                {
                    cond.parameter = remap(remaps, cond.parameter, ref dirty);
                    return cond;
                })
                .ToImmutableList();
            t.Conditions = conditions;
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
                parameterConfig.localOnly = parameterConfig.localOnly || param.syncType == ParameterSyncType.NotSynced;
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