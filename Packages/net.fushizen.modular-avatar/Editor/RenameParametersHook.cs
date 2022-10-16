﻿using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace net.fushizen.modular_avatar.core.editor
{
    public class RenameParametersHook
    {
        private int internalParamIndex = 0;

        public void OnPreprocessAvatar(GameObject avatar)
        {
            WalkTree(avatar, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty);
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
                ApplyRemappings(p, ref remaps, ref prefixRemaps);
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
                        var controller = merger.animator as AnimatorController;
                        if (controller != null)
                        {
                            ProcessAnimator(ref controller, remaps);
                            merger.animator = controller;
                        }

                        break;
                    }

                    case ModularAvatarMenuInstaller installer:
                    {
                        if (installer.menuToAppend != null && installer.installTargetMenu != null)
                        {
                            ProcessMenu(ref installer.menuToAppend, remaps);
                        }

                        break;
                    }
                }
            }

            foreach (Transform child in obj.transform)
            {
                WalkTree(child.gameObject, remaps, prefixRemaps);
            }
        }

        private void ProcessMenu(ref VRCExpressionsMenu rootMenu, ImmutableDictionary<string, string> remaps)
        {
            Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> remapped =
                new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

            rootMenu = Transform(rootMenu);

            VRCExpressionsMenu Transform(VRCExpressionsMenu menu)
            {
                if (remapped.TryGetValue(menu, out var newMenu)) return newMenu;

                newMenu = Object.Instantiate(menu);
                AssetDatabase.CreateAsset(newMenu, Util.GenerateAssetPath());
                remapped[menu] = newMenu;

                foreach (var control in newMenu.controls)
                {
                    control.parameter.name = remap(remaps, control.parameter.name);
                    foreach (var subParam in control.subParameters)
                    {
                        subParam.name = remap(remaps, subParam.name);
                    }

                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        control.subMenu = Transform(control.subMenu);
                    }
                }

                return newMenu;
            }
        }

        private void ProcessAnimator(ref AnimatorController controller, ImmutableDictionary<string, string> remaps)
        {
            var visited = new HashSet<AnimatorStateMachine>();
            var queue = new Queue<AnimatorStateMachine>();

            // Deep clone the animator
            var merger = new AnimatorCombiner();
            merger.AddController("", controller, null);
            controller = merger.Finish();

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

        private void ApplyRemappings(
            ModularAvatarParameters p,
            ref ImmutableDictionary<string, string> remaps,
            ref ImmutableDictionary<string, string> prefixRemaps
        )
        {
            foreach (var param in p.parameters)
            {
                var remapTo = param.remapTo;
                if (param.internalParameter)
                {
                    remapTo = param.nameOrPrefix + "$$Internal_" + internalParamIndex++;
                }
                else if (string.IsNullOrWhiteSpace(remapTo)) continue;
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

                if (param.isPrefix)
                {
                    prefixRemaps = prefixRemaps.Add(param.nameOrPrefix, remapTo);
                    foreach (var suffix in ParameterPolicy.PhysBoneSuffixes)
                    {
                        var suffixKey = param.nameOrPrefix + suffix;
                        var suffixValue = remapTo + suffix;
                        remaps = remaps.Add(suffixKey, suffixValue);
                    }
                }
                else
                {
                    remaps = remaps.Add(param.nameOrPrefix, remapTo);
                }
            }
        }

        // This is generic to simplify remapping parameter driver fields, some of which are 'object's.
        private T remap<T>(ImmutableDictionary<string, string> remaps, T x)
            where T : class
        {
            if (x is string s && remaps.TryGetValue(s, out var newS))
            {
                return (T) (object) newS;
            }

            return x;
        }
    }
}