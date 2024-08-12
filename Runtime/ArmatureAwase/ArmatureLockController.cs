#region

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.ui;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion


namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class ArmatureLockConfig
#if UNITY_EDITOR
        : ScriptableSingleton<ArmatureLockConfig>
#endif
    {
#if !UNITY_EDITOR
        internal static ArmatureLockConfig instance { get; } = new ArmatureLockConfig();
#endif

        [SerializeField] private bool _globalEnable = true;
        internal event Action OnGlobalEnableChange;

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

                OnGlobalEnableChange?.Invoke();
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
        public delegate IReadOnlyList<(Transform, Transform)> GetTransformsDelegate();

        private static long lastMovedFrame = 0;

        // Undo operations can reinitialize the MAMA component, which destroys critical lock controller state.
        // Avoid this issue by keeping a static reference to the controller for each MAMA component.
        private static Dictionary<ModularAvatarMergeArmature, ArmatureLockController>
            _controllers = new Dictionary<ModularAvatarMergeArmature, ArmatureLockController>();

        private readonly GetTransformsDelegate _getTransforms;

        private readonly ModularAvatarMergeArmature _mama;

        private ArmatureLockMode _curMode, _mode;

        private bool _enabled;
        private ArmatureLockJob _job;

        public ArmatureLockController(ModularAvatarMergeArmature mama, GetTransformsDelegate getTransforms)
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += OnDomainUnload;
#endif

            _mama = mama;
            _getTransforms = getTransforms;
        }

        public static bool MovedThisFrame => Time.frameCount == lastMovedFrame;

        private bool GlobalEnable => ArmatureLockConfig.instance.GlobalEnable;

        public ArmatureLockMode Mode
        {
            get => _mode;
            set
            {
                if (value == _mode && _job != null) return;

                _mode = value;

                RebuildLock();
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (Enabled == value) return;

                _enabled = value;

                RebuildLock();
            }
        }

        public void Dispose()
        {
            _job?.Dispose();
            _job = null;

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnDomainUnload;
#endif

            _controllers.Remove(_mama);
        }

        internal event Action WhenUnstable;

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

        internal void CheckLockJob()
        {
            if (_mama == null || !_mama.gameObject.scene.IsValid() || !Enabled)
            {
                _job?.Dispose();
                return;
            }

            if (_curMode != _mode || _job == null || !_job.IsValid)
            {
                if (_job != null && _job.FailedOnStartup)
                {
                    WhenUnstable?.Invoke();
                    Enabled = false;
                    _job?.Dispose();
                    return;
                }

                RebuildLock();
            }
        }

        private bool RebuildLock()
        {
            _job?.Dispose();
            _job = null;

            var xforms = _getTransforms();
            if (xforms == null)
            {
                return false;
            }

#if UNITY_EDITOR
            if (xforms.Count == 0 || EditorUtility.IsPersistent(xforms[0].Item1))
                // Bail out if we're trying to lock a prefab...
                return true;
#endif

            try
            {
                switch (Mode)
                {
                    case ArmatureLockMode.BidirectionalExact:
                        _job = BidirectionalArmatureLockOperator.Instance.RegisterLock(xforms);
                        break;
                    case ArmatureLockMode.BaseToMerge:
                        _job = OnewayArmatureLockOperator.Instance.RegisterLock(xforms);
                        break;
                    default:
                        Enabled = false;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _job = null;
                return false;
            }

            if (_job != null)
            {
#if UNITY_EDITOR
                _job.OnInvalidation += () => { EditorApplication.delayCall += CheckLockJob; };
#endif
            }

            _curMode = _mode;

            return true;
        }

        private void OnDomainUnload()
        {
            // Unity 2019 does not call deferred callbacks before domain unload completes,
            // so we need to make sure to immediately destroy all our TransformAccessArrays.
            DeferDestroy.DestroyImmediate(this);
        }
    }
}