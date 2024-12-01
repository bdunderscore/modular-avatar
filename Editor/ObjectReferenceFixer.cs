using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    public static class ObjectReferenceFixer
    {
        private static ComputeContext _context;

        private static int? _lastStage;

        private static int? GetCurrentContentsRootId(out GameObject contentsRoot)
        {
            contentsRoot = null;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null) return null;

            contentsRoot = stage.prefabContentsRoot;

            return stage.prefabContentsRoot.GetInstanceID();
        }
        
        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.delayCall += ProcessObjectReferences;
            EditorApplication.update += () =>
            {
                var curStage = GetCurrentContentsRootId(out _);

                if (curStage != _lastStage)
                {
                    _context?.Invalidate?.Invoke();
                }
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.delayCall += ProcessObjectReferences;
                }
            };
        }

        private static void ProcessObjectReferences()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _context = null;
                return;
            }
            
            _lastStage = GetCurrentContentsRootId(out var contentsRoot);

            AvatarObjectReference.InvalidateAll();

            _context = new ComputeContext("ObjectReferenceFixer");
            _context.InvokeOnInvalidate<object>(typeof(ObjectReferenceFixer), _ => ProcessObjectReferences());

            IEnumerable<IHaveObjReferences> withReferences = _context.GetComponentsByType<IHaveObjReferences>();
            if (contentsRoot != null)
                withReferences =
                    withReferences.Concat(
                        _context.GetComponentsInChildren<IHaveObjReferences>(contentsRoot, true)
                    );

            foreach (var obj in withReferences)
            {
                var component = obj as Component;
                if (component == null) continue;

                var avatar = _context.GetAvatarRoot(component.gameObject);
                if (avatar == null) continue;

                var references = _context.Observe(component,
                    c => ((IHaveObjReferences)c).GetObjectReferences().Select(
                        r => (r.targetObject, r.referencePath, r)
                    ),
                    Enumerable.SequenceEqual
                );

                var dirty = false;

                foreach (var (targetObject, referencePath, objRef) in references)
                {
                    var resolvedTarget = objRef.Get(component);
                    if (objRef.Get(component) == null) continue;
                    if (targetObject == null)
                    {
                        Undo.RecordObject(component, "");
                        objRef.targetObject = resolvedTarget;
                        dirty = true;
                    }
                    else
                    {
                        // Direct object reference always wins in the event of a conflict.
                        resolvedTarget = targetObject;
                    }

                    foreach (var t in _context.ObservePath(resolvedTarget.transform))
                    {
                        _context.Observe(t.gameObject, g => g.name);
                    }

                    if (!resolvedTarget.transform.IsChildOf(avatar.transform)) continue;

                    if (objRef.IsConsistent(avatar)) continue;

                    if (!dirty)
                    {
                        dirty = true;
                        Undo.RecordObject(component, "");
                    }

                    objRef.Set(targetObject);
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(component);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
        }
    }
}
