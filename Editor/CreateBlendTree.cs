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
               0,
               Editor.CreateInstance<DoCreateBlendTree>(),
               "New BlendTree.asset",
               EditorGUIUtility.IconContent("BlendTree Icon").image as Texture2D,
               null);
        }

        class DoCreateBlendTree : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                BlendTree blendTree = new BlendTree { name = Path.GetFileNameWithoutExtension(pathName) };
                AssetDatabase.CreateAsset(blendTree, pathName);
                Selection.activeObject = blendTree;
            }
        }
    }
}
