#nullable enable

#if MA_VRCSDK3_AVATARS
using nadena.dev.modular_avatar.core.menu;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core
{
    public enum SubmenuSource
    {
        MenuAsset,
        Children,
    }

    [Serializable]
    public enum PortableControlType
    {
        Toggle = 102,
        Button = 101,
        SubMenu = 103,
        RadialPuppet = 203,
        TwoAxisPuppet = 201,
        FourAxisPuppet = 202
    }

#if MA_VRCSDK3_AVATARS
    public static class PortableControlConversions
    {
        public static PortableControlType ToPortable(this VRCExpressionsMenu.Control.ControlType type)
        {
            return type switch
            {
                VRCExpressionsMenu.Control.ControlType.Toggle => PortableControlType.Toggle,
                VRCExpressionsMenu.Control.ControlType.Button => PortableControlType.Button,
                VRCExpressionsMenu.Control.ControlType.SubMenu => PortableControlType.SubMenu,
                VRCExpressionsMenu.Control.ControlType.RadialPuppet => PortableControlType.RadialPuppet,
                VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet => PortableControlType.TwoAxisPuppet,
                VRCExpressionsMenu.Control.ControlType.FourAxisPuppet => PortableControlType.FourAxisPuppet,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static VRCExpressionsMenu.Control.ControlType ToVRCSDK(this PortableControlType type)
        {
            return type switch
            {
                PortableControlType.Toggle => VRCExpressionsMenu.Control.ControlType.Toggle,
                PortableControlType.Button => VRCExpressionsMenu.Control.ControlType.Button,
                PortableControlType.SubMenu => VRCExpressionsMenu.Control.ControlType.SubMenu,
                PortableControlType.RadialPuppet => VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                PortableControlType.TwoAxisPuppet => VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
                PortableControlType.FourAxisPuppet => VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
#endif

    [Serializable]
    public struct PortableLabel
    {
        [SerializeField] internal string name;
        [SerializeField] internal Texture2D icon;

        public string Name
        {
            get => name;
            set => name = value;
        }

        public Texture2D Icon
        {
            get => icon;
            set => icon = value;
        }
    }

    [Serializable]
    internal struct SerializedParameter
    {
        public string name;
    }

    [Serializable]
    public class PortableMenuControl
    {
        internal ModularAvatarMenuItem? _backingMenuItem = null;

        internal ModularAvatarMenuItem BackingMenuItem =>
            _backingMenuItem ?? throw new NullReferenceException("BackingMenuItem is not set.");

        internal PortableMenuControl()
        {
        }

#if MA_VRCSDK3_AVATARS

        public VRCExpressionsMenu.Control CloneToVRCSDK()
        {
            return new VRCExpressionsMenu.Control
            {
                icon = BackingControl.icon,
                type = BackingControl.type,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = BackingControl.parameter?.name ?? string.Empty },
                value = BackingControl.value,
                subMenu = BackingControl.subMenu,
                subParameters = (BackingControl.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                    .Select(p => new VRCExpressionsMenu.Control.Parameter { name = p.name }).ToArray(),
                labels = (BackingControl.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>())
                    .Select(l => new VRCExpressionsMenu.Control.Label
                    { name = l.name, icon = l.icon }).ToArray(),
                name = string.IsNullOrEmpty(BackingMenuItem.label)
                    ? BackingMenuItem.gameObject.name
                    : BackingMenuItem.label
            };
        }

        public void SetFrom(VRCExpressionsMenu.Control control)
        {
            BackingControl.icon = control.icon;
            BackingControl.type = control.type;
            BackingControl.parameter = control.parameter;
            BackingControl.value = control.value;
            BackingControl.subMenu = control.subMenu;
            BackingControl.subParameters = (control.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                .Select(p => new VRCExpressionsMenu.Control.Parameter { name = p.name }).ToArray();
            BackingControl.labels = (control.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>())
                .Select(l => new VRCExpressionsMenu.Control.Label { name = l.name, icon = l.icon }).ToArray();
            BackingMenuItem.label = control.name;
        }

        internal VRCExpressionsMenu.Control BackingControl
        {
            get
            {
                if (BackingMenuItem.Control == null)
                {
                    BackingMenuItem.Control = new VRCExpressionsMenu.Control();
                }

                return BackingMenuItem.Control;
            }
        }

        public Texture2D? Icon
        {
            get => BackingControl.icon;
            set => BackingControl.icon = value;
        }

        public PortableControlType Type
        {
            get => BackingControl.type.ToPortable();
            set => BackingControl.type = value.ToVRCSDK();
        }

        public string Parameter
        {
            get => BackingControl.parameter?.name ?? string.Empty;
            set => BackingControl.parameter = new VRCExpressionsMenu.Control.Parameter { name = value };
        }

        public float Value
        {
            get => BackingControl.value;
            set => BackingControl.value = value;
        }

        // Not represented (unused?): Style

        public Object? VRChatSubMenu
        {
            get => BackingControl.subMenu;
            set
            {
                BackingControl.subMenu = (VRCExpressionsMenu?)value;
            }
        }

        public ImmutableList<string> SubParameters
        {
            get => (BackingControl.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                .Select(p => p.name).ToImmutableList();
            set => BackingControl.subParameters =
                value.Select(name => new VRCExpressionsMenu.Control.Parameter { name = name }).ToArray();
        }

        public ImmutableList<PortableLabel> Labels
        {
            get => (BackingControl.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>())
                .Select(l => new PortableLabel { name = l.name, icon = l.icon })
                .ToImmutableList();
            set => BackingControl.labels =
                value.Select(l => new VRCExpressionsMenu.Control.Label { name = l.Name, icon = l.Icon }).ToArray();
        }

#else
        [SerializeField]
        private Texture2D? icon;
        public Texture2D? Icon
        {
            get => icon;
            set => icon = value;
        }
        [SerializeField]
        private PortableControlType type;
        public PortableControlType Type
        {
            get => type;
            set => type = value;
        }

        [SerializeField]
        private SerializedParameter parameter;
        public string Parameter
        {
            get => parameter.name;
            set => parameter.name = value;
        }

        [SerializeField]
        private float value = 1f;
        public float Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private UnityEngine.Object? subMenu = null;
        public UnityEngine.Object? VRChatSubMenu
        {
            get => subMenu;
            set => subMenu = value;
        }

        [SerializeField]
        private SerializedParameter[] subParameters = Array.Empty<SerializedParameter>();
        public ImmutableList<string> SubParameters
        {
            get => subParameters.Select(p => p.name).ToImmutableList();
            set => subParameters = value.Select(name => new SerializedParameter { name = name }).ToArray();
        }

        [SerializeField] private PortableLabel[] labels = Array.Empty<PortableLabel>();
        public ImmutableList<PortableLabel> Labels
        {
            get => labels.ToImmutableList();
            set => labels = value.ToArray();
        }
#endif
    }

    [AddComponentMenu("Modular Avatar/MA Menu Item")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/menu-item?lang=auto")]
    public class ModularAvatarMenuItem : AvatarTagComponent
#if MA_VRCSDK3_AVATARS
        , MenuSource
#endif
    {
#if MA_VRCSDK3_AVATARS
        public VRCExpressionsMenu.Control? Control;
#endif

        #if !MA_VRCSDK3_AVATARS
        [SerializeField]
        private PortableMenuControl Control = new();
        public PortableMenuControl PortableControl => Control;
        
        #else
        public PortableMenuControl PortableControl { get; }
        #endif
        
        public SubmenuSource MenuSource;

        public GameObject? menuSource_otherObjectChildren;

        /// <summary>
        /// If this MenuItem references a parameter that does not exist, it is created automatically.
        /// In this case, isSynced controls whether the parameter is network synced.
        /// </summary>
        public bool isSynced = true;

        /// <summary>
        ///     If this MenuItem references a parameter that does not exist, it is created automatically.
        ///     In this case, isSaved controls whether the parameter is saved across avatar changes.
        /// </summary>
        public bool isSaved = true;

        /// <summary>
        ///     If this MenuItem references a parameter that does not exist, it is created automatically.
        ///     In this case, isDefault controls whether the parameter is set, by default, to the value for this
        ///     menu item. If multiple menu items reference the same parameter, the last menu item in hierarchy order
        ///     with isDefault = true is selected.
        /// </summary>
        public bool isDefault;

        /// <summary>
        ///     If true, the value for this toggle or button menu item will be automatically selected.
        ///     Typically, this will be zero for the default menu item, then subsequent menu items will be allocated
        ///     sequentially in hierarchy order.
        /// </summary>
        public bool automaticValue;

        /// <summary>
        ///     Although unspecified, the label of a menu may contain rich text and line breaks.
        ///     If label is not an empty string, this MenuItem will use that as its name.
        ///     Otherwise, it will use the name of the containing game object as the label.
        /// </summary>
        [Multiline]
        public string label = "";

        private ModularAvatarMenuItem()
        {
            #if MA_VRCSDK3_AVATARS
            PortableControl = new PortableMenuControl();
            #else
            Control = new();
            #endif

            PortableControl._backingMenuItem = this;
        }
        
        private void Reset()
        {
            // Init settings only when added or reset manually from the Inspector.
            // Otherwise, some plugins that add this component may break in non-playmode builds.
            if (RuntimeUtil.IsResetFromInspector())
            {
                InitSettings();
            }
        }

        internal void InitSettings()
        {
#if MA_VRCSDK3_AVATARS
            Control = new VRCExpressionsMenu.Control();
#endif
            PortableControl.Type = PortableControlType.Toggle;
            PortableControl.Value = 1;
            PortableControl.Parameter = "";
            PortableControl.Icon = null;
            PortableControl.Labels = ImmutableList<PortableLabel>.Empty;
            PortableControl.SubParameters = ImmutableList<string>.Empty;
            PortableControl.VRChatSubMenu = null;
            label = "";
            
            isSaved = true;
            isSynced = true;
            isDefault = false;
            automaticValue = true;

            MenuSource = SubmenuSource.Children;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            RuntimeUtil.InvalidateMenu();

#if MA_VRCSDK3_AVATARS
            if (Control == null)
            {
                Control = new VRCExpressionsMenu.Control();
            }
#endif
        }

        public override void ResolveReferences()
        {
            // no-op
        }
#if MA_VRCSDK3_AVATARS
        public void Visit(NodeContext context)
        {
            if (Control == null)
            {
                Control = new VRCExpressionsMenu.Control();
            }

            var cloned = new VirtualControl(Control);
            cloned.subMenu = null;
            cloned.name = string.IsNullOrEmpty(label) ? gameObject.name : label;

            FilterSubParameters(cloned);

            if (cloned.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                switch (this.MenuSource)
                {
                    case SubmenuSource.MenuAsset:
                        cloned.SubmenuNode = context.NodeFor(this.Control.subMenu);
                        break;
                    case SubmenuSource.Children:
                    {
                        var root = this.menuSource_otherObjectChildren != null
                            ? this.menuSource_otherObjectChildren
                            : this.gameObject;

                        cloned.SubmenuNode = context.NodeFor(new MenuNodesUnder(root));
                        break;
                    }
                }
            }

            context.PushControl(cloned);
        }
#endif

#if MA_VRCSDK3_AVATARS
        private void FilterSubParameters(VirtualControl control)
        {
            var maxSubParams = 0;
            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                case VRCExpressionsMenu.Control.ControlType.Button:
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                default:
                    maxSubParams = 0;
                    break;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    maxSubParams = 1;
                    break;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    maxSubParams = 2;
                    break;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    maxSubParams = 4;
                    break;
            }

            if (control.subParameters.Length > maxSubParams)
                control.subParameters = control.subParameters.Take(maxSubParams).ToArray();
        }

        internal VRCExpressionParameters.ValueType ExpressionParametersValueType
        {
            get
            {
                if (Control == null)
                {
                    Control = new VRCExpressionsMenu.Control();
                }

                var type = VRCExpressionParameters.ValueType.Bool;

                // 2, 3, ..., (255)
                if (Control.value > 1)
                {
                    type = VRCExpressionParameters.ValueType.Int;
                }

                // (-1.0), ..., -0.1, 0.1, ..., 0.9
                if (Control.value < 0 || Mathf.Abs(Control.value - Mathf.Round(Control.value)) > 0.01f)
                {
                    type = VRCExpressionParameters.ValueType.Float;
                }

                return type;
            }
        }

        internal AnimatorControllerParameterType AnimatorControllerParameterType
            => ExpressionParametersValueType switch
            {
                VRCExpressionParameters.ValueType.Bool => AnimatorControllerParameterType.Bool,
                VRCExpressionParameters.ValueType.Int => AnimatorControllerParameterType.Int,
                VRCExpressionParameters.ValueType.Float => AnimatorControllerParameterType.Float,
                _ => 0,
            };
#endif
    }
}