using System.Collections;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

public class ShapeDeletionAnalysis : TestBase
{
    [Test]
    public void BasicShapeDeletionAnalysis()
    {
        var root = CreatePrefab("DeletionTest.prefab");

        var mesh = AssertPreviewDeletion(root);

        AssertBuildDeletion(mesh, root);
    }

    [Test]
    public void WhenShapeDeletionIsConditionedOnSubsequentChanger_DoesNotDelete()
    {
        var root = CreatePrefab("DeletionTest.prefab");
        root.transform.Find("MenuSet").gameObject.SetActive(true);

        AssertPreviewDeletion(root);
        AssertNoMeshDeletion(root);
        
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.AreEqual(100, mesh.GetBlendShapeWeight(mesh.sharedMesh.GetBlendShapeIndex("bottom")));
    }


    [Test]
    public void WhenShapeDeletionIsConditionedOnItself_DoesNotDelete()
    {
        var root = CreatePrefab("DeletionTest.prefab");
        root.transform.Find("Delete").gameObject.AddComponent<ModularAvatarMenuItem>().InitSettings();
        
        AssertNoPreviewDeletion(root);
        AssertNoMeshDeletion(root);
        
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        // deletion action is initially off, so we use the shape changer above it, which is set to 50.
        Assert.AreEqual(50f, mesh.GetBlendShapeWeight(mesh.sharedMesh.GetBlendShapeIndex("bottom")));
    }
    
    private static void AssertBuildDeletion(SkinnedMeshRenderer mesh, GameObject root)
    {
        var originalSharedMesh = mesh.sharedMesh;
        AvatarProcessor.ProcessAvatar(root);
        Assert.AreNotEqual(originalSharedMesh, mesh.sharedMesh);

        Assert.IsTrue(mesh.sharedMesh.vertices.All(v => v.z >= 0));
    }

    private static SkinnedMeshRenderer AssertPreviewDeletion(GameObject root)
    {
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var analysis = new ReactiveObjectAnalyzer().Analyze(root);
        var deletedShape = analysis.Shapes.GetValueOrDefault(new TargetProp()
        {
            TargetObject = mesh,
            PropertyName = "deletedShape.bottom"
        });
        Assert.IsNotNull(deletedShape);
        var activeGroup = deletedShape.actionGroups.LastOrDefault(ag => ag.InitiallyActive);
        Assert.AreEqual(1.0f, activeGroup?.Value);
        return mesh;
    }
    
    private static void AssertNoPreviewDeletion(GameObject root)
    {
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var analysis = new ReactiveObjectAnalyzer().Analyze(root);
        var deletedShape = analysis.Shapes.GetValueOrDefault(new TargetProp()
        {
            TargetObject = mesh,
            PropertyName = "deletedShape.bottom"
        });
        if (deletedShape != null)
        {
            var activeGroup = deletedShape.actionGroups.LastOrDefault(ag => ag.InitiallyActive);
            Assert.IsFalse(activeGroup?.Value is float f && f > 0);
        }
        
    }

    private static void AssertNoMeshDeletion(GameObject root)
    {
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var originalSharedMesh = mesh.sharedMesh;
        AvatarProcessor.ProcessAvatar(root);
        Assert.AreEqual(originalSharedMesh, mesh.sharedMesh);
    }
}
