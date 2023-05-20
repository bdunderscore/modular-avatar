using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Remove all GameObjects which have no influence on the avatar.
    /// </summary>
    internal class GCGameObjectsPass
    {
        private readonly BuildContext _context;
        private readonly GameObject _root;
        private readonly HashSet<GameObject> _referencedGameObjects = new HashSet<GameObject>();
        private readonly HashSet<GameObject> _allBones = new HashSet<GameObject>();
        private readonly HashSet<GameObject> _usedBones = new HashSet<GameObject>();
        private readonly HashSet<VRCPhysBone> _physBones = new HashSet<VRCPhysBone>();
        private readonly HashSet<VRCPhysBoneColliderBase> _physBoneColliders = new HashSet<VRCPhysBoneColliderBase>();
        private int _removedObjects;
        private int _removedComponents;

        internal GCGameObjectsPass(BuildContext context, GameObject root)
        {
            _context = context;
            _root = root;
        }

        internal void OnPreprocessAvatar()
        {
            CollectReferences();
            Cleanup();
            MarkAll();
            Sweep();
            Debug.Log($"Cleanup complete, removed {_removedObjects} objects and {_removedComponents} components.");
        }

        private void CollectReferences()
        {
            LoopAllComponents((obj, component) =>
            {
                switch (component)
                {
                    case SkinnedMeshRenderer mesh:
                        CollectBonesFromSkinnedMeshRenderer(mesh);
                        break;

                    case VRCPhysBone pb:
                        _physBones.Add(pb);
                        break;
                    case VRCPhysBoneCollider collider:
                        _physBoneColliders.Add(collider);
                        break;
                }
            });
        }

        private void CollectBonesFromSkinnedMeshRenderer(SkinnedMeshRenderer renderer)
        {
            HashSet<int> usedBonesIndices = new HashSet<int>();
            foreach (var boneWeight in renderer.sharedMesh.boneWeights)
            {
                // technically should add only if weight is > 0, but not worth the cost and does not have negative effects
                usedBonesIndices.Add(boneWeight.boneIndex0);
                usedBonesIndices.Add(boneWeight.boneIndex1);
                usedBonesIndices.Add(boneWeight.boneIndex2);
                usedBonesIndices.Add(boneWeight.boneIndex3);
            }

            Transform[] bones = renderer.bones;
            foreach (var usedBoneIndex in usedBonesIndices)
            {
                Transform usedBone = bones[usedBoneIndex];
                if (usedBone != null) _usedBones.Add(usedBone.gameObject);
            }

            foreach (var bone in bones)
            {
                if (bone != null) _allBones.Add(bone.gameObject);
            }
        }

        private void Cleanup()
        {
            // remove phys bones that only affect bones that are not used
            var bonesToRemove = new HashSet<VRCPhysBone>();
            foreach (var vrcPhysBone in _physBones)
            {
                vrcPhysBone.InitTransforms();
                if (vrcPhysBone.bones.Any(x => _usedBones.Contains(x.transform.gameObject))) continue;
                // in case someone uses the component on chain of something that is not actually a bone
                if (vrcPhysBone.bones.Any(x => !_allBones.Contains(x.transform.gameObject))) continue;
                bonesToRemove.Add(vrcPhysBone);
            }

            foreach (var vrcPhysBone in bonesToRemove)
            {
                _physBones.Remove(vrcPhysBone);
                PurgeComponent(vrcPhysBone);
            }

            // remove all colliders that no longer collide with anything
            var collidersToRemove = new HashSet<VRCPhysBoneColliderBase>(_physBoneColliders);
            foreach (var vrcPhysBone in _physBones)
            {
                foreach (var vrcPhysBoneColliderBase in vrcPhysBone.colliders)
                {
                    collidersToRemove.Remove(vrcPhysBoneColliderBase);
                }
            }

            foreach (var collider in collidersToRemove)
            {
                _physBoneColliders.Remove(collider);
                PurgeComponent(collider);
            }
        }


        private void MarkAll()
        {
            foreach (var gameObject in _usedBones)
            {
                MarkObject(gameObject);
            }

            LoopAllComponents((obj, component) =>
                {
                    switch (component)
                    {
                        case Transform _: break;

                        case SkinnedMeshRenderer mesh:
                            MarkObject(obj);
                            MarkSkinnedMeshRenderer(mesh);
                            break;

                        case VRCPhysBone pb:
                            MarkObject(obj);
                            MarkPhysBone(pb);
                            break;

                        case AvatarTagComponent _:
                            // Tag components will not be retained at runtime, so pretend they're not there.
                            break;

                        default:
                            MarkObject(obj);
                            MarkAllReferencedObjects(component);
                            break;
                    }
                }
            );
        }

        private void LoopAllComponents(Action<GameObject, Component> action)
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
                    if (component == null) continue;
                    action(obj, component);
                }
            }
        }

        private void MarkSkinnedMeshRenderer(SkinnedMeshRenderer renderer)
        {
            MarkObject(renderer.lightProbeProxyVolumeOverride);
            MarkObject(renderer.rootBone);
            MarkObject(renderer.probeAnchor);
        }

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

        private void MarkObject(Transform go)
        {
            if (go != null) MarkObject(go.gameObject);
        }

        private void MarkObject(GameObject go)
        {
            while (go != null && _referencedGameObjects.Add(go) && go != _root)
            {
                go = go.transform.parent?.gameObject;
            }
        }

        private void Sweep()
        {
            foreach (var go in GameObjects())
            {
                if (!_referencedGameObjects.Contains(go))
                {
                    PurgeObject(go);
                }
            }
        }

        private void PurgeObject(GameObject go)
        {
            Debug.Log("Purging object: " + RuntimeUtil.AvatarRootPath(go));
            UnityEngine.Object.DestroyImmediate(go);
            _removedObjects += 1;
        }

        private void PurgeComponent(Component component)
        {
            Debug.Log("Purging component: " + RuntimeUtil.AvatarRootPath(component.gameObject) + "::" +
                      component.GetType().Name);
            UnityEngine.Object.DestroyImmediate(component);
            _removedComponents += 1;
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