using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.animation
{
    internal class ReadableProperty
    {
        private readonly BuildContext _context;
        private readonly AnimationDatabase _animDB;
        private readonly AnimationServicesContext _asc;
        private readonly Dictionary<EditorCurveBinding, string> _alreadyBound = new();
        private long _nextIndex;

        public ReadableProperty(BuildContext context, AnimationDatabase animDB, AnimationServicesContext asc)
        {
            _context = context;
            _animDB = animDB;
            _asc = asc;
        }

        public IEnumerable<(EditorCurveBinding, string)> BoundProperties =>
            _alreadyBound.Select(kv => (kv.Key, kv.Value));
        
        /// <summary>
        ///     Creates an animator parameter which tracks the effective value of a property on a component. This only
        ///     tracks FX layer properties.
        /// </summary>
        /// <param name="ecb"></param>
        /// <returns></returns>
        public string ForBinding(string path, Type componentType, string property)
        {
            var ecb = new EditorCurveBinding
            {
                path = path,
                type = componentType,
                propertyName = property
            };

            if (_alreadyBound.TryGetValue(ecb, out var reader))
            {
                return reader;
            }

            var lastComponent = path.Split("/")[^1];
            var emuPropName = $"__MA/ReadableProp/{lastComponent}/{componentType}/{property}#{_nextIndex++}";

            float initialValue = 0;
            var gameObject = _asc.PathMappings.PathToObject(path);
            Object component = componentType == typeof(GameObject)
                ? gameObject
                : gameObject?.GetComponent(componentType);
            if (component != null)
            {
                var so = new SerializedObject(component);
                var prop = so.FindProperty(property);
                if (prop != null)
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Boolean:
                            initialValue = prop.boolValue ? 1 : 0;
                            break;
                        case SerializedPropertyType.Float:
                            initialValue = prop.floatValue;
                            break;
                        case SerializedPropertyType.Integer:
                            initialValue = prop.intValue;
                            break;
                        default: throw new NotImplementedException($"Property type {prop.type} not supported");
                    }
            }


            _asc.AddPropertyDefinition(new AnimatorControllerParameter
            {
                defaultFloat = initialValue,
                name = emuPropName,
                type = AnimatorControllerParameterType.Float
            });

            BindProperty(ecb, emuPropName);

            _alreadyBound[ecb] = emuPropName;

            return emuPropName;
        }

        private void BindProperty(EditorCurveBinding ecb, string propertyName)
        {
            var boundProp = new EditorCurveBinding
            {
                path = "",
                type = typeof(Animator),
                propertyName = propertyName
            };

            foreach (var clip in _animDB.ClipsForPath(ecb.path)) ProcessAnyClip(clip);

            void ProcessBlendTree(BlendTree blendTree)
            {
                foreach (var child in blendTree.children)
                    switch (child.motion)
                    {
                        case AnimationClip animationClip:
                            ProcessAnimationClip(animationClip);
                            break;

                        case BlendTree subBlendTree:
                            ProcessBlendTree(subBlendTree);
                            break;
                    }
            }

            void ProcessAnimationClip(AnimationClip animationClip)
            {
                var curve = AnimationUtility.GetEditorCurve(animationClip, ecb);
                if (curve == null) return;

                AnimationUtility.SetEditorCurve(animationClip, boundProp, curve);
            }

            void ProcessAnyClip(AnimationDatabase.ClipHolder clip)
            {
                switch (clip.CurrentClip)
                {
                    case AnimationClip animationClip:
                        ProcessAnimationClip(animationClip);
                        break;

                    case BlendTree blendTree:
                        ProcessBlendTree(blendTree);
                        break;
                }
            }
        }

        public string ForActiveSelf(string path)
        {
            return ForBinding(path, typeof(GameObject), "m_IsActive");
        }
    }
}