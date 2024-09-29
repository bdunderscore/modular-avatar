using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using BuildContext = nadena.dev.ndmf.BuildContext;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.animation
{
    using UnityObject = Object;

    internal class DeepClone
    {
        private bool _isSaved;
        private UnityObject _combined;

        public AnimatorOverrideController OverrideController { get; set; }

        public DeepClone(BuildContext context)
        {
            _isSaved = context.AssetContainer != null && EditorUtility.IsPersistent(context.AssetContainer);
            _combined = context.AssetContainer;
        }

        public T DoClone<T>(T original,
            string basePath = null,
            Dictionary<UnityObject, UnityObject> cloneMap = null
        ) where T : UnityObject
        {
            if (original == null) return null;
            if (cloneMap == null) cloneMap = new Dictionary<UnityObject, UnityObject>();

            Func<UnityObject, UnityObject> visitor = null;
            if (basePath != null)
            {
                visitor = o => CloneWithPathMapping(o, basePath);
            }

            // We want to avoid trying to copy assets not part of the animation system (eg - textures, meshes,
            // MonoScripts...), so check for the types we care about here
            switch (original)
            {
                // Any object referenced by an animator that we intend to mutate needs to be listed here.
                case Motion _:
                case AnimatorController _:
                case AnimatorState _:
                case AnimatorStateMachine _:
                case AnimatorTransitionBase _:
                case StateMachineBehaviour _:
                case AvatarMask _:
                    break; // We want to clone these types
                    
                case AudioClip _: //Used in VRC Animator Play Audio State Behavior
                // Leave textures, materials, and script definitions alone
                case Texture2D _:
                case MonoScript _:
                case Material _:
                    return original;

                // Also avoid copying unknown scriptable objects.
                // This ensures compatibility with e.g. avatar remote, which stores state information in a state
                // behaviour referencing a custom ScriptableObject
                case ScriptableObject _:
                    return original;

                default:
                    throw new Exception($"Unknown type referenced from animator: {original.GetType()}");
            }

            // When using AnimatorOverrideController, replace the original AnimationClip based on AnimatorOverrideController.
            if (OverrideController != null && original is AnimationClip srcClip)
            {
                T overrideClip = OverrideController[srcClip] as T;
                if (overrideClip != null)
                {
                    original = overrideClip;
                }
            }

            if (cloneMap.ContainsKey(original))
            {
                return (T)cloneMap[original];
            }

            var obj = visitor?.Invoke(original);
            if (obj != null)
            {
                cloneMap[original] = obj;
                if (obj != original)
                {
                    ObjectRegistry.RegisterReplacedObject(original, obj);
                }
                
                if (_isSaved && !EditorUtility.IsPersistent(obj))
                {
                    AssetDatabase.AddObjectToAsset(obj, _combined);
                }

                return (T)obj;
            }



            var ctor = original.GetType().GetConstructor(Type.EmptyTypes);
            if (ctor == null || original is ScriptableObject)
            {
                obj = UnityObject.Instantiate(original);
            }
            else
            {
                obj = (T)ctor.Invoke(Array.Empty<object>());
                EditorUtility.CopySerialized(original, obj);
            }

            cloneMap[original] = obj;
            ObjectRegistry.RegisterReplacedObject(original, obj);

            if (_isSaved)
            {
                AssetDatabase.AddObjectToAsset(obj, _combined);
            }

            SerializedObject so = new SerializedObject(obj);
            SerializedProperty prop = so.GetIterator();

            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = true;
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                    {
                        if (prop.objectReferenceValue != null && prop.objectReferenceValue != obj)
                        {
                            var newObj = DoClone(prop.objectReferenceValue, basePath, cloneMap);
                            prop.objectReferenceValue = newObj;
                        }

                        break;
                    }
                    // Iterating strings can get super slow...
                    case SerializedPropertyType.String:
                        enterChildren = false;
                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            return (T)obj;
        }

        // internal for testing
        internal static AvatarMask CloneAvatarMask(AvatarMask mask, string basePath)
        {
            if (basePath.EndsWith("/")) basePath = basePath.Substring(0, basePath.Length - 1);

            var newMask = new AvatarMask();

            // Transfer first the humanoid mask data
            EditorUtility.CopySerialized(mask, newMask);

            var srcSo = new SerializedObject(mask);
            var dstSo = new SerializedObject(newMask);
            var srcElements = srcSo.FindProperty("m_Elements");

            if (basePath == "" || srcElements.arraySize == 0) return newMask; // no changes required

            // We now need to prefix the elements of basePath (with weight zero)

            var newElements = new List<string>();

            var accum = "";
            foreach (var element in basePath.Split("/"))
            {
                if (accum != "") accum += "/";
                accum += element;

                newElements.Add(accum);
            }

            var dstElements = dstSo.FindProperty("m_Elements");

            // We'll need to create new array elements by using DuplicateCommand. We'll then rewrite the whole
            // list to keep things in traversal order.
            for (var i = 0; i < newElements.Count; i++) dstElements.GetArrayElementAtIndex(0).DuplicateCommand();

            var totalElements = srcElements.arraySize + newElements.Count;
            for (var i = 0; i < totalElements; i++)
            {
                var dstElem = dstElements.GetArrayElementAtIndex(i);
                var dstPath = dstElem.FindPropertyRelative("m_Path");
                var dstWeight = dstElem.FindPropertyRelative("m_Weight");

                var srcIndex = i - newElements.Count;
                if (srcIndex < 0)
                {
                    dstPath.stringValue = newElements[i];
                    dstWeight.floatValue = 0;
                }
                else
                {
                    var srcElem = srcElements.GetArrayElementAtIndex(srcIndex);
                    dstPath.stringValue = basePath + "/" + srcElem.FindPropertyRelative("m_Path").stringValue;
                    dstWeight.floatValue = srcElem.FindPropertyRelative("m_Weight").floatValue;
                }
            }

            dstSo.ApplyModifiedPropertiesWithoutUndo();

            return newMask;
        }

        private UnityObject CloneWithPathMapping(UnityObject o, string basePath)
        {
            if (o is AvatarMask mask)
            {
                return CloneAvatarMask(mask, basePath);
            }

            if (o is AnimationClip clip)
            {
                // We'll always rebase if the asset is non-persistent, because we can't reference a nonpersistent asset
                // from a persistent asset. If the asset is persistent, skip cases where path editing isn't required,
                // or where this is one of the special VRC proxy animations.
                if (EditorUtility.IsPersistent(o) && (basePath == "" || Util.IsProxyAnimation(clip))) return clip;

                AnimationClip newClip = new AnimationClip();
                newClip.name = "rebased " + clip.name;
                if (_isSaved)
                {
                    AssetDatabase.AddObjectToAsset(newClip, _combined);
                }

                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var newBinding = binding;
                    newBinding.path = MapPath(binding, basePath);
                    // https://github.com/bdunderscore/modular-avatar/issues/950
                    // It's reported that sometimes using SetObjectReferenceCurve right after SetCurve might cause the
                    // curves to be forgotten; use SetEditorCurve instead.
                    AnimationUtility.SetEditorCurve(newClip, newBinding,
                        AnimationUtility.GetEditorCurve(clip, binding));
                }

                foreach (var objBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var newBinding = objBinding;
                    newBinding.path = MapPath(objBinding, basePath);
                    AnimationUtility.SetObjectReferenceCurve(newClip, newBinding,
                        AnimationUtility.GetObjectReferenceCurve(clip, objBinding));
                }

                newClip.wrapMode = clip.wrapMode;
                newClip.legacy = clip.legacy;
                newClip.frameRate = clip.frameRate;
                newClip.localBounds = clip.localBounds;
                AnimationUtility.SetAnimationClipSettings(newClip, AnimationUtility.GetAnimationClipSettings(clip));

                return newClip;
            }
            else if (o is Texture)
            {
                return o;
            }
            else
            {
                return null;
            }
        }

        private static string MapPath(EditorCurveBinding binding, string basePath)
        {
            if (binding.type == typeof(Animator) && binding.path == "")
            {
                return "";
            }
            else
            {
                var newPath = binding.path == "" ? basePath : basePath + binding.path;
                if (newPath.EndsWith("/"))
                {
                    newPath = newPath.Substring(0, newPath.Length - 1);
                }

                return newPath;
            }
        }
    }
}
