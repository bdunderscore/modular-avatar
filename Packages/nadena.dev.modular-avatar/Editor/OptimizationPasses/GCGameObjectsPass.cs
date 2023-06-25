using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
        private readonly HashSet<GameObject> referencedGameObjects = new HashSet<GameObject>();
        private readonly List<GameObject> _avatarGameObject = new List<GameObject>();

        internal GCGameObjectsPass(BuildContext context, GameObject root)
        {
            _context = context;
            _root = root;
            _avatarGameObject = root.GetComponentsInChildren<Transform>(true).Select(x => x.gameObject).ToList();
        }

        internal void OnPreprocessAvatar()
        {
            MarkSkinnedMeshRenderers();
            MarkOthers();
            MarkPhysBones();
            Sweep();
        }

        private void MarkOthers()
        {
            foreach (GameObject obj in _avatarGameObject)
            {
                if (!IsEditorOnly(obj))
                {
                    foreach (Component component in obj.GetComponents<Component>())
                    {
                        // component is null if script is missing
                        if (!component) continue;
                        switch (component)
                        {
                            case Transform _: break;

                            case VRCPhysBone _:
                            case VRCPhysBoneCollider _:
                                // PB-related components handled separately.
                                break;

                            case SkinnedMeshRenderer _:
                                // SkinnedMehsRenderer has been processed.
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
                }
                else
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        // Retain EditorOnly objects (in case they contain camera fixtures or something),
                        // but ignore references _from_ them. (TODO: should we mark from them as well?)
                        MarkObject(obj);

                        //SkinnedMeshRenderer with insufficient bones will result in corrupted rendering.
                        if (obj.TryGetComponent(out SkinnedMeshRenderer r))
                        {
                            MarkAllReferencedObjects(r);
                        }
                    }
                }
            }

            // Also retain humanoid bones
            if (_root.TryGetComponent(out Animator animator))
            {
                foreach (object bone_ in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    HumanBodyBones bone = (HumanBodyBones)bone_;
                    if (bone == HumanBodyBones.LastBone) continue;

                    Transform transform = animator.GetBoneTransform(bone);
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
        }

        private void MarkSkinnedMeshRenderers()
        {
            foreach (SkinnedMeshRenderer smr in _avatarGameObject.Where(x => !IsEditorOnly(x)).Select(x => x.GetComponent<SkinnedMeshRenderer>()))
            {
                if (!smr) continue;
                MarkObject(smr.gameObject);
                MarkObject(smr.rootBone != null ? smr.rootBone.gameObject : null);
                MarkObject(smr.probeAnchor != null ? smr.probeAnchor.gameObject : null);
                IEnumerable<int> weightedBoneIdxs = smr.sharedMesh.boneWeights.SelectMany(x => { return new int[] { x.boneIndex0, x.boneIndex1, x.boneIndex2, x.boneIndex3 }; });

                weightedBoneIdxs.Select(x => smr.bones[x]).ToList().ForEach(x => MarkObject(x.gameObject));
            }
        }

        private void MarkPhysBones()
        {
            IEnumerable<VRCPhysBone> pbs = referencedGameObjects.SelectMany(x => x.GetComponents<VRCPhysBone>()).Where(x => x != null);
            IEnumerable<VRCPhysBone> markObjects = Enumerable.Empty<VRCPhysBone>();
            foreach (VRCPhysBone pb in pbs)
            {
                markObjects = markObjects.Append(pb);
            }
            foreach (VRCPhysBone pb in markObjects)
            {
                pb.GetComponentsInChildren<Transform>(true).ToList().ForEach(x => MarkObject(x.gameObject));
                MarkAllReferencedObjects(pb);
            }
        }

        private void MarkAllReferencedObjects(Component component)
        {
            SerializedObject so = new SerializedObject(component);
            SerializedProperty sp = so.GetIterator();

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
                go = go.transform.parent != null ? go.transform.parent.gameObject : null;
            }
        }

        private void Sweep()
        {
            foreach (GameObject go in _avatarGameObject)
            {
                if (!referencedGameObjects.Contains(go))
                {
                    Debug.Log("Purging object: " + RuntimeUtil.AvatarRootPath(go));
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        private bool IsEditorOnly(GameObject go)
        {
            Transform t = go.transform;
            while (t != _root.transform && t)
            {
                if (t.CompareTag("EditorOnly")) return true;
                t = t.parent;
            }
            return false;
        }
    }
}