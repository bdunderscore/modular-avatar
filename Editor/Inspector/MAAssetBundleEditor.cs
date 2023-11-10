using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(MAAssetBundle))]
    class MAAssetBundleEditor : MAEditorBase
    {
        protected override void OnInnerInspectorGUI()
        {
            if (GUILayout.Button("Unpack"))
            {
                foreach (var target in targets)
                {
                    MAAssetBundle bundle = (MAAssetBundle) target;
                    MAAssetBundleExtractor.Unpack(bundle);
                }
            }
        }
    }

    public class MAAssetBundleExtractor
    {
        private static readonly ISet<Type> RootAssets = new HashSet<Type>()
        {
            typeof(Mesh),
            typeof(AnimationClip),
            typeof(RuntimeAnimatorController),
#if MA_VRCSDK3_AVATARS
            typeof(VRCExpressionParameters),
            typeof(VRCExpressionsMenu),
#endif
        };

        private Dictionary<UnityEngine.Object, AssetInfo> _assets;
        private MAAssetBundle Bundle;
        private HashSet<Object> _unassigned;

        private MAAssetBundleExtractor(MAAssetBundle bundle)
        {
            _assets = GetContainedAssets(bundle);
            this.Bundle = bundle;
        }

        class AssetInfo
        {
            public readonly UnityEngine.Object Asset;
            public readonly HashSet<AssetInfo> IncomingReferences = new HashSet<AssetInfo>();
            public readonly HashSet<AssetInfo> OutgoingReferences = new HashSet<AssetInfo>();

            public AssetInfo Root;

            public AssetInfo(UnityEngine.Object obj)
            {
                this.Asset = obj;
            }

            public void PopulateReferences(Dictionary<UnityEngine.Object, AssetInfo> assets)
            {
                switch (Asset)
                {
                    case Mesh _:
                    case AnimationClip _:
#if MA_VRCSDK3_AVATARS
                    case VRCExpressionsMenu _:
                    case VRCExpressionParameters _:
#endif
                        return; // No child objects
                }

                var so = new SerializedObject(Asset);
                var prop = so.GetIterator();

                // TODO extract to common code
                bool enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = true;
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var value = prop.objectReferenceValue;
                        if (value != null && assets.TryGetValue(value, out var target))
                        {
                            OutgoingReferences.Add(target);
                            target.IncomingReferences.Add(this);
                        }
                    }
                    else if (prop.propertyType == SerializedPropertyType.String)
                    {
                        enterChildren = false;
                    }
                }
            }

            public void ForceAssignRoot()
            {
                // First, see if we're reachable only from one root.
                HashSet<AssetInfo> visited = new HashSet<AssetInfo>();
                HashSet<AssetInfo> roots = new HashSet<AssetInfo>();
                Queue<AssetInfo> queue = new Queue<AssetInfo>();
                visited.Add(this);
                queue.Enqueue(this);

                while (queue.Count > 0 && roots.Count < 2)
                {
                    var next = queue.Dequeue();
                    if (next.Root != null)
                    {
                        roots.Add(next.Root);
                    }

                    foreach (var outgoingReference in next.IncomingReferences)
                    {
                        if (visited.Add(outgoingReference))
                        {
                            queue.Enqueue(outgoingReference);
                        }
                    }
                }

                if (roots.Count == 1)
                {
                    this.Root = roots.First();
                }
                else
                {
                    this.Root = this;
                }
            }
        }

        public static void Unpack(MAAssetBundle bundle)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                new MAAssetBundleExtractor(bundle).Extract();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }


        private bool TryAssignRoot(AssetInfo info)
        {
            if (info.Root != null)
            {
                return true;
            }

            if (RootAssets.Any(t => t.IsInstanceOfType(info.Asset)) || info.IncomingReferences.Count == 0)
            {
                info.Root = info;
                return true;
            }

            var firstRoot = info.IncomingReferences.First().Root;
            if (firstRoot != null && !_unassigned.Contains(firstRoot.Asset)
                                  && info.IncomingReferences.All(t => t.Root == firstRoot))
            {
                info.Root = firstRoot;
                return true;
            }

            return false;
        }

        private void Extract()
        {
            string path = AssetDatabase.GetAssetPath(Bundle);
            var directory = System.IO.Path.GetDirectoryName(path);
            _unassigned = new HashSet<UnityEngine.Object>(_assets.Keys);

            foreach (var info in _assets.Values)
            {
                info.PopulateReferences(_assets);
            }

            var queue = new Queue<UnityEngine.Object>();
            while (_unassigned.Count > 0)
            {
                // Bootstrap
                if (queue.Count == 0)
                {
                    _unassigned.Where(o => TryAssignRoot(_assets[o])).ToList().ForEach(o => { queue.Enqueue(o); });

                    if (queue.Count == 0)
                    {
                        _assets[_unassigned.First()].ForceAssignRoot();
                        queue.Enqueue(_unassigned.First());
                    }
                }

                while (queue.Count > 0)
                {
                    var next = queue.Dequeue();
                    ProcessSingleAsset(directory, next);
                    _unassigned.Remove(next);

                    foreach (var outgoingReference in _assets[next].OutgoingReferences)
                    {
                        if (_unassigned.Contains(outgoingReference.Asset) && TryAssignRoot(outgoingReference))
                        {
                            queue.Enqueue(outgoingReference.Asset);
                        }
                    }
                }
            }

            AssetDatabase.DeleteAsset(path);
        }

        private string AssignAssetFilename(string directory, Object next)
        {
            string assetName = next.name;
            if (string.IsNullOrEmpty(assetName))
            {
                next.name = next.GetType().Name + " " + GUID.Generate().ToString();
                assetName = next.name;
            }

            string assetFile;
            for (int extension = 0;; extension++)
            {
                assetFile = assetName + (extension == 0 ? "" : $" ({extension})") + ".asset";
                assetFile = System.IO.Path.Combine(directory, assetFile);
                if (!System.IO.File.Exists(assetFile))
                {
                    break;
                }
            }

            return assetFile;
        }

        private void ProcessSingleAsset(string directory, Object next)
        {
            AssetDatabase.RemoveObjectFromAsset(next);

            var info = _assets[next];
            if (info.Root != info)
            {
                if (!AssetDatabase.IsMainAsset(info.Root.Asset))
                {
                    throw new Exception(
                        $"Desired root {info.Root.Asset.name} for asset {next.name} is not a root asset");
                }

                AssetDatabase.AddObjectToAsset(next, info.Root.Asset);
            }
            else
            {
                AssetDatabase.CreateAsset(next, AssignAssetFilename(directory, next));
            }
        }

        private static Dictionary<Object, AssetInfo> GetContainedAssets(MAAssetBundle bundle)
        {
            string path = AssetDatabase.GetAssetPath(bundle);
            var rawAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            Dictionary<Object, AssetInfo> infos = new Dictionary<Object, AssetInfo>(rawAssets.Length);
            foreach (var asset in rawAssets)
            {
                if (!(asset is MAAssetBundle))
                {
                    infos.Add(asset, new AssetInfo(asset));
                }
            }


            return infos;
        }
    }
}