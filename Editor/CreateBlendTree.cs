using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class CreateBlendTree
    {
        [MenuItem("Assets/Create/BlendTree", priority = 411)]
        static void CreateNewBlendTree()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
#if UNITY_6000_6_OR_NEWER
               EntityId.None,
#else
               0,
#endif
               Editor.CreateInstance<DoCreateBlendTree>(),
               "New BlendTree.asset",
               EditorGUIUtility.IconContent("BlendTree Icon").image as Texture2D,
               null);
        }
#if UNITY_6000_6_OR_NEWER
        class DoCreateBlendTree : AssetCreationEndAction
        {
            public override void Action(EntityId entityId, string pathName, string resourceFile)
            {
                BlendTree blendTree = new BlendTree { name = Path.GetFileNameWithoutExtension(pathName) };
                AssetDatabase.CreateAsset(blendTree, pathName);
                Selection.activeObject = blendTree;
            }
        }
#else
        class DoCreateBlendTree : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                BlendTree blendTree = new BlendTree { name = Path.GetFileNameWithoutExtension(pathName) };
                AssetDatabase.CreateAsset(blendTree, pathName);
                Selection.activeObject = blendTree;
            }
        }
#endif
    }
}
