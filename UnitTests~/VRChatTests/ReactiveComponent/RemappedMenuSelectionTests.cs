#if MA_VRCSDK3_AVATARS

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace UnitTests.ReactiveComponent
{
    public class RemappedMenuSelectionTests : TestBase
    {
        private ROSimulator? _window;
        private ComputeContext? _context;
        private ImmutableDictionary<string, ModularAvatarMenuItem?>? _previousMenuOverrides;
        private ImmutableDictionary<string, float>? _previousPropertyOverrides;

        public override void Setup()
        {
            base.Setup();
            _previousMenuOverrides = ROSimulator.MenuItemOverrides.Value;
            _previousPropertyOverrides = ROSimulator.PropertyOverrides.Value;
            ROSimulator.MenuItemOverrides.Value = ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty;
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
        }

        [UnityTearDown]
        public IEnumerator RestoreDebuggerState()
        {
            _context?.Invalidate();
            _context = null;
            if (_window != null) Object.DestroyImmediate(_window);
            _window = null;
            // Let this detached window's queued OnEnable/OnDisable callbacks finish before restoring shared state.
            yield return null;
            ROSimulator.MenuItemOverrides.Value = _previousMenuOverrides;
            ROSimulator.PropertyOverrides.Value = _previousPropertyOverrides;
            ComputeContext.FlushInvalidates();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DebuggerControlsUseEffectiveParameterForMenuSelection(bool ruleControl)
        {
            var root = CreateRoot("root");
            // Exercise the explicit analysis below without registering this temporary avatar in scene previews.
            // The global preview can otherwise retain it while TestBase destroys tracked objects before our
            // asynchronous window cleanup has finished.
            root.hideFlags = HideFlags.HideAndDontSave;
            AddParameter(root, "Shared", 1);
            AddParameter(root, "Local", 1);
            var scope = CreateChild(root, "remapped-scope");
            AddParameter(scope, "Local", 0, "Shared", false);
            var (local, localTarget) = CreateMenuToggle(scope, root, "remapped", "Local", false);
            var (_, sharedTarget) = CreateMenuToggle(root, root, "shared", "Shared", true);
            var (_, unrelatedTarget) = CreateMenuToggle(root, root, "unrelated", "Local", true);

            // Create an independent window without opening, replacing, or closing any user debugger window.
            _window = ScriptableObject.CreateInstance<ROSimulator>();
            _context = new ComputeContext("Remapped menu selection debugger test");
            SetField(_window, "_lastComputeContext", _context);
            var debugInfo = new VisualElement();
            var topControl = new VisualElement { name = "this-menu-override" };
            topControl.Add(new StateOverrideController());
            debugInfo.Add(topControl);
            SetField(_window, "_eDebugInfo", debugInfo);

            var condition = new MenuItemPreviewCondition(_context).ConditionForPreview(local, null,
                new Dictionary<string, float>());
            Assert.NotNull(condition);
            Assert.AreEqual("Shared", condition!.Parameter);

            StateOverrideController controller;
            if (ruleControl)
            {
                var rule = new ReactionRule(new DriveActiveState(localTarget, false));
                rule.ControllingConditions.Add(condition);
                var conditions = new VisualElement();
                InvokePrivate(_window, "BuildRuleConditionBlock", conditions, rule);
                controller = conditions.Q<StateOverrideController>();
            }
            else
            {
                InvokePrivate(_window, "BindOverrideToMenuItem", "this-menu-override", local);
                controller = topControl.Q<StateOverrideController>();
            }

            Assert.NotNull(controller.OnStateOverrideChanged);
            controller.OnStateOverrideChanged!(true);
            Assert.AreEqual(1, ROSimulator.MenuItemOverrides.Value!.Count);
            Assert.AreSame(local, ROSimulator.MenuItemOverrides.Value["Shared"]);
            AssertSelection(root, localTarget, sharedTarget, unrelatedTarget, true, false);

            controller.OnStateOverrideChanged(false);
            Assert.IsTrue(ROSimulator.MenuItemOverrides.Value.ContainsKey("Shared"));
            Assert.IsNull(ROSimulator.MenuItemOverrides.Value["Shared"]);
            AssertSelection(root, localTarget, sharedTarget, unrelatedTarget, false, false);

            controller.OnStateOverrideChanged(null);
            Assert.IsEmpty(ROSimulator.MenuItemOverrides.Value);
            AssertSelection(root, localTarget, sharedTarget, unrelatedTarget, true, true);
            Assert.AreEqual("Local", local.PortableControl.Parameter);
            Assert.IsFalse(local.isDefault);
            Assert.AreEqual(1, local.PortableControl.Value);
        }

        private static void AssertSelection(GameObject root, GameObject local, GameObject shared,
            GameObject unrelated, bool localSelected, bool sharedSelected)
        {
            var context = new ComputeContext("Remapped menu selection analysis test");
            try
            {
                var analyzer = new ReactiveObjectAnalyzer(context)
                {
                    ForceMenuItems = ROSimulator.MenuItemOverrides.Value!
                };
                var result = analyzer.Analyze(root);
                Assert.AreEqual(localSelected, result.InitialActions.ContainsKey(new ObjectActiveTarget(local)));
                Assert.AreEqual(sharedSelected, result.InitialActions.ContainsKey(new ObjectActiveTarget(shared)));
                Assert.IsTrue(result.InitialActions.ContainsKey(new ObjectActiveTarget(unrelated)),
                    "A raw name in another parameter scope must not share the remapped override.");
                Assert.IsTrue(local.activeSelf);
                Assert.IsTrue(shared.activeSelf);
                Assert.IsTrue(unrelated.activeSelf);
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        private static void AddParameter(GameObject owner, string name, float defaultValue, string remapTo = "",
            bool explicitDefault = true)
        {
            var parameters = owner.GetComponent<ModularAvatarParameters>()
                             ?? owner.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = name,
                remapTo = remapTo,
                defaultValue = defaultValue,
                hasExplicitDefaultValue = explicitDefault,
                syncType = ParameterSyncType.Bool,
            });
        }

        private (ModularAvatarMenuItem Item, GameObject Target) CreateMenuToggle(GameObject parent, GameObject root,
            string name, string parameter, bool isDefault)
        {
            var owner = CreateChild(parent, name);
            var item = owner.AddComponent<ModularAvatarMenuItem>();
            item.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
                value = 1,
            };
            item.automaticValue = false;
            item.isDefault = isDefault;
            var target = CreateChild(root, name + "-target");
            owner.AddComponent<ModularAvatarObjectToggle>().Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target),
                Active = false,
            });
            return (item, target);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name);
            field!.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, name);
            method!.Invoke(target, arguments);
        }
    }
}

#endif
