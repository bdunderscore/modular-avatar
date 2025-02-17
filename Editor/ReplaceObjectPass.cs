using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    using UnityObject = Object;
    // ReSharper disable once RedundantUsingDirective
    using Object = System.Object;

    internal class ReplaceObjectPass
    {
        private readonly ndmf.BuildContext _buildContext;

        public ReplaceObjectPass(ndmf.BuildContext context)
        {
            _buildContext = context;
        }

        struct Reference
        {
            public UnityObject Source;
            public string PropPath;
        }

        public void Process()
        {
            var avatarRootTransform = _buildContext.AvatarRootTransform;
            var replacementComponents =
                avatarRootTransform.GetComponentsInChildren<ModularAvatarReplaceObject>(true);

            if (replacementComponents.Length == 0) return;

            // Build an index of object references within the avatar that we might need to fix
            Dictionary<UnityObject, List<Reference>> refIndex = BuildReferenceIndex();

            Dictionary<GameObject, (ModularAvatarReplaceObject, GameObject)> replacements
                = new Dictionary<GameObject, (ModularAvatarReplaceObject, GameObject)>();

            foreach (var component in replacementComponents)
            {
                var targetObject = component.targetObject?.Get(avatarRootTransform);

                if (targetObject == null)
                {
                    BuildReport.LogFatal("error.replace_object.null_target", new string[0],
                        component, targetObject);
                    UnityObject.DestroyImmediate(component.gameObject);
                    continue;
                }

                if (component.transform.GetComponentsInParent<Transform>().Contains(targetObject.transform))
                {
                    BuildReport.LogFatal("error.replace_object.parent_of_target", new string[0],
                        component, targetObject);
                    UnityObject.DestroyImmediate(component.gameObject);
                    continue;
                }

                if (replacements.TryGetValue(targetObject, out var existingReplacement))
                {
                    BuildReport.LogFatal("error.replace_object.replacing_replacement", new string[0],
                        component, existingReplacement.Item1);
                    UnityObject.DestroyImmediate(component);
                    continue;
                }

                replacements[targetObject] = (component, component.gameObject);
            }

            // Execute replacement. For now, we reparent children.
            // TODO: Handle replacing recursively.

            foreach (var kvp in replacements)
            {
                var original = kvp.Key;
                var replacement = kvp.Value.Item2;

                replacement.transform.SetParent(original.transform.parent, true);
                var siblingIndex = original.transform.GetSiblingIndex();

                // Move children of original parent (the List needs to be cloned first because it is being modified)
                List<Transform> children = new List<Transform>();
                foreach (Transform child in original.transform)
                {
                    children.Add(child);
                }
                foreach (Transform child in children)
                {
                    child.SetParent(replacement.transform, true);
                }

                // Update property references
                foreach (var refKey in GetIndexedComponents(original))
                {
                    var (component, index) = refKey;

                    if (!refIndex.TryGetValue(component, out var references))
                    {
                        continue;
                    }

                    UnityObject newValue = null;
                    if (component is GameObject)
                    {
                        newValue = replacement;
                    }
                    else
                    {
                        var replacementCandidates = replacement.GetComponents(component.GetType());

                        if (replacementCandidates.Length > index)
                        {
                            newValue = replacementCandidates[index];
                        }
                    }

                    foreach (var reference in references)
                    {
                        SerializedObject so = new SerializedObject(reference.Source);
                        SerializedProperty prop = so.FindProperty(reference.PropPath);
                        prop.objectReferenceValue = newValue;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                _buildContext.Extension<AnimatorServicesContext>().ObjectPathRemapper
                    .ReplaceObject(original, replacement);

                // Destroy original
                UnityObject.DestroyImmediate(original);
                replacement.transform.SetSiblingIndex(siblingIndex);
            }
        }

        private IEnumerable<(UnityObject, int)> GetIndexedComponents(GameObject original)
        {
            yield return (original, -1);

            Dictionary<Type, int> componentTypeIndex = new Dictionary<Type, int>();
            foreach (var component in original.GetComponents<Component>())
            {
                if (!componentTypeIndex.TryGetValue(component.GetType(), out int index))
                {
                    index = 0;
                }

                componentTypeIndex[component.GetType()] = index + 1;

                yield return (component, index);
            }
        }

        private Dictionary<UnityObject, List<Reference>> BuildReferenceIndex()
        {
            Dictionary<UnityObject, List<Reference>> refIndex = new Dictionary<UnityObject, List<Reference>>();

            IndexObject(_buildContext.AvatarRootObject);

            return refIndex;

            void IndexObject(GameObject obj)
            {
                foreach (Transform child in obj.transform)
                {
                    IndexObject(child.gameObject);
                }

                Dictionary<Type, int> componentIndex = new Dictionary<Type, int>();
                foreach (Component c in obj.GetComponents(typeof(Component)))
                {
                    if (c == null) continue;
                    if (c is Transform) continue;

                    if (!componentIndex.TryGetValue(c.GetType(), out int index)) index = 0;
                    componentIndex[c.GetType()] = index + 1;

                    var so = new SerializedObject(c);
                    var sp = so.GetIterator();
                    bool enterChildren = true;
                    while (sp.Next(enterChildren))
                    {
                        enterChildren = true;

                        if (sp.propertyType == SerializedPropertyType.String) enterChildren = false;
                        if (sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (sp.objectReferenceValue == null) continue;

                        string path = sp.propertyPath;
                        if (path == "m_GameObject") continue;

                        Reference reference;
                        if (sp.objectReferenceValue is GameObject)
                        {
                            // ok
                        }
                        else if (sp.objectReferenceValue is Component)
                        {
                            // ok
                        }
                        else
                        {
                            continue;
                        }

                        reference.Source = c;
                        reference.PropPath = sp.propertyPath;

                        var refKey = sp.objectReferenceValue;
                        if (!refIndex.TryGetValue(refKey, out var refList))
                        {
                            refList = new List<Reference>();
                            refIndex[refKey] = refList;
                        }

                        refList.Add(reference);
                    }
                }
            }
        }
    }
}