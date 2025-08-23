using System.Linq;
using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using AvatarProcessor = nadena.dev.ndmf.AvatarProcessor;
using Object = UnityEngine.Object;

public class ModularAvatarMeshCutterTest : TestBase
{
    /*
     * Test artifacts:
     *
     * `three planes test mesh.fbx` - a mesh with three planes, aligned on the XY axis, at positions Z=0, Z=1, and Z=2.
     * Three blendshapes are provided, which manipulate each mesh: Positive moves the Z=2 plane, Negative moves the Z=0
     * plane, and Center moves the Z=1 plane.
     */
    
    private GameObject avatarRoot;
    private GameObject meshObject;
    private SkinnedMeshRenderer meshRenderer;
    private Mesh testMesh;
    
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        
        // Load the test mesh
        testMesh = LoadAsset<Mesh>("three planes test mesh.fbx");
        
        // Create avatar root and mesh object
        avatarRoot = CreateRoot("MeshCutterTestAvatar");
        meshObject = CreateChild(avatarRoot, "MeshObject");
        
        meshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();
        meshRenderer.sharedMesh = testMesh;
        
        // Ensure consistent transforms
        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
        meshObject.transform.localPosition = Vector3.zero;
        meshObject.transform.localRotation = Quaternion.identity;
        meshObject.transform.localScale = Vector3.one;
    }
    
    [Test]
    public void TestMeshCutterAlone_NoChanges()
    {
        // Add mesh cutter targeting the mesh object
        var meshCutter = avatarRoot.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);
        
        var originalVertexCount = testMesh.vertexCount;
        var originalTriangleCount = testMesh.triangles.Length;
        
        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // The mesh should remain unchanged when only a cutter is present (no filters)
        Assert.AreEqual(originalVertexCount, processedMesh.vertexCount, 
            "Vertex count should remain unchanged with mesh cutter alone");
        Assert.AreEqual(originalTriangleCount, processedMesh.triangles.Length, 
            "Triangle count should remain unchanged with mesh cutter alone");
    }
    
    [Test]
    public void TestMeshCutterWithVertexFilter_DeletesVertices()
    {
        // Add mesh cutter
        var meshCutter = meshObject.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);
        
        // Add vertex filter to select vertices with Z > 1.5 (Z=2 plane)
        var vertexFilter = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter.Center = new Vector3(0, 0, 1.5f);
        vertexFilter.Axis = Vector3.forward;
        
        var originalVertexCount = testMesh.vertexCount;
        var originalVertices = testMesh.vertices;
        
        // Count vertices that should be deleted (Z > 1.5)
        int verticesToDelete = originalVertices.Count(v => v.z > 1.5f);
        Assert.Greater(verticesToDelete, 0, "Some vertices should be selected for deletion");
        
        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // Verify vertices were deleted
        Assert.Less(processedMesh.vertexCount, originalVertexCount, 
            "Vertex count should decrease after deletion");
        
        // Verify the remaining vertices don't include the deleted ones
        var processedVertices = processedMesh.vertices;
        foreach (var vertex in processedVertices)
        {
            Assert.LessOrEqual(vertex.z, 1.5f, 
                $"Remaining vertex at {vertex} should not be from the deleted Z=2 plane");
        }
    }
    
    [Test]
    public void TestMeshCutterWithTwoVertexFilters_AndCondition_NaNimate()
    {
        // Add mesh cutter
        var meshCutter = meshObject.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);
        
        // Add a Menu Item to ensure that we use the NanImation path
        meshCutter.gameObject.AddComponent<ModularAvatarMenuItem>();
        
        // Add first vertex filter (Z > 0.5, should select Z=1 and Z=2 planes)
        var vertexFilter1 = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter1.Center = new Vector3(0, 0, 0.5f);
        vertexFilter1.Axis = Vector3.forward;
        
        // Add second vertex filter (Z > 1.5, should select only Z=0 and Z=2 plane)
        var vertexFilter2 = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter2.Center = new Vector3(0, 0, 1.5f);
        vertexFilter2.Axis = -Vector3.forward;
        
        var originalBindposeCount = testMesh.bindposeCount;

        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // Verify the correct vertices were deleted (AND condition: Z > 1.5)
        var processedVertices = processedMesh.vertices;
        var weights = processedMesh.boneWeights;
        
        // Verify that two new bones were created (root + NaNimation)
        Assert.AreEqual(originalBindposeCount + 2, processedMesh.bindposeCount,
            "An additional root bone should be created for unskinned mesh");

        var tris = processedMesh.triangles;

        for (int i = 0; i < tris.Length; i += 3)
        {
            bool shouldHaveBeenDeleted = false;
            bool wasDeleted = false;
            for (int v = 3; v < i + 3; v++)
            {
                var pos = processedVertices[tris[v]];
                if (pos.z > 0.5f && pos.z < 1.5f) shouldHaveBeenDeleted = true;
                switch (weights[tris[v]].boneIndex0)
                {
                    case 0: break; // not deleted
                    case 1: wasDeleted = true; break;
                    default: Assert.Fail($"Unexpected bone index {weights[v].boneIndex0} on vertex {pos}"); break;
                }
            }
            
            Assert.AreEqual(shouldHaveBeenDeleted, wasDeleted, 
                $"Triangle at {i} should have {(shouldHaveBeenDeleted ? "" : " not")} been deleted");
        }
    }
    
    
    [Test]
    public void TestMeshCutterWithTwoVertexFilters_AndCondition_Erase()
    {
        // Add mesh cutter
        var meshCutter = meshObject.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);

        // Add first vertex filter (Z > 0.5, should select Z=1 and Z=2 planes)
        var vertexFilter1 = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter1.Center = new Vector3(0, 0, 0.5f);
        vertexFilter1.Axis = Vector3.forward;
        
        // Add second vertex filter (Z > 1.5, should select only Z=0 and Z=2 plane)
        var vertexFilter2 = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter2.Center = new Vector3(0, 0, 1.5f);
        vertexFilter2.Axis = -Vector3.forward;
        
        var originalBindposeCount = testMesh.bindposeCount;
        
        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // Verify the correct vertices were deleted (AND condition: Z > 1.5)
        var processedVertices = processedMesh.vertices;

        foreach (var pos in processedVertices)
        {
            if (pos.z > 0.5f && pos.z < 1.5f)
            {
                Assert.Fail($"Vertex at {pos} should have been deleted");
            }
        }
    }
    
    
    [Test]
    public void TestMeshCutterWithShapeChanger_OrCondition()
    {
        // Add mesh cutter
        var meshCutter = meshObject.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);
        
        // Add vertex filter (Z > 1.5, should select Z=2 plane)
        var vertexFilter = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter.Center = new Vector3(0, 0, 1.5f);
        vertexFilter.Axis = Vector3.forward;
        
        // Add shape changer in delete mode (affects Z=0 plane)
        var shapeChanger = avatarRoot.AddComponent<ModularAvatarShapeChanger>();
        shapeChanger.Shapes.Add(new ChangedShape
        {
            Object = new AvatarObjectReference(),
            ShapeName = "Negative", // This affects the Z=0 plane
            ChangeType = ShapeChangeType.Delete,
            Value = 0 // Value doesn't matter for delete mode
        });
        shapeChanger.Shapes[0].Object.Set(meshObject);
        
        var originalVertices = testMesh.vertices;
        
        // Count vertices that should be deleted (Z > 1.5 OR affected by Negative blendshape)
        int verticesFromAxisFilter = originalVertices.Count(v => v.z > 1.5f);
        int verticesFromShapeFilter = originalVertices.Count(v => Mathf.Abs(v.z - 0.0f) < 0.1f);
        
        Assert.Greater(verticesFromAxisFilter, 0, "Axis filter should select some vertices");
        Assert.Greater(verticesFromShapeFilter, 0, "Shape filter should select some vertices");
        
        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // Verify the correct vertices were deleted (OR condition)
        var processedVertices = processedMesh.vertices;
        foreach (var vertex in processedVertices)
        {
            bool shouldHaveBeenDeletedByAxis = vertex.z > 1.5f;
            bool shouldHaveBeenDeletedByShape = Mathf.Abs(vertex.z - 0.0f) < 0.1f;
            bool shouldHaveBeenDeleted = shouldHaveBeenDeletedByAxis || shouldHaveBeenDeletedByShape;
            
            Assert.IsFalse(shouldHaveBeenDeleted, 
                $"Vertex at {vertex} should have been deleted by OR condition");
        }
    }
    
    [Test]
    public void TestMeshCutterWithMenuItem_NaNimation()
    {
        // Add mesh cutter
        var meshCutter = meshObject.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);
        
        // Add vertex filter (Z > 1.5, should select Z=2 plane)
        var vertexFilter = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter.Center = new Vector3(0, 0, 1.5f);
        vertexFilter.Axis = Vector3.forward;
        
        // Add menu item (default configuration enables NaNimation)
        var menuItem = meshObject.AddComponent<ModularAvatarMenuItem>();
        menuItem.Control.name = "Test Toggle";
        
        var originalVertexCount = testMesh.vertexCount;
        var originalTriangleCount = testMesh.triangles.Length;
        var originalBindposeCount = testMesh.bindposeCount;
        
        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // Verify that we've augmented the unskinned mesh with two new bones (and identity bindposes)
        Assert.AreEqual(2, processedMesh.bindposeCount, 
            "Two new bones should be added for NaNimation");
        var bindposes = processedMesh.bindposes;
        Assert.AreEqual(Matrix4x4.identity, bindposes[0], "First NaNimation bindpose should be identity");
        Assert.AreEqual(Matrix4x4.identity, bindposes[1], "Second NaNimation bindpose should be identity");
        
        // Verify that bones have been added to the SkinnedMeshRenderer
        var smrBones = meshRenderer.bones;
        Assert.AreEqual(2, smrBones.Length, "SkinnedMeshRenderer should have two bones for NaNimation");
        
        // Verify mesh structure is preserved (no vertex deletion with NaNimation)
        Assert.AreEqual(originalVertexCount, processedMesh.vertexCount, 
            "Vertex count should remain the same with NaNimation");
        Assert.AreEqual(originalTriangleCount, processedMesh.triangles.Length, 
            "Triangle count should remain the same with NaNimation");
        
        // Verify new bones were added for NaNimation
        Assert.Greater(processedMesh.bindposeCount, originalBindposeCount, 
            "New bones should be added for NaNimation");
        
        // Verify bone weights were modified
        var boneWeights = processedMesh.GetAllBoneWeights();
        var bonesPerVertex = processedMesh.GetBonesPerVertex();
        
        if (boneWeights.Length > 0)
        {
            // Check that some vertices now reference the new bones (indices >= original count)
            bool foundNewBoneReference = false;
            
            foreach (var boneWeight in boneWeights)
            {
                if (boneWeight.boneIndex >= originalBindposeCount)
                {
                    foundNewBoneReference = true;
                    break;
                }
            }
            
            Assert.IsTrue(foundNewBoneReference, 
                "Some vertices should reference the newly created NaNimation bones");
        }
        
        // Verify that polygons containing filtered vertices have at least one vertex weighted to a new bone
        var originalVertices = testMesh.vertices;
        var processedVertices = processedMesh.vertices;
        
        // Find vertices that should be affected by the filter
        var affectedVertices = new bool[originalVertices.Length];
        for (int i = 0; i < originalVertices.Length; i++)
        {
            affectedVertices[i] = originalVertices[i].z > 1.5f;
        }
        
        if (affectedVertices.Any(a => a) && boneWeights.Length > 0)
        {
            // Check triangles containing affected vertices
            var triangles = processedMesh.triangles;
            bool foundAffectedTriangleWithNewBone = false;
            
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int v0 = triangles[t];
                int v1 = triangles[t + 1];
                int v2 = triangles[t + 2];
                
                // Check if this triangle contains any affected vertices
                if (affectedVertices[v0] || affectedVertices[v1] || affectedVertices[v2])
                {
                    // Check if any vertex in this triangle references a new bone
                    foreach (int vertexIndex in new[] { v0, v1, v2 })
                    {
                        int startIndex = 0;
                        for (int i = 0; i < vertexIndex; i++)
                        {
                            startIndex += bonesPerVertex[i];
                        }
                        
                        for (int b = 0; b < bonesPerVertex[vertexIndex]; b++)
                        {
                            if (boneWeights[startIndex + b].boneIndex >= originalBindposeCount)
                            {
                                foundAffectedTriangleWithNewBone = true;
                                break;
                            }
                        }
                        
                        if (foundAffectedTriangleWithNewBone) break;
                    }
                    
                    if (foundAffectedTriangleWithNewBone) break;
                }
            }
            
            Assert.IsTrue(foundAffectedTriangleWithNewBone, 
                "Triangles containing affected vertices should have at least one vertex weighted to a new bone");
        }
    }
    
    [Test]
    public void TestMeshCutterAllVerticesSelected_DegenerateTriangle()
    {
        // Add mesh cutter
        var meshCutter = meshObject.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);
        
        // Add vertex filter that selects ALL vertices (Z > -1, which should include all planes)
        var vertexFilter = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter.Center = new Vector3(0, 0, -1);
        vertexFilter.Axis = Vector3.forward;
        
        var originalVertices = testMesh.vertices;
        
        // Verify that all vertices would be selected
        int verticesSelected = originalVertices.Count(v => v.z > -1.0f);
        Assert.AreEqual(originalVertices.Length, verticesSelected, "All vertices should be selected for deletion");
        
        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // Verify the mesh has been reduced to a degenerate state
        Assert.AreEqual(1, processedMesh.vertexCount, "Mesh should have exactly one vertex when all are deleted");
        Assert.AreEqual(3, processedMesh.triangles.Length, "Mesh should have exactly one triangle (3 indices)");
        
        // Verify all triangle indices point to the same vertex (index 0)
        var triangles = processedMesh.triangles;
        Assert.AreEqual(0, triangles[0], "First triangle index should be 0");
        Assert.AreEqual(0, triangles[1], "Second triangle index should be 0");
        Assert.AreEqual(0, triangles[2], "Third triangle index should be 0");
        
        var processedVertices = processedMesh.vertices;
        Assert.AreEqual(1, processedVertices.Length, "Should have exactly one vertex");
        
        // The vertex position should be finite (not NaN or infinity)
        var vertex = processedVertices[0];
        Assert.IsTrue(float.IsFinite(vertex.x), "Vertex X should be finite");
        Assert.IsTrue(float.IsFinite(vertex.y), "Vertex Y should be finite");
        Assert.IsTrue(float.IsFinite(vertex.z), "Vertex Z should be finite");
    }
    
    [Test]
    public void TestMeshCutterMultipleSubmeshes()
    {
        // Load the multiple submesh test asset
        var multiSubmeshMesh = LoadAsset<Mesh>("multiple submesh.fbx");
        
        // Replace the test mesh with the multi-submesh one
        meshRenderer.sharedMesh = multiSubmeshMesh;
        
        // Add mesh cutter
        var meshCutter = meshObject.AddComponent<ModularAvatarMeshCutter>();
        meshCutter.Object.Set(meshObject);
        
        // Add vertex filter with Y > 0 (axis Y=1, position 0,-3,0)
        var vertexFilter = meshObject.AddComponent<VertexFilterByAxisComponent>();
        vertexFilter.Center = Vector3.up * -2.5f;
        vertexFilter.Axis = Vector3.up;
        
        var originalVertices = multiSubmeshMesh.vertices;
        var originalSubmeshCount = multiSubmeshMesh.subMeshCount;
        
        Assert.AreEqual(2, originalSubmeshCount, "Test mesh should have exactly 2 submeshes");
        
        // Count vertices that should be deleted from each submesh (Y > 0)
        int totalVerticesToDelete = originalVertices.Count(v => v.y > -2.5f);
        Assert.Greater(totalVerticesToDelete, 0, "Some vertices should be selected for deletion");
        
        // Process the avatar
        AvatarProcessor.ProcessAvatar(avatarRoot);
        
        // Get the processed mesh
        var processedMesh = meshRenderer.sharedMesh;
        
        // Verify vertices were deleted
        Assert.Less(processedMesh.vertexCount, multiSubmeshMesh.vertexCount, 
            "Vertex count should decrease after deletion");
        
        // Verify the mesh still has the same number of submeshes
        Assert.AreEqual(originalSubmeshCount, processedMesh.subMeshCount, 
            "Submesh count should remain the same after processing");
        
        // Verify the remaining vertices don't include the deleted ones (Y > -2.5f)
        var processedVertices = processedMesh.vertices;
        foreach (var vertex in processedVertices)
        {
            Assert.LessOrEqual(vertex.y, -2.5f, 
                $"Remaining vertex at {vertex} should not have Y > -2.5");
        }
        
        // Verify both submeshes still have triangles (assuming the original mesh had vertices with Y <= 0 in both submeshes)
        for (int submeshIndex = 0; submeshIndex < processedMesh.subMeshCount; submeshIndex++)
        {
            var submeshTriangles = processedMesh.GetTriangles(submeshIndex);
            Assert.Greater(submeshTriangles.Length, 0, 
                $"Submesh {submeshIndex} should still have triangles after processing");
            
            // Verify all triangle indices are valid
            foreach (var triangleIndex in submeshTriangles)
            {
                Assert.GreaterOrEqual(triangleIndex, 0, "Triangle index should be non-negative");
                Assert.Less(triangleIndex, processedMesh.vertexCount, "Triangle index should be within vertex count");
            }
        }
        
        // Verify that triangles in each submesh reference vertices that weren't deleted
        for (int submeshIndex = 0; submeshIndex < processedMesh.subMeshCount; submeshIndex++)
        {
            var submeshTriangles = processedMesh.GetTriangles(submeshIndex);
            
            for (int i = 0; i < submeshTriangles.Length; i++)
            {
                var vertexIndex = submeshTriangles[i];
                var vertex = processedVertices[vertexIndex];
                
                Assert.LessOrEqual(vertex.y, -2.5f, 
                    $"Submesh {submeshIndex} triangle vertex at {vertex} should not have been deleted (Y > -2.5)");
            }
        }
    }

    [Test]
    public void TestMeshCutterObjectReference()
    {
        var meshCutter = avatarRoot.AddComponent<ModularAvatarMeshCutter>();
        
        // Test null object reference throws exception
        Assert.Throws<System.ArgumentNullException>(() =>
        {
            meshCutter.Object = null;
        });
        
        // Test valid object reference
        meshCutter.Object = new AvatarObjectReference();
        meshCutter.Object.Set(meshObject);
        Assert.IsNotNull(meshCutter.Object);
        Assert.AreEqual(meshObject, meshCutter.Object.Get(meshCutter));
        
        // Test GetObjectReferences
        var objRefs = meshCutter.GetObjectReferences().ToList();
        Assert.AreEqual(1, objRefs.Count);
        Assert.AreEqual(meshCutter.Object, objRefs[0]);
    }
}
