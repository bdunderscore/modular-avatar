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
    public void WhenShapeDeletionIsSetOnSubsequentChanger_DoesDelete()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        root.transform.Find("MenuSet").gameObject.SetActive(true);

        AssertPreviewDeletion(root);
        AssertMeshDeletion(root);
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

        var shapeIndex = mesh.sharedMesh.GetBlendShapeIndex(NaNimationFilter.BlendShapeNamePrefix + 0);
        Assert.AreNotEqual(-1, shapeIndex);
        
        var vertices = mesh.sharedMesh.vertices;
        var deltaPositions = new Vector3[vertices.Length];
        
        mesh.sharedMesh.GetBlendShapeFrameVertices(shapeIndex, 0, deltaPositions, null, null);
        
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].z < 0)
            {
                Assert.IsTrue(float.IsInfinity(deltaPositions[i].x),
                    $"Vertex {i} (z={vertices[i].z}) should have infinite delta position");
            }
            else if (vertices[i].z > 0)
            {
                Assert.AreEqual(0, deltaPositions[i].x,
                    $"Vertex {i} (z={vertices[i].z}) should have zero delta position");
            }
        }
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
        Assert.That((activeGroup?.Value as VertexFilterByShape)?.Threshold - 0.01f, Is.LessThanOrEqualTo(0.005f));
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
            Assert.IsFalse(activeGroup?.Value is VertexFilterByShape f && f.Threshold > 0);
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