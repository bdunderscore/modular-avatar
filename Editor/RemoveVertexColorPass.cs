#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class RemoveVertexColorPass : Pass<RemoveVertexColorPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var removers = context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarRemoveVertexColor>(true)!;

            Dictionary<Mesh, Mesh> conversionMap = new();
            
            foreach (var remover in removers)
            {
                foreach (var smr in remover!.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    TryRemove(context.IsTemporaryAsset, smr, conversionMap);
                }
                
                foreach (var mf in remover.GetComponentsInChildren<MeshFilter>(true))
                {
                    TryRemove(context.IsTemporaryAsset, mf, conversionMap);
                }
            }
        }

        private const string PropPath = "m_Mesh";

        private static void TryRemove(
            Func<Mesh, bool> isTempAsset,
            Component c,
            Dictionary<Mesh, Mesh> conversionMap
        )
        {
            var nearestRemover = c.GetComponentInParent<ModularAvatarRemoveVertexColor>()!;
            if (nearestRemover.Mode != ModularAvatarRemoveVertexColor.RemoveMode.Remove) return;

            ForceRemove(isTempAsset, c, conversionMap);
        }

        internal static void ForceRemove(Func<Mesh, bool> isTempAsset, Component c,
            Dictionary<Mesh, Mesh> conversionMap)
        {
            var obj = new SerializedObject(c);
            var prop = obj.FindProperty("m_Mesh");
            if (prop == null)
            {
                throw new Exception("Property not found: " + PropPath);
            }

            var mesh = prop.objectReferenceValue as Mesh;
            if (mesh == null)
            {
                return;
            }

            var originalMesh = mesh;

            if (conversionMap.TryGetValue(mesh, out var converted))
            {
                prop.objectReferenceValue = converted;
                obj.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            if (mesh.GetVertexAttributes().All(va => va.attribute != VertexAttribute.Color))
            {
                // no-op
                return;
            }

            if (!isTempAsset(mesh))
            {
                mesh = Object.Instantiate(mesh);
                prop.objectReferenceValue = mesh;
                obj.ApplyModifiedPropertiesWithoutUndo();
            }

            mesh.colors = null;

            conversionMap[originalMesh] = mesh;
        }
    }
}