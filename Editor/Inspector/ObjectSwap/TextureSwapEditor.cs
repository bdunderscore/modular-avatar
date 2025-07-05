#region

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomEditor(typeof(ModularAvatarTextureSwap))]
    internal class TextureSwapEditor : ObjectSwapEditor<Texture, TexSwap, ModularAvatarTextureSwap>
    {
        internal override IEnumerable<Object> GetObjects(SerializedProperty property)
        {
            var swap = property.serializedObject.targetObject as ModularAvatarTextureSwap;
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
                .SelectMany(x => x.GetTexturePropertyNames(), (x, y) => x.GetTexture(y))
                .Where(x => x != null)
                .Distinct();
        }
    }
}
