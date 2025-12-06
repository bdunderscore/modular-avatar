#if MA_VRCSDK3_AVATARS

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
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");

        var mesh = AssertPreviewDeletion(root);

        AssertBuildDeletion(mesh, root);
    }

    [Test]
    public void WhenAnimationExistsForOtherShape_DoesDelete()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest_WithAnimation.prefab");
        
        var mesh = AssertPreviewDeletion(root);

        AssertBuildDeletion(mesh, root);
    }

    [Test]
    public void WhenChangerParentedUnderMesh_DoesDelete()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest_UnderParent.prefab");
        
        var mesh = AssertPreviewDeletion(root);

        AssertBuildDeletion(mesh, root);
    }

    [Test]
    public void WhenShapeDeletionIsSetOnSubsequentChanger_DoesNotDelete()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        root.transform.Find("MenuSet").gameObject.SetActive(true);

        AssertPreviewDeletion(root);
        AssertNoMeshDeletion(root);

        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        // menu is initially inactive
        Assert.AreEqual(50.0f, mesh.GetBlendShapeWeight(mesh.sharedMesh.GetBlendShapeIndex("bottom")));
    }

    [Test]
    public void WhenShapeDeletionIsSetOnSubsequentChanger_AndMenuIsDefaultOn_DoesNotPreviewDelete()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        root.transform.Find("MenuSet").gameObject.SetActive(true);
        root.transform.Find("MenuSet").GetComponent<ModularAvatarMenuItem>().isDefault = true;

        AssertNoPreviewDeletion(root);
        AssertNoMeshDeletion(root);

        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        // menu is initially active
        Assert.AreEqual(0.0f, mesh.GetBlendShapeWeight(mesh.sharedMesh.GetBlendShapeIndex("bottom")));
    }

    [Test]
    public void WhenShapeChangerOverrides_DoesNotDelete()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        
        root.transform.Find("NullSet").gameObject.SetActive(true);

        AssertNoPreviewDeletion(root);
        AssertNoMeshDeletion(root);
    }
    
    [Test]
    public void WhenShapeDeletionIsConditionedOnItself_DoesNotDelete()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        root.transform.Find("Delete").gameObject.AddComponent<ModularAvatarMenuItem>().InitSettings();
        
        AssertNoPreviewDeletion(root);
        AssertNoMeshDeletion(root);
        
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        // deletion action is initially off, so we use the shape changer above it, which is set to 50.
        Assert.AreEqual(50f, mesh.GetBlendShapeWeight(mesh.sharedMesh.GetBlendShapeIndex("bottom")));
    }
    
    [Test]
    public void WhenDeletionIsConditional_GeneratesNaNimationMesh([Values("DeletionTest/NaNimationTest_no_bones.prefab", "DeletionTest/NaNimationTest.prefab")] string prefabPath)
    {
        var root = CreatePrefab(prefabPath);
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();

        AssertNoPreviewDeletion(root);
        
        AvatarProcessor.ProcessAvatar(root);

        var createdBone = root.GetComponentsInChildren<Transform>()
            .Any(c => c.gameObject.name.StartsWith(NaNimationFilter.NaNimatedBonePrefix));
        Assert.IsTrue(createdBone);
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
        Assert.That(activeGroup?.Value is IVertexFilter);
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
            Assert.IsFalse(activeGroup?.Value is IVertexFilter);
        }
        
    }

    private static void AssertNoMeshDeletion(GameObject root)
    {
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var originalSharedMesh = mesh.sharedMesh;
        AvatarProcessor.ProcessAvatar(root);
        Assert.AreEqual(originalSharedMesh.vertexCount, mesh.sharedMesh.vertexCount);
    }
    
    private static void AssertMeshDeletion(GameObject root)
    {
        var mesh = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var originalSharedMesh = mesh.sharedMesh;
        AvatarProcessor.ProcessAvatar(root);
        Assert.AreNotEqual(originalSharedMesh.vertexCount, mesh.sharedMesh.vertexCount);
    }
}

#endif