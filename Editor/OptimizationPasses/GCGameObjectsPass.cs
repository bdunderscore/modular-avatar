using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Remove all GameObjects which have no influence on the avatar.
    /// </summary>
    internal class GCGameObjectsPass
    {
        private readonly BuildContext _context;
        private readonly GameObject _root;
        private readonly HashSet<GameObject> referencedGameObjects = new HashSet<GameObject>();

        internal GCGameObjectsPass(BuildContext context, GameObject root)
        {
            _context = context;
            _root = root;
        }

        internal void OnPreprocessAvatar()
        {
            MarkAll();
            Sweep();
        }

        private void MarkAll()
        {
            foreach (var obj in GameObjects(_root,
                         node =>
                         {
                             if (node.CompareTag("EditorOnly"))
                             {
                                 if (EditorApplication.isPlayingOrWillChangePlaymode)
                                 {
                                     // Retain EditorOnly objects (in case they contain camera fixtures or something),
                                     // but ignore references _from_ them. (TODO: should we mark from them as well?)
                                     MarkObject(node);
                                 }

                                 return false;
                             }

                             return true;
                         }
                     ))
            {
                foreach (var component in obj.GetComponents<Component>())
                {
                    // component is null if script is missing
                    if (!component) continue;
                    switch (component)
                    {
                        case Transform t: break;

#if MA_VRCSDK3_AVATARS
                        case VRCPhysBone pb:
                            MarkObject(obj);
                            MarkPhysBone(pb);
                            break;
#endif

                        case AvatarTagComponent _:
                            // Tag components will not be retained at runtime, so pretend they're not there.
                            break;

                        default:
                            MarkObject(obj);
                            MarkAllReferencedObjects(component);
                            break;
                    }
                }
            }

            // Also retain humanoid bones
            var animator = _root.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (bone == HumanBodyBones.LastBone) continue;

                    var transform = animator.GetBoneTransform(bone);
                    if (transform != null)
                    {
                        MarkObject(transform.gameObject);
                    }
                }
            }

            // https://github.com/bdunderscore/modular-avatar/issues/332
            // Retain transforms with names ending in "end" as these might be used for VRM spring bones
            foreach (Transform t in _root.GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLower().EndsWith("end"))
                {
                    MarkObject(t.gameObject);
                }
            }
            
            // https://github.com/bdunderscore/modular-avatar/issues/308
            // If we have duplicate Armature bones, retain them all in order to deal with some horrible hacks that are
            // in use in the wild.
            if (animator != null && animator.isHuman)
            {
                try
                {
                    var trueArmature = animator?.GetBoneTransform(HumanBodyBones.Hips)?.parent;
                    if (trueArmature != null)
                    {
                        foreach (Transform t in _root.transform)
                        {
                            if (t.name == trueArmature.name)
                            {
                                MarkObject(t.gameObject);
                            }
                        }
                    }
                }
                catch (MissingComponentException)
                {
                    // No animator? weird. Move on.
                }
            }
        }

#if MA_VRCSDK3_AVATARS
        private void MarkPhysBone(VRCPhysBone pb)
        {
            var rootTransform = pb.GetRootTransform();
            var ignoreTransforms = pb.ignoreTransforms ?? new List<Transform>();

            foreach (var obj in GameObjects(rootTransform.gameObject,
                         obj => !obj.CompareTag("EditorOnly") && !ignoreTransforms.Contains(obj.transform)))
            {
                MarkObject(obj);
            }

            // Mark colliders, etc
            MarkAllReferencedObjects(pb);
        }
#endif

        private void MarkAllReferencedObjects(Component component)
        {
            var so = new SerializedObject(component);
            var sp = so.GetIterator();

            bool enterChildren = true;
            while (sp.Next(enterChildren))
            {
                enterChildren = true;

                switch (sp.propertyType)
                {
                    case SerializedPropertyType.String:
                        enterChildren = false;
                        continue;
                    case SerializedPropertyType.ObjectReference:
                        if (sp.objectReferenceValue != null)
                        {
                            if (sp.objectReferenceValue is GameObject refObj)
                            {
                                MarkObject(refObj);
                            }
                            else if (sp.objectReferenceValue is Component comp)
                            {
                                MarkObject(comp.gameObject);
                            }
                        }

                        break;
                }
            }
        }

        private void MarkObject(GameObject go)
        {
            while (go != null && referencedGameObjects.Add(go) && go != _root)
            {
                go = go.transform.parent?.gameObject;
            }
        }

        private void Sweep()
        {
            foreach (var go in GameObjects())
            {
                if (!referencedGameObjects.Contains(go))
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private IEnumerable<GameObject> GameObjects(GameObject node = null,
            Func<GameObject, bool> shouldTraverse = null)
        {
            if (node == null) node = _root;
            if (shouldTraverse == null) shouldTraverse = obj => !obj.CompareTag("EditorOnly");

            if (!shouldTraverse(node)) yield break;

            yield return node;
            if (node == null) yield break;

            // Guard against object deletion mid-traversal
            List<Transform> children = new List<Transform>();
            foreach (Transform t in node.transform)
            {
                children.Add(t);
            }

            foreach (var child in children)
            {
                foreach (var grandchild in GameObjects(child.gameObject, shouldTraverse))
                {
                    yield return grandchild;
                }
            }
        }
    }
}