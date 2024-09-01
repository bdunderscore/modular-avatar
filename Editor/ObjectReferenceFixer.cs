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

        private static PrefabStage _lastStage;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.delayCall += ProcessObjectReferences;
            EditorApplication.update += () =>
            {
                if (PrefabStageUtility.GetCurrentPrefabStage() != _lastStage) _context?.Invalidate?.Invoke();
            };
        }

        private static void ProcessObjectReferences()
        {
            _lastStage = PrefabStageUtility.GetCurrentPrefabStage();

            _context = new ComputeContext("ObjectReferenceFixer");
            _context.InvokeOnInvalidate<object>(typeof(ObjectReferenceFixer), _ => ProcessObjectReferences());

            IEnumerable<IHaveObjReferences> withReferences = _context.GetComponentsByType<IHaveObjReferences>();
            if (_lastStage != null)
                withReferences =
                    withReferences.Concat(
                        _context.GetComponentsInChildren<IHaveObjReferences>(_lastStage.prefabContentsRoot, true)
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
                    if (targetObject == null) continue;
                    _context.ObservePath(targetObject.transform);

                    if (!targetObject.transform.IsChildOf(avatar.transform)) continue;

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