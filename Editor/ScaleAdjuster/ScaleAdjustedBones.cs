#region

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ScaleAdjuster
{
    internal class ScaleAdjustedBones
    {
        private static int editorFrameCount = 0;
        private static int lastUpdateFrame = 0;
        private static int lastMutatingUpdate = 0;
        private static int mutatingUpdateCount = 0;

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.update += () => editorFrameCount++;
        }

        internal class BoneState
        {
            public Component original;
            public Transform proxy;
            public int lastUsedFrame;
            public BoneState parentHint;
        }

        private readonly Dictionary<Component, BoneState> _bones = new();
        //private List<BoneState> _states = new List<BoneState>();

        public void Clear()
        {
            foreach (var state in _bones.Values)
            {
                if (state.proxy != null) Object.DestroyImmediate(state.proxy.gameObject);
            }

            _bones.Clear();
        }
        
        public BoneState GetBone(Component src, bool force = true)
        {
            if (src == null) return null;

            if (_bones.TryGetValue(src, out var state))
            {
                state.lastUsedFrame = mutatingUpdateCount;
                return state;
            }

            if (!force) return null;

            var proxyObj = new GameObject(src.name);
            proxyObj.hideFlags = HideFlags.HideAndDontSave;
            proxyObj.AddComponent<SelfDestructComponent>().KeepAlive = this;

#if MODULAR_AVATAR_DEBUG_HIDDEN
            proxyObj.hideFlags = HideFlags.DontSave;
#endif

            var boneState = new BoneState();
            boneState.original = src;
            boneState.proxy = proxyObj.transform;
            boneState.parentHint = null;
            boneState.lastUsedFrame = Time.frameCount;

            _bones[src] = boneState;

            CheckParent(CopyState(boneState), boneState);

            return boneState;
        }

        private List<Component> toRemove = new List<Component>();
        private List<BoneState> stateList = new List<BoneState>();

        public void Update()
        {
            if (lastUpdateFrame == editorFrameCount)
            {
                return;
            }

            lastUpdateFrame = editorFrameCount;
            
            if (lastMutatingUpdate != editorFrameCount)
            {
                mutatingUpdateCount++;
                lastMutatingUpdate = editorFrameCount;
            }

            toRemove.Clear();

            stateList.Clear();
            stateList.AddRange(_bones.Values);

            foreach (var entry in stateList)
            {
                if (entry.original == null || entry.proxy == null)
                {
                    if (entry.proxy != null)
                    {
                        Object.DestroyImmediate(entry.proxy.gameObject);
                    }

                    toRemove.Add(entry.original);
                    continue;
                }

                if (mutatingUpdateCount - entry.lastUsedFrame > 5 && entry.proxy.childCount == 0)
                {
                    Object.DestroyImmediate(entry.proxy.gameObject);
                    toRemove.Add(entry.original);
                    continue;
                }

                if (entry.original.gameObject.scene != entry.proxy.gameObject.scene &&
                    entry.proxy.transform.parent == null)
                {
                    SceneManager.MoveGameObjectToScene(entry.proxy.gameObject, entry.original.gameObject.scene);
                }

                Transform parent = CopyState(entry);

                CheckParent(parent, entry);
            }

            foreach (var remove in toRemove)
            {
                _bones.Remove(remove);
            }
        }

        private void CheckParent(Transform parent, BoneState entry)
        {
            if (parent != entry.parentHint?.original)
            {
                entry.parentHint = GetBone(parent);
                entry.proxy.SetParent(entry.parentHint?.proxy, false);
            }
        }

        private static Transform CopyState(BoneState entry)
        {
            Transform parent;
            if (entry.original is Transform t)
            {
                parent = t.parent;

                entry.proxy.localPosition = t.localPosition;
                entry.proxy.localRotation = t.localRotation;
                entry.proxy.localScale = t.localScale;
            }
            else
            {
                parent = entry.original.transform;

                if (entry.original is ModularAvatarScaleAdjuster sa)
                {
                    entry.proxy.localPosition = Vector3.zero;
                    entry.proxy.localRotation = Quaternion.identity;
                    entry.proxy.localScale = sa.Scale;
                }
            }

            return parent;
        }
    }
}