using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class RenameParametersHook
    {
        private const string DEFAULT_EXP_PARAMS_ASSET_GUID = "03a6d797deb62f0429471c4e17ea99a7";

        private BuildContext _context;

        private int internalParamIndex = 0;

        private Dictionary<string, VRCExpressionParameters.Parameter> _syncedParams =
            new Dictionary<string, VRCExpressionParameters.Parameter>();

        public void OnPreprocessAvatar(GameObject avatar, BuildContext context)
        {
            _context = context;

            _syncedParams.Clear();

            WalkTree(avatar, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty);

            SetExpressionParameters(avatar);
        }

        private void SetExpressionParameters(GameObject avatarRoot)
        {
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
            var parameters = expParams.parameters.ToList();

            foreach (var kvp in _syncedParams)
            {
                var name = kvp.Key;
                var param = kvp.Value;
                if (!knownParams.Contains(name))
                {
                    parameters.Add(param);
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

        private void WalkTree(
            GameObject obj,
            ImmutableDictionary<string, string> remaps,
            ImmutableDictionary<string, string> prefixRemaps
        )
        {
            var p = obj.GetComponent<ModularAvatarParameters>();
            if (p != null)
            {
                BuildReport.ReportingObject(p, () => ApplyRemappings(p, ref remaps, ref prefixRemaps));
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

                        case Animator anim:
                        {
                            if (willPurgeAnimators) break; // animator will be deleted in subsequent processing

                            // RuntimeAnimatorController may be AnimatorOverrideController, convert in case of AnimatorOverrideController
                            if (anim.runtimeAnimatorController is AnimatorOverrideController overrideController)
                            {
                                anim.runtimeAnimatorController = _context.ConvertAnimatorController(overrideController);
                            }

                            var controller = anim.runtimeAnimatorController as AnimatorController;
                            if (controller != null)
                            {
                                ProcessAnimator(ref controller, remaps);
                                anim.runtimeAnimatorController = controller;
                            }

                            break;
                        }

                        case ModularAvatarMergeAnimator merger:
                        {
                            // RuntimeAnimatorController may be AnimatorOverrideController, convert in case of AnimatorOverrideController
                            for (int i = 0; i < merger.animators.Count; i++)
                            {
                                if (merger.animators[i].animator is AnimatorOverrideController overrideController)
                                {
                                        merger.animators[i] = new AnimLayerData() { type = merger.animators[i].type, animator = _context.ConvertAnimatorController(overrideController)};
                                }

                                var controller = merger.animators[i].animator as AnimatorController;
                                if (controller != null)
                                {
                                    ProcessAnimator(ref controller, remaps);
                                    merger.animators[i] = new AnimLayerData() { type = merger.animators[i].type, animator = controller};
                                }
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

            foreach (Transform child in obj.transform)
            {
                WalkTree(child.gameObject, remaps, prefixRemaps);
            }
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
            if (!Util.IsTemporaryAsset(controller))
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

        private void ApplyRemappings(ModularAvatarParameters p,
            ref ImmutableDictionary<string, string> remaps,
            ref ImmutableDictionary<string, string> prefixRemaps
        )
        {
            foreach (var param in p.parameters)
            {
                bool doRemap = true;

                var remapTo = param.remapTo;
                if (param.internalParameter)
                {
                    remapTo = param.nameOrPrefix + "$$Internal_" + internalParamIndex++;
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

                if (!param.isPrefix && param.syncType != ParameterSyncType.NotSynced)
                {
                    AddSyncParam(param, remapTo);
                }
            }
        }

        private void AddSyncParam(
            ParameterConfig parameterConfig,
            string remapTo
        )
        {
            if (_syncedParams.ContainsKey(remapTo)) return;

            VRCExpressionParameters.ValueType type;
            switch (parameterConfig.syncType)
            {
                case ParameterSyncType.Bool:
                    type = VRCExpressionParameters.ValueType.Bool;
                    break;
                case ParameterSyncType.Float:
                    type = VRCExpressionParameters.ValueType.Float;
                    break;
                case ParameterSyncType.Int:
                    type = VRCExpressionParameters.ValueType.Int;
                    break;
                default: throw new Exception("Unknown sync type " + parameterConfig.syncType);
            }

            _syncedParams[remapTo] = new VRCExpressionParameters.Parameter
            {
                name = remapTo,
                valueType = type,
                defaultValue = parameterConfig.defaultValue,
                saved = parameterConfig.saved,
                networkSynced = !parameterConfig.localOnly
            };
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