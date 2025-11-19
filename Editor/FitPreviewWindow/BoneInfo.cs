#nullable enable
using System;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.preview;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    internal class BoneInfo
    {
        public readonly Transform Original;
        public readonly Transform Proxy;
        public readonly Transform Control;
        public Transform FinalRender => Control;
        public int RefCount;
        public readonly BoneInfo? Parent;
        public Pose ProxyPose, DeltaPose;

        public readonly int Depth;
        public readonly Transform? PBExcluded;
        
        public event Action OnDestroy;

        public BoneInfo(
            Scene controlScene,
            Transform original,
            Transform proxy,
            BoneInfo? parent
        )
        {
            Parent = parent;
            Original = original;
            Proxy = proxy;

            var previewScene = NDMFPreviewSceneManager.GetPreviewScene();
            if (!controlScene.IsValid() || !previewScene.IsValid())
            {
                throw new Exception("Scene is not valid");
            }

            if (parent?.Proxy != proxy.parent)
            {
                // Moved by Merge Armature
                PBExcluded = original;
            }

            if (original.TryGetComponent<ModularAvatarPBBlocker>(out _))
            {
                PBExcluded = original;
            }

            var control = new GameObject(original.name);
            control.hideFlags = HideFlags.DontSave;
            SceneManager.MoveGameObjectToScene(control, controlScene);
            if (parent != null)
            {
                control.transform.SetParent(parent.Control, false);
            }

            Control = control.transform;

            ResetPose();
            RefCount = 0;
            Depth = parent == null ? 0 : parent.Depth + 1;
        }

        internal void ResetPose()
        {
            if (Original == null || Control == null) return;
            var pose = GetVirtualProxyPose();
            pose.ToTransform(Control);

            ProxyPose = pose;
            DeltaPose = pose.DeltaTo(Pose.FromTransform(Control));
        }
        
        private Pose GetVirtualProxyPose()
        {
            // Compute the proxy pose relative to the parent proxy pose
            // This isn't the same as the local transform, as we might be applying
            // merge armature, which requires that we adjust this relationship to retarget the
            // merged parent.
            var proxyToWorld = Proxy.localToWorldMatrix;
            var worldToParent = Parent?.Proxy?.worldToLocalMatrix ?? Matrix4x4.identity;
            var proxyToParent = worldToParent * proxyToWorld;

            return new Pose
            {
                position = proxyToParent.GetPosition(),
                rotation = proxyToParent.rotation,
                scale = proxyToParent.lossyScale
            };
        }

        public void Update()
        {
            if (Original == null || Control == null) return;

            Control.name = Original.name;

            var curProxyPose = GetVirtualProxyPose();
            if (Pose.Approximately(curProxyPose, ProxyPose))
            {
                DeltaPose = curProxyPose.DeltaTo(Pose.FromTransform(Control));
            }
            else
            {
                ProxyPose = curProxyPose;
                var targetPose = ProxyPose;
                targetPose.Apply(DeltaPose);
                targetPose.ToTransform(Control);
            }
        }

        public void AddRef()
        {
            RefCount++;
        }

        public void RemoveRef()
        {
            RefCount--;
        }

        public void Destroy()
        {
            OnDestroy?.Invoke();
            if (Control != null)
            {
                Object.DestroyImmediate(Control.gameObject);
            }
        }
    }
}