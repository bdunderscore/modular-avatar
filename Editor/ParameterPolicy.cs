﻿#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal struct DetectedParameter
    {
        public string OriginalName;
        public bool IsPrefix;
        public ParameterSyncType syncType;
        public float defaultValue;
        public bool saved;

        public string MapKey => IsPrefix ? OriginalName + "*" : OriginalName;
    }

    internal static class ParameterPolicy
    {
        /// <summary>
        /// Parameters predefined by the VRChat SDK which should not be offered as remappable.
        /// </summary>
        public static ImmutableHashSet<string> VRCSDKParameters = new string[]
        {
            "IsLocal",
            "Viseme",
            "Voice",
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
            "AngularY",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "VelocityMagnitude",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "Earmuffs",
        }.ToImmutableHashSet();

        public static ImmutableList<string> PhysBoneSuffixes = new string[]
        {
            "_IsGrabbed",
            "_Angle",
            "_Stretch",
            "_IsPosed",
            "_Squish",
        }.ToImmutableList();

        public static ImmutableDictionary<string, DetectedParameter> ProbeParameters(GameObject root)
        {
            Dictionary<string, DetectedParameter> parameters = new Dictionary<string, DetectedParameter>();

            WalkTree(ref parameters, root, false);

            CleanPhysBoneParams(parameters);

            return parameters.ToImmutableDictionary();
        }

        private static void WalkTree(ref Dictionary<string, DetectedParameter> parameters, GameObject root,
            bool applyRemappings)
        {
            ModularAvatarParameters parametersComponent = null;

            foreach (var component in root.GetComponents<Component>())
            {
                switch (component)
                {
                    case ModularAvatarParameters p:
                    {
                        parametersComponent = p;
                        break;
                    }

                    case VRCPhysBone bone:
                    {
                        if (!string.IsNullOrWhiteSpace(bone.parameter))
                        {
                            var param = new DetectedParameter()
                            {
                                OriginalName = bone.parameter,
                                IsPrefix = true,
                            };

                            parameters[param.MapKey] = param;
                        }

                        break;
                    }

                    case VRCContactReceiver contact:
                    {
                        if (!string.IsNullOrWhiteSpace(contact.parameter))
                        {
                            var param = new DetectedParameter()
                            {
                                OriginalName = contact.parameter,
                                IsPrefix = false,
                            };

                            parameters[param.MapKey] = param;
                        }

                        break;
                    }

                    case Animator anim:
                    {
                        WalkAnimator(parameters, anim.runtimeAnimatorController as AnimatorController);
                        break;
                    }

                    case ModularAvatarMenuInstaller installer:
                    {
                        WalkMenu(parameters, installer.menuToAppend, new HashSet<VRCExpressionsMenu>());
                        break;
                    }

                    case ModularAvatarMergeAnimator merger:
                    {
                        WalkAnimator(parameters, merger.animator as AnimatorController);
                        break;
                    }
                }
            }

            foreach (Transform child in root.transform)
            {
                WalkTree(ref parameters, child.gameObject, true);
            }

            if (parametersComponent != null && applyRemappings)
            {
                CleanPhysBoneParams(parameters);
                ApplyRemappings(ref parameters, parametersComponent);
            }
        }

        private static void WalkMenu(Dictionary<string, DetectedParameter> parameters, VRCExpressionsMenu menu,
            HashSet<VRCExpressionsMenu> visited)
        {
            if (menu == null || visited.Contains(menu)) return;
            visited.Add(menu);

            void AddParam(string name)
            {
                var param = new DetectedParameter()
                {
                    OriginalName = name,
                    IsPrefix = false
                };
                parameters[param.MapKey] = param;
            }

            foreach (var control in menu.controls)
            {
                if (!string.IsNullOrWhiteSpace(control.parameter.name))
                {
                    AddParam(control.parameter.name);
                }

                if (control.subParameters != null)
                {
                    foreach (var subParam in control.subParameters)
                    {
                        if (!string.IsNullOrWhiteSpace(subParam.name))
                        {
                            AddParam(subParam.name);
                        }
                    }
                }

                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    WalkMenu(parameters, control.subMenu, visited);
                }
            }
        }

        private static void CleanPhysBoneParams(Dictionary<string, DetectedParameter> parameters)
        {
            var physBonePrefixes = parameters.Values.Where(p => p.IsPrefix)
                .Select(p => p.OriginalName)
                .ToArray();

            foreach (var prefix in physBonePrefixes)
            {
                foreach (var suffix in PhysBoneSuffixes)
                {
                    var key = prefix + suffix;
                    if (parameters.ContainsKey(key))
                    {
                        parameters.Remove(key);
                    }
                }
            }
        }

        private static void WalkAnimator(
            Dictionary<string, DetectedParameter> parameters,
            AnimatorController controller
        )
        {
            if (controller == null) return;

            foreach (var parameter in controller.parameters)
            {
                AddDetectedParameter(parameter.name);
            }

            HashSet<AnimatorStateMachine> visitedStateMachines = new HashSet<AnimatorStateMachine>();
            Queue<AnimatorStateMachine> stateMachines =
                new Queue<AnimatorStateMachine>(controller.layers.Select(l => l.stateMachine));

            while (stateMachines.Count > 0)
            {
                var sm = stateMachines.Dequeue();
                if (visitedStateMachines.Contains(sm)) continue;
                visitedStateMachines.Add(sm);

                foreach (var subStateMachine in sm.stateMachines)
                {
                    stateMachines.Enqueue(subStateMachine.stateMachine);
                }

                foreach (var state in sm.states)
                {
                    foreach (var behavior in state.state.behaviours)
                    {
                        if (behavior is VRCAvatarParameterDriver driver)
                        {
                            foreach (var p in driver.parameters)
                            {
                                AddDetectedParameter(p.name);
                                AddDetectedParameter(p.source);
                                AddDetectedParameter(p.destParam as string);
                                AddDetectedParameter(p.sourceParam as string);
                            }
                        }
                    }
                }
            }

            void AddDetectedParameter(string parameterName)
            {
                if (string.IsNullOrEmpty(parameterName) || VRCSDKParameters.Contains(parameterName)) return;
                var param = new DetectedParameter()
                {
                    OriginalName = parameterName,
                    IsPrefix = false
                };
                parameters[param.MapKey] = param;
            }
        }

        private static void ApplyRemappings(ref Dictionary<string, DetectedParameter> parameters,
            ModularAvatarParameters parametersComponent)
        {
            Dictionary<string, DetectedParameter> newParams = new Dictionary<string, DetectedParameter>();

            foreach (var map in parametersComponent.parameters)
            {
                var dictKey = map.nameOrPrefix + (map.isPrefix ? "*" : "");
                if (!parameters.ContainsKey(dictKey))
                {
                    // We're trying to remap a parameter that's not actually used by this subtree.
                    // If it's synced, we'll propagate it (as it could be used by a menu as a no-op?)
                    // but otherwise disregard it.
                    if (map.internalParameter || map.syncType == ParameterSyncType.NotSynced) continue;
                    var param = new DetectedParameter()
                    {
                        OriginalName = map.remapTo,
                        IsPrefix = map.isPrefix,
                        syncType = map.syncType,
                        defaultValue = map.defaultValue,
                        saved = map.saved,
                    };
                    newParams[param.MapKey] = param;
                    continue;
                }

                if (map.internalParameter)
                {
                    parameters.Remove(dictKey);
                }
                else
                {
                    var exposedName = !string.IsNullOrWhiteSpace(map.remapTo) ? map.remapTo : map.nameOrPrefix;
                    var param = parameters[dictKey];
                    param.OriginalName = exposedName;
                    param.syncType = map.syncType;
                    param.defaultValue = map.defaultValue;
                    param.saved = map.saved;
                    newParams[param.MapKey] = param;
                    parameters.Remove(dictKey);
                }
            }

            // TODO - warn of overlap? could be intentional...
            foreach (var param in parameters)
            {
                newParams[param.Key] = param.Value;
            }

            parameters = newParams;
        }
    }
}

#endif