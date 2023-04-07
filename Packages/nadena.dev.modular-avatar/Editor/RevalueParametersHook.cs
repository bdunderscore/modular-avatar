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
    internal class RevalueParametersHook
    {
        private BuildContext _context;

        private int internalParamIndex = 0;

        private Dictionary<string, VRCExpressionParameters.Parameter> _syncedParams =
            new Dictionary<string, VRCExpressionParameters.Parameter>();

        private struct RevalueKey
        {
            public string ParameterName;
            public float OriginalValue;
        }

        private class RevalueComparer : IEqualityComparer<RevalueKey>
        {
            public bool Equals(RevalueKey x, RevalueKey y)
            {
                return x.ParameterName == y.ParameterName && Mathf.Abs(x.OriginalValue - y.OriginalValue) < 0.00001;
            }

            public int GetHashCode(RevalueKey obj)
            {
                unchecked
                {
                    return ((obj.ParameterName != null ? obj.ParameterName.GetHashCode() : 0) * 397) ^
                           ((int)(obj.OriginalValue * 100000)).GetHashCode();
                }
            }
        }

        private static readonly ImmutableDictionary<RevalueKey, float> RevaluesEmpty =
            ImmutableDictionary.CreateBuilder<RevalueKey, float>(new RevalueComparer()).ToImmutableDictionary();

        public void OnPreprocessAvatar(GameObject avatar, BuildContext context)
        {
            _context = context;

            _syncedParams.Clear();

            WalkTree(avatar, RevaluesEmpty);
        }

        private void WalkTree(
            GameObject obj,
            ImmutableDictionary<RevalueKey, float> revalues
        )
        {
            var p = obj.GetComponent<ModularAvatarParametersRevalue>();
            if (p != null)
            {
                BuildReport.ReportingObject(p, () => ApplyRevaluings(p, ref revalues));
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
                        case VRCContactReceiver contact:
                        {
                            if (contact.parameter != null && revalues.TryGetValue(new RevalueKey()
                                {
                                    ParameterName = contact.parameter,
                                    OriginalValue = contact.paramValue
                                },
                                out var newVal
                            ))
                            {
                                contact.paramValue = newVal;
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
                                ProcessAnimator(ref controller, revalues);
                                anim.runtimeAnimatorController = controller;
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
                                ProcessAnimator(ref controller, revalues);
                                merger.animator = controller;
                            }

                            break;
                        }

                        case ModularAvatarMenuInstaller installer:
                        {
                            if (installer.menuToAppend != null && installer.enabled)
                            {
                                ProcessMenu(ref installer.menuToAppend, revalues);
                            }

                            break;
                        }
                    }
                });
            }

            foreach (Transform child in obj.transform)
            {
                WalkTree(child.gameObject, revalues);
            }
        }

        private void ProcessMenu(ref VRCExpressionsMenu rootMenu, ImmutableDictionary<RevalueKey, float> revalues)
        {
            Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> revalued =
                new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

            rootMenu = Transform(rootMenu);

            VRCExpressionsMenu Transform(VRCExpressionsMenu menu)
            {
                if (menu == null) return null;

                if (revalued.TryGetValue(menu, out var newMenu)) return newMenu;

                newMenu = Object.Instantiate(menu);
                _context.SaveAsset(newMenu);
                revalued[menu] = newMenu;
                ClonedMenuMappings.Add(menu, newMenu);

                foreach (var control in newMenu.controls)
                {
                    control.value = revalue(revalues, control.parameter.name, control.value);

                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        control.subMenu = Transform(control.subMenu);
                    }
                }

                return newMenu;
            }
        }

        private void ProcessAnimator(ref AnimatorController controller, ImmutableDictionary<RevalueKey, float> revalues)
        {
            var visited = new HashSet<AnimatorStateMachine>();
            var queue = new Queue<AnimatorStateMachine>();

            // Deep clone the animator
            if (!Util.IsTemporaryAsset(controller))
            {
                controller = _context.DeepCloneAnimator(controller);
            }

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
                        ProcessDriver(driver, revalues);
                    }
                }

                foreach (var t in sm.anyStateTransitions)
                {
                    ProcessTransition(t, revalues);
                }

                foreach (var t in sm.entryTransitions)
                {
                    ProcessTransition(t, revalues);
                }

                foreach (var sub in sm.stateMachines)
                {
                    queue.Enqueue(sub.stateMachine);


                    foreach (var t in sm.GetStateMachineTransitions(sub.stateMachine))
                    {
                        ProcessTransition(t, revalues);
                    }
                }

                foreach (var st in sm.states)
                {
                    ProcessState(st.state, revalues);
                }
            }
        }

        private void ProcessState(AnimatorState state, ImmutableDictionary<RevalueKey, float> revalues)
        {
            state.mirrorParameterActive =
                Mathf.Abs(revalue(revalues, state.mirrorParameter, state.mirrorParameterActive ? 1f : 0f)) >
                float.Epsilon;
            state.timeParameterActive =
                Mathf.Abs(revalue(revalues, state.timeParameter, state.timeParameterActive ? 1f : 0f)) > float.Epsilon;
            state.speedParameterActive =
                Mathf.Abs(revalue(revalues, state.speedParameter, state.speedParameterActive ? 1f : 0f)) >
                float.Epsilon;
            state.cycleOffsetParameterActive =
                Mathf.Abs(revalue(revalues, state.cycleOffsetParameter, state.cycleOffsetParameterActive ? 1f : 0f)) >
                float.Epsilon;

            foreach (var t in state.transitions)
            {
                ProcessTransition(t, revalues);
            }

            foreach (var behavior in state.behaviours)
            {
                if (behavior is VRCAvatarParameterDriver driver)
                {
                    ProcessDriver(driver, revalues);
                }
            }

            if (state.motion is BlendTree blendTree)
            {
                ProcessBlendtree(blendTree, revalues);
            }
        }

        private void ProcessBlendtree(BlendTree blendTree, ImmutableDictionary<RevalueKey, float> revalues)
        {
            var children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                var childMotion = children[i];

                switch (blendTree.blendType)
                {
                    case BlendTreeType.Simple1D:
                        childMotion.threshold = revalue(revalues, blendTree.blendParameter, childMotion.threshold);
                        break;
                    case BlendTreeType.SimpleDirectional2D:
                    case BlendTreeType.FreeformDirectional2D:
                    case BlendTreeType.FreeformCartesian2D:
                        childMotion.position = new Vector2()
                        {
                            x = revalue(revalues, blendTree.blendParameter, childMotion.position.x),
                            y = revalue(revalues, blendTree.blendParameterY, childMotion.position.y)
                        };
                        break;
                    case BlendTreeType.Direct:
                        // childMotion.directBlendParameter
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (childMotion.motion is BlendTree subTree)
                {
                    ProcessBlendtree(subTree, revalues);
                }
                
                children[i] = childMotion;
            }
        }

        private void ProcessDriver(VRCAvatarParameterDriver driver, ImmutableDictionary<RevalueKey, float> revalues)
        {
            var parameters = driver.parameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                p.value = revalue(revalues, p.name, p.value);
            }
        }

        private void ProcessTransition(AnimatorStateTransition t, ImmutableDictionary<RevalueKey, float> revalues)
        {
            var conditions = t.conditions;

            for (int i = 0; i < conditions.Length; i++)
            {
                var cond = conditions[i];
                cond.threshold = revalue(revalues, cond.parameter, cond.threshold);
                conditions[i] = cond;
            }

            t.conditions = conditions;
        }

        private void ProcessTransition(AnimatorTransition t, ImmutableDictionary<RevalueKey, float> revalues)
        {
            var conditions = t.conditions;

            for (int i = 0; i < conditions.Length; i++)
            {
                var cond = conditions[i];
                cond.threshold = revalue(revalues, cond.parameter, cond.threshold);
                conditions[i] = cond;
            }

            t.conditions = conditions;
        }

        private void ApplyRevaluings(ModularAvatarParametersRevalue p,
            ref ImmutableDictionary<RevalueKey, float> revalues
        )
        {
            foreach (var param in p.parameters)
            {
                bool doRevalue = true;

                if (doRevalue)
                {
                    revalues = revalues.SetItem(
                        new RevalueKey() { ParameterName = param.ParameterName, OriginalValue = param.OriginalValue },
                        param.NewValue);
                }
            }
        }

        // This is generic to simplify revalue parameter driver fields, some of which are 'object's.
        private float revalue<T>(ImmutableDictionary<RevalueKey, float> revalues, T x, float v)
            where T : class
        {
            if (x is string s &&
                revalues.TryGetValue(new RevalueKey() { ParameterName = s, OriginalValue = v }, out var newV))
            {
                return newV;
            }

            return v;
        }
    }
}