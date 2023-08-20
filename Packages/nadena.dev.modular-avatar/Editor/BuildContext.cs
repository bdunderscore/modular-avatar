﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BuildContext
    {
        internal readonly VRCAvatarDescriptor AvatarDescriptor;
        internal readonly AnimationDatabase AnimationDatabase = new AnimationDatabase();
        internal readonly UnityEngine.Object AssetContainer;

        private bool SaveImmediate = false;

        internal readonly Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> ClonedMenus
            = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

        /// <summary>
        /// This dictionary overrides the _original contents_ of ModularAvatarMenuInstallers. Notably, this does not
        /// replace the source menu for the purposes of identifying any other MAMIs that might install to the same
        /// menu asset.
        /// </summary>
        internal readonly Dictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>> PostProcessControls
            = new Dictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>();

        public BuildContext(VRCAvatarDescriptor avatarDescriptor)
        {
            AvatarDescriptor = avatarDescriptor;

            // AssetDatabase.CreateAsset is super slow - so only do it once, and add everything else as sub-assets.
            // This scriptable object exists for the sole purpose of providing a placeholder to dump everything we
            // generate into. Note that we use a custom component here to force binary serialization; this saves both
            // time as well as disk space (if you're using manual bake).
            AssetContainer = ScriptableObject.CreateInstance<MAAssetBundle>();
            AssetDatabase.CreateAsset(AssetContainer, Util.GenerateAssetPath());
        }

        public void SaveAsset(Object obj)
        {
            if (!SaveImmediate || AssetDatabase.IsMainAsset(obj) || AssetDatabase.IsSubAsset(obj)) return;

            AssetDatabase.AddObjectToAsset(obj, AssetContainer);
        }

        public AnimatorController CreateAnimator(AnimatorController toClone = null)
        {
            AnimatorController controller;
            if (toClone != null)
            {
                controller = Object.Instantiate(toClone);
            }
            else
            {
                controller = new AnimatorController();
            }

            SaveAsset(controller);

            return controller;
        }

        public AnimatorController DeepCloneAnimator(RuntimeAnimatorController controller)
        {
            if (controller == null) return null;

            var merger = new AnimatorCombiner(this, controller.name + " (clone)");
            switch (controller)
            {
                case AnimatorController ac:
                    merger.AddController("", ac, null);
                    break;
                case AnimatorOverrideController oac:
                    merger.AddOverrideController("", oac, null);
                    break;
                default:
                    throw new Exception("Unknown RuntimeAnimatorContoller type " + controller.GetType());
            }

            return merger.Finish();
        }

        public AnimatorController ConvertAnimatorController(AnimatorOverrideController overrideController)
        {
            var merger = new AnimatorCombiner(this, overrideController.name + " (clone)");
            merger.AddOverrideController("", overrideController, null);
            return merger.Finish();
        }

        public VRCExpressionsMenu CloneMenu(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            if (ClonedMenus.TryGetValue(menu, out var newMenu)) return newMenu;
            newMenu = Object.Instantiate(menu);
            this.SaveAsset(newMenu);
            ClonedMenus[menu] = newMenu;

            foreach (var control in newMenu.controls)
            {
                if (Util.ValidateExpressionMenuIcon(control.icon) != Util.ValidateExpressionMenuIconResult.Success)
                    control.icon = null;

                for (int i = 0; i < control.labels.Length; i++)
                {
                    var label = control.labels[i];
                    var labelResult = Util.ValidateExpressionMenuIcon(label.icon);
                    if (labelResult != Util.ValidateExpressionMenuIconResult.Success)
                    {
                        label.icon = null;
                        control.labels[i] = label;
                    }
                }

                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    control.subMenu = CloneMenu(control.subMenu);
                }
            }

            return newMenu;
        }

        public void CommitReferencedAssets()
        {
            HashSet<UnityEngine.Object> referencedAssets = new HashSet<UnityEngine.Object>();
            HashSet<UnityEngine.Object> sceneAssets = new HashSet<UnityEngine.Object>();

            Walk(AvatarDescriptor.gameObject);

            referencedAssets.RemoveWhere(sceneAssets.Contains);
            referencedAssets.RemoveWhere(a => a is GameObject || a is Component);
            referencedAssets.RemoveWhere(o => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o)));

            int index = 0;

            foreach (var asset in referencedAssets)
            {
                if (asset.name == "")
                {
                    asset.name = "Asset " + index++;
                }

                AssetDatabase.AddObjectToAsset(asset, AssetContainer);
            }

            SaveImmediate = true;

            void Walk(GameObject root)
            {
                var components = AvatarDescriptor.gameObject.GetComponentsInChildren<Component>(true);
                Queue<UnityEngine.Object> visitQueue = new Queue<UnityEngine.Object>(
                    components.Where(t => (!(t is Transform || t == null)))
                );

                while (visitQueue.Count > 0)
                {
                    var current = visitQueue.Dequeue();
                    if (referencedAssets.Contains(current)) continue;
                    referencedAssets.Add(current);

                    // These assets have large internal arrays we don't want to walk through...
                    if (current is Mesh || current is AnimationClip || current is Texture) continue;

                    var so = new SerializedObject(current);
                    var sp = so.GetIterator();
                    bool enterChildren = true;

                    while (sp.Next(enterChildren))
                    {
                        enterChildren = true;
                        if (sp.name == "m_GameObject") continue;
                        if (sp.propertyType == SerializedPropertyType.String)
                        {
                            enterChildren = false;
                            continue;
                        }

                        if (sp.isArray && IsPrimitiveArray(sp))
                        {
                            enterChildren = false;
                        }

                        if (sp.propertyType != SerializedPropertyType.ObjectReference)
                        {
                            continue;
                        }

                        var obj = sp.objectReferenceValue;
                        if (obj != null && !referencedAssets.Contains(obj) && !(obj is Transform) &&
                            !(obj is GameObject))
                        {
                            visitQueue.Enqueue(sp.objectReferenceValue);
                        }
                    }
                }
            }
        }

        private bool IsPrimitiveArray(SerializedProperty prop)
        {
            if (prop.arraySize == 0) return false;
            var propertyType = prop.GetArrayElementAtIndex(0).propertyType;
            switch (propertyType)
            {
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ObjectReference:
                    return false;
                default:
                    return true;
            }
        }
    }
}