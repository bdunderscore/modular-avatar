#region

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomEditor(typeof(ModularAvatarMaterialSwap))]
    internal class MaterialSwapEditor : ObjectSwapEditor<Material, MatSwap, ModularAvatarMaterialSwap>
    {
        internal override IEnumerable<Object> GetObjects(SerializedProperty property)
        {
            var swap = property.serializedObject.targetObject as ModularAvatarMaterialSwap;
            if (swap == null)
            {
                return Enumerable.Empty<Object>();
            }
            var root = swap.Root.Get(swap)?.transform ?? RuntimeUtil.FindAvatarTransformInParents(swap.transform);
            if (root == null)
            {
                return Enumerable.Empty<Object>();
            }

            return root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(x => x.sharedMaterials)
                .Where(x => x != null)
                .Distinct();
        }
    }
}
