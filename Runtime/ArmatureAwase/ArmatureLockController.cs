﻿using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.ui;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class ArmatureLockConfig
#if UNITY_EDITOR
        : UnityEditor.ScriptableSingleton<ArmatureLockConfig>
#endif
    {
#if !UNITY_EDITOR
        internal static ArmatureLockConfig instance { get; } = new ArmatureLockConfig();
#endif

        [SerializeField] private bool _globalEnable = true;

        internal bool GlobalEnable
        {
            get => _globalEnable;
            set
            {
                if (value == _globalEnable) return;

#if UNITY_EDITOR
                Undo.RecordObject(this, "Toggle Edit Mode Bone Sync");
                Menu.SetChecked(UnityMenuItems.TopMenu_EditModeBoneSync, value);
#endif

                _globalEnable = value;

                if (!value)
                {
                    // Run prepare one last time to dispose of lock structures
                    UpdateLoopController.InvokeArmatureLockPrepare();
                }
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.delayCall += () =>
            {
                Menu.SetChecked(UnityMenuItems.TopMenu_EditModeBoneSync, instance._globalEnable);
            };
        }

        [MenuItem(UnityMenuItems.TopMenu_EditModeBoneSync, false, UnityMenuItems.TopMenu_EditModeBoneSyncOrder)]
        static void ToggleBoneSync()
        {
            instance.GlobalEnable = !instance.GlobalEnable;
        }
#endif
    }

    internal class ArmatureLockController : IDisposable
    {
        private static long lastMovedFrame = 0;
        public static bool MovedThisFrame => Time.frameCount == lastMovedFrame;

        // Undo operations can reinitialize the MAMA component, which destroys critical lock controller state.
        // Avoid this issue by keeping a static reference to the controller for each MAMA component.
        private static Dictionary<ModularAvatarMergeArmature, ArmatureLockController>
            _controllers = new Dictionary<ModularAvatarMergeArmature, ArmatureLockController>();

        public delegate IReadOnlyList<(Transform, Transform)> GetTransformsDelegate();

        private readonly ModularAvatarMergeArmature _mama;
        private readonly GetTransformsDelegate _getTransforms;
        private ArmatureLock _lock;

        private bool GlobalEnable => ArmatureLockConfig.instance.GlobalEnable;
        private bool _updateActive;

        private bool UpdateActive
        {
            get => _updateActive;
            set
            {
                if (UpdateActive == value) return;
#if UNITY_EDITOR
                if (value)
                {
                    UpdateLoopController.OnArmatureLockPrepare += UpdateLoopPrepare;
                    UpdateLoopController.OnArmatureLockUpdate += UpdateLoopFinish;
                }
                else
                {
                    UpdateLoopController.OnArmatureLockPrepare -= UpdateLoopPrepare;
                    UpdateLoopController.OnArmatureLockUpdate -= UpdateLoopFinish;
                }

                _updateActive = value;
#endif
            }
        }

        private ArmatureLockMode _curMode, _mode;

        public ArmatureLockMode Mode
        {
            get => _mode;
            set
            {
                if (value == _mode) return;

                _mode = value;

                UpdateActive = true;
            }
        }

        private bool _enabled;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (Enabled == value) return;

                _enabled = value;
                if (_enabled) UpdateActive = true;
            }
        }

        public ArmatureLockController(ModularAvatarMergeArmature mama, GetTransformsDelegate getTransforms)
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += OnDomainUnload;
#endif

            this._mama = mama;
            this._getTransforms = getTransforms;
        }

        public static ArmatureLockController ForMerge(ModularAvatarMergeArmature mama,
            GetTransformsDelegate getTransforms)
        {
            if (!_controllers.TryGetValue(mama, out var controller))
            {
                controller = new ArmatureLockController(mama, getTransforms);
                _controllers[mama] = controller;
            }

            return controller;
        }

        public bool IsStable()
        {
            if (Mode == ArmatureLockMode.NotLocked) return true;

            if (_curMode == _mode && _lock?.IsStable() == true) return true;
            return RebuildLock() && (_lock?.IsStable() ?? false);
        }

        private void VoidPrepare()
        {
            UpdateLoopPrepare();
        }

        private void UpdateLoopFinish()
        {
            DoFinish();
        }

        internal bool Update()
        {
            UpdateLoopPrepare();
            return DoFinish();
        }

        private bool IsPrepared = false;

        private void UpdateLoopPrepare()
        {
            if (_mama == null || !_mama.gameObject.scene.IsValid())
            {
                UpdateActive = false;
                return;
            }

            if (!Enabled)
            {
                UpdateActive = false;
                _lock?.Dispose();
                _lock = null;
                return;
            }

            if (!GlobalEnable)
            {
                _lock?.Dispose();
                _lock = null;
                return;
            }

            if (_curMode == _mode)
            {
                _lock?.Prepare();
                IsPrepared = _lock != null;
            }
        }

        private bool DoFinish()
        {
            LockResult result;

            if (!GlobalEnable)
            {
                _lock?.Dispose();
                _lock = null;
                return true;
            }

            var wasPrepared = IsPrepared;
            IsPrepared = false;

            if (!Enabled) return true;

            if (_curMode == _mode)
            {
                if (!wasPrepared) _lock?.Prepare();
                result = _lock?.Execute() ?? LockResult.Failed;
                if (result == LockResult.Success)
                {
                    lastMovedFrame = Time.frameCount;
                }

                if (result != LockResult.Failed) return true;
            }

            if (!RebuildLock()) return false;

            _lock?.Prepare();
            result = (_lock?.Execute() ?? LockResult.Failed);

            return result != LockResult.Failed;
        }

        private bool RebuildLock()
        {
            _lock?.Dispose();
            _lock = null;

            var xforms = _getTransforms();
            if (xforms == null)
            {
                return false;
            }

            try
            {
                switch (Mode)
                {
                    case ArmatureLockMode.BidirectionalExact:
                        _lock = new BidirectionalArmatureLock(_getTransforms());
                        break;
                    case ArmatureLockMode.BaseToMerge:
                        _lock = new OnewayArmatureLock(_getTransforms());
                        break;
                    default:
                        UpdateActive = false;
                        break;
                }
            }
            catch (Exception)
            {
                _lock = null;
                return false;
            }

            _curMode = _mode;

            return true;
        }

        public void Dispose()
        {
            _lock?.Dispose();
            _lock = null;

            #if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnDomainUnload;
            #endif

            _controllers.Remove(_mama);
            UpdateActive = false;
        }

        private void OnDomainUnload()
        {
            // Unity 2019 does not call deferred callbacks before domain unload completes,
            // so we need to make sure to immediately destroy all our TransformAccessArrays.
            DeferDestroy.DestroyImmediate(this);
        }
    }
}