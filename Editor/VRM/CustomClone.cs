#if MA_VRM1

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    internal abstract class CustomClone<T, TOut>
        where T : Object
        where TOut : Object
    {
        CopyStrategyDescriptor _descriptor;

        protected abstract void Define(CopyStrategyDescriptor descriptor);

        [PublicAPI]
        public (TOut mainAsset, IEnumerable<Object> subAssets) Clone(T source)
        {
            if (_descriptor == null)
            {
                _descriptor = new CopyStrategyDescriptor();
                Define(_descriptor);                
            }
            
            var context = new CustomCloneContext(_descriptor);
            var mainAsset = context.CachedClone(source) as TOut;
            var subAssets = context.Mapping.Values.Where(a => a != mainAsset).ToArray();
            return (mainAsset, subAssets);
        }

        [PublicAPI]
        public Object CloneAsNewAsset(T source, string assetPath)
        {
            var assets = Clone(source);
            AssetDatabase.CreateAsset(assets.mainAsset, assetPath);
            foreach (var subAsset in assets.subAssets)
            {
                AssetDatabase.AddObjectToAsset(subAsset, assets.mainAsset);
                subAsset.hideFlags = HideFlags.None;
            }
            AssetDatabase.SaveAssets();
            return assets.mainAsset;
        }
    }
    
    abstract class CustomClone<T> : CustomClone<T, T> where T : Object { }

    class CustomCloneContext
    {
        readonly CopyStrategyDescriptor _descriptor;
        public readonly Dictionary<Object, Object> Mapping = new Dictionary<Object, Object>();
            
        public CustomCloneContext(CopyStrategyDescriptor descriptor) => _descriptor = descriptor;

        public Object CachedClone(Object source)
        {
            if (source is null) return default;

            foreach (var copyStrategy in _descriptor.List)
            {
                if (copyStrategy.Match(source))
                {
                    return copyStrategy.GetCopy(source, this);
                }
            }
            throw new CustomCloneException($"Unable to handle object <{source}> with type <{source.GetType()}>");
        }
    }

    internal class CopyStrategyDescriptor
    {
        public List<ICopyStrategy> List { get; } = new List<ICopyStrategy>();
        public void Add(ICopyStrategy copyStrategy) => List.Add(copyStrategy);

        public void ShallowCopy<T>() where T : Object => Add(new ShallowCopy<T>());
        public void DeepCopy<T>(Func<Object, T> instantiate = null, Action<T, CustomCloneContext> duplicateFields = null, Action<T> postProcess = null)
            where T : Object
        {
            Add(new DeepCopy<T>(instantiate, duplicateFields, postProcess));
        }
    }

    
    internal interface ICopyStrategy
    {
        bool Match(Object source);
        Object GetCopy(Object source, CustomCloneContext context);
    }

    internal class ShallowCopy<T> : ICopyStrategy
        where T : Object
    {
        public bool Match(Object source) => source is T;
        public Object GetCopy(Object source, CustomCloneContext context) => source;
    }
    
    internal class DeepCopy<T> : ICopyStrategy
        where T : Object
    {
        readonly Func<Object, T> _instantiate;
        readonly Action<T, CustomCloneContext> _duplicateFields;
        readonly Action<T> _postProcess;

        public DeepCopy(Func<Object, T> instantiate = null, Action<T, CustomCloneContext> duplicateFields = null, Action<T> postProcess = null)
        {
            _instantiate = instantiate ?? Instantiate;
            _duplicateFields = duplicateFields ?? DuplicateFields;
            _postProcess = postProcess ?? Postprocess;
        }

        public bool Match(Object source) => source is T;

        public Object GetCopy(Object source, CustomCloneContext context)
        {
            if (context.Mapping.TryGetValue(source, out var mapped)) return mapped;
            var target = _instantiate(source);
            context.Mapping[source] = target;
            _duplicateFields(target, context);
            _postProcess(target);
            return target;
        }

        protected virtual T Instantiate(Object source)
        {
            T target;
            var ctor = typeof(T).GetConstructor(Type.EmptyTypes);
            if (ctor == null || source is ScriptableObject)
            {
                target = (T) Object.Instantiate(source);
                target.name = source.name;
            }
            else
            {
                target = (T) ctor.Invoke(Array.Empty<object>());
                EditorUtility.CopySerialized(source, target);
            }
            return target;
        }

        protected virtual void DuplicateFields(T target, CustomCloneContext context)
        {
            var serializedObject = new SerializedObject(target);
            var it = serializedObject.GetIterator();
            
            var enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = true;
                switch (it.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        it.objectReferenceValue = context.CachedClone(it.objectReferenceValue);
                        break;
                    // Iterating strings can get super slow...
                    case SerializedPropertyType.String:
                        enterChildren = false;
                        break;
                }
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        [UsedImplicitly]
        protected virtual void Postprocess(T target)
        {
        }
    }
    
    internal class CustomCloneException : Exception
    {
        public CustomCloneException(string message) : base(message) { }
    }
}

#endif