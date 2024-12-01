using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class RemoveVertexColorPass : Pass<RemoveVertexColorPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var removers = context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarRemoveVertexColor>(true);

            Dictionary<Mesh, Mesh> conversionMap = new();
            
            foreach (var remover in removers)
            {
                foreach (var smr in remover.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    TryRemove(context, smr, conversionMap);
                }
                
                foreach (var mf in remover.GetComponentsInChildren<MeshFilter>(true))
                {
                    TryRemove(context, mf, conversionMap);
                }
            }
        }

        private const string PROP_PATH = "m_Mesh";

        private void TryRemove(ndmf.BuildContext context, Component c, Dictionary<Mesh, Mesh> conversionMap)
        {
            SerializedObject obj;
            SerializedProperty prop;

            if (c is SkinnedMeshRenderer smr)
            {
                obj = new SerializedObject(smr);
                prop = obj.FindProperty(PROP_PATH);
            } else if (c is MeshFilter mf)
            {
                obj = new SerializedObject(mf);
                prop = obj.FindProperty(PROP_PATH);
            } else
            {
                //Debug.LogWarning($"Could not find a mesh to remove vertex colors from on {remover.name}");
                // TODO: warning
                return;
            }
            
            var mesh = prop.objectReferenceValue as Mesh;
            Mesh originalMesh = mesh;
            if (mesh == null)
            {
                // TODO: warning;
                return;
            }
            
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

            if (!context.IsTemporaryAsset(mesh))
            {
                mesh = UnityEngine.Object.Instantiate(mesh);
                prop.objectReferenceValue = mesh;
                obj.ApplyModifiedPropertiesWithoutUndo();
            }

            mesh.colors = null;

            conversionMap[originalMesh] = mesh;
        }
    }
}