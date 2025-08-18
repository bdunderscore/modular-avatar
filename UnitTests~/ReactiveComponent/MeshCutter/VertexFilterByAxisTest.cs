using System;
using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Object = UnityEngine.Object;

public class VertexFilterByAxisTest : TestBase
{
    /*
     * Test artifacts:
     *
     * `three planes test mesh.fbx` - a mesh with three planes, aligned on the XZ axis, at positions Y=0, Y=1, and Y=-1.
     * Three blendshapes are provided, which manipulate each mesh: Positive moves the Y=1 plane, Negative moves the Y=-1
     * plane, and Center moves the Y=0 plane.
     */
    
    private GameObject avatarRoot;
    private MeshRenderer meshRenderer;
    private Mesh testMesh;
    
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        
        // Load the test mesh
        testMesh = LoadAsset<Mesh>("three planes test mesh.fbx");
        
        // Create test object with the mesh
        avatarRoot = CreateRoot("AxisFilterTestObject");
        meshRenderer = avatarRoot.AddComponent<MeshRenderer>();
        var meshFilter = avatarRoot.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;
        
        // Ensure we start with a consistent position and scale.
        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
    }
    
    // Tests for vertex selection with Renderer reference frame
    [Test]
    public void TestVertexSelectionRenderer()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up; // Y-axis up
        component.ReferenceFrame = ByAxisReferenceFrame.Renderer;
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // Only vertices above Y=0 should now be filtered
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            bool shouldBeFiltered = vertices[i].y > 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at position {vertices[i]} filtering mismatch");
        }
    }
    
    [Test]
    public void TestVertexSelectionAvatarRoot()
    {
        // Create a separate object for the MeshRenderer with different rotation
        var meshObject = CreateChild(avatarRoot, "MeshObject");
        meshObject.transform.rotation = Quaternion.Euler(45, 0, 0); // Rotate mesh object
        
        // Move the MeshRenderer to the separate object
        Object.DestroyImmediate(meshRenderer);
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        var meshFilter = meshObject.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;
        
        // Rotate the avatar root (testObject) differently to verify avatar root space is used
        avatarRoot.transform.rotation = Quaternion.Euler(0, 90, 0);
        
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero; // Avatar root center
        component.Axis = Vector3.up;
        component.ReferenceFrame = ByAxisReferenceFrame.AvatarRoot;
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // Transform vertices to world space and check against avatar root axis
        // The avatar root (testObject) is rotated, but the axis should be in avatar root space
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // Transform vertex from mesh space to world space via the meshObject
            var worldVertex = meshObject.transform.TransformPoint(vertices[i]);
            
            // Transform world vertex to avatar root space (testObject space)
            var avatarRootSpaceVertex = avatarRoot.transform.InverseTransformPoint(worldVertex);
            
            // Check filtering against avatar root space Y axis
            bool shouldBeFiltered = avatarRootSpaceVertex.y > 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at avatar root space position {avatarRootSpaceVertex} filtering mismatch");
        }
    }
    
    [Test]
    public void TestVertexSelectionRootBone()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.right; // X-axis
        component.ReferenceFrame = ByAxisReferenceFrame.RootBone;
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // For MeshRenderer, root bone reference frame should be same as renderer
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            bool shouldBeFiltered = vertices[i].x > 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at position {vertices[i]} filtering mismatch");
        }
    }
    
    [Test]
    public void TestVertexSelectionDifferentAxis()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.down; // Negative Y axis
        component.ReferenceFrame = ByAxisReferenceFrame.Renderer;
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            bool shouldBeFiltered = vertices[i].y < 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at position {vertices[i]} should be filtered with negative Y axis");
        }
    }
    
    [Test]
    public void TestVertexSelectionOffsetCenter()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = new Vector3(0, -0.5f, 0); // Offset center
        component.Axis = Vector3.up;
        component.ReferenceFrame = ByAxisReferenceFrame.Renderer;
        
        var context = new ComputeContext("test");
        var filter = new VertexFilterByAxis(component, context);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        for (int i = 0; i < filtered.Length; i++) filtered[i] = false;
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // With center at Y=0.5, only vertices above Y=0.5 should be filtered
            bool shouldBeFiltered = vertices[i].y > -0.5f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at position {vertices[i]} filtering mismatch with offset center");
        }
    }
    
    // Tests for pre-set value preservation
    [Test]
    public void TestPreFilteredVerticesRemainFiltered()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up;
        component.ReferenceFrame = ByAxisReferenceFrame.Renderer;
        
        var context = new ComputeContext("test");
        var filter = new VertexFilterByAxis(component, context);
        
        // Pre-mark all vertices as filtered
        var filtered = new bool[testMesh.vertices.Length];
        for (int i = 0; i < filtered.Length; i++) filtered[i] = true;
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // All vertices should remain filtered (method only sets true, never false)
        for (int i = 0; i < filtered.Length; i++)
        {
            Assert.IsTrue(filtered[i], $"Pre-filtered vertex {i} should remain filtered");
        }
    }
    
    [Test]
    public void TestUnfilteredVerticesRemainUnfiltered()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.up * 4;
        component.Axis = Vector3.up;
        component.ReferenceFrame = ByAxisReferenceFrame.Renderer;
        
        var context = new ComputeContext("test");
        var filter = new VertexFilterByAxis(component, context);
        
        // Mark only some vertices as filtered initially (alternating pattern)
        var filtered = new bool[testMesh.vertices.Length];
        for (int i = 0; i < filtered.Length; i++)
        {
            filtered[i] = i % 2 == 0; // Every other vertex starts as filtered
        }
        
        var originalFiltered = new bool[filtered.Length];
        System.Array.Copy(filtered, originalFiltered, filtered.Length);
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // Vertices that were originally unfiltered should remain unchanged
        for (int i = 0; i < filtered.Length; i++)
        {
            if (!originalFiltered[i])
            {
                Assert.IsFalse(filtered[i], $"Originally unfiltered vertex {i} should remain unfiltered");
            }
        }
    }
    
    [Test]
    public void TestSkinnedMeshRendererWithExplicitRootBone()
    {
        // Create a root bone that is rotated
        var rootBone = CreateChild(avatarRoot, "RootBone");
        rootBone.transform.rotation = Quaternion.Euler(0, 0, 90); // Rotate 90 degrees around Z
        
        // Replace MeshRenderer with SkinnedMeshRenderer
        Object.DestroyImmediate(meshRenderer);
        var skinnedMeshRenderer = avatarRoot.AddComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = testMesh;
        skinnedMeshRenderer.rootBone = rootBone.transform;
        
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up; // Y-axis in root bone space
        component.ReferenceFrame = ByAxisReferenceFrame.RootBone;
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(skinnedMeshRenderer, testMesh, filtered);
        
        // The root bone is rotated 90 degrees around Z, so Y-axis in bone space
        // corresponds to X-axis in mesh space (since mesh is in renderer space initially)
        // After transformation: bone's Y-axis points in mesh's -X direction
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // Transform vertex to root bone space to check filtering
            var rendererSpaceVertex = vertices[i];
            var boneSpaceVertex = rootBone.transform.InverseTransformPoint(
                avatarRoot.transform.TransformPoint(rendererSpaceVertex));
            
            bool shouldBeFiltered = boneSpaceVertex.y > 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at bone space position {boneSpaceVertex} filtering mismatch");
        }
    }
    
    [Test]
    public void TestSkinnedMeshRendererWithNullRootBone()
    {
        // Create a separate object for the SkinnedMeshRenderer with different rotation
        var meshObject = CreateChild(avatarRoot, "MeshObject");
        meshObject.transform.rotation = Quaternion.Euler(0, 45, 0); // Rotate mesh object 45 degrees around Y
        
        // Rotate the avatar root differently to verify renderer space is used (not avatar root space)
        avatarRoot.transform.rotation = Quaternion.Euler(30, 0, 0);
        
        // Replace MeshRenderer with SkinnedMeshRenderer on the mesh object
        Object.DestroyImmediate(meshRenderer);
        var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = testMesh;
        skinnedMeshRenderer.rootBone = null; // Explicitly null - should fallback to renderer transform
        
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.forward; // Z-axis in root bone space (which is renderer space)
        component.ReferenceFrame = ByAxisReferenceFrame.RootBone;
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(skinnedMeshRenderer, testMesh, filtered);
        
        // With null rootBone, it should use the renderer transform (meshObject)
        // The renderer evaluation should be relative to the meshObject's transform
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // For vertices close to Z=0, rounding errors can happen, so don't assert on those
            if (Math.Abs(vertices[i].z) < 0.001) continue;
            
            // Filter axis is Z-forward in renderer space
            bool shouldBeFiltered = vertices[i].z > 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at renderer space position {vertices[i]} filtering mismatch");
        }
    }
    
    [Test]
    public void TestSkinnedMeshRendererRootBoneVsRenderer()
    {
        // Create a root bone with different rotation than renderer
        var rootBone = CreateChild(avatarRoot, "RootBone");
        rootBone.transform.rotation = Quaternion.Euler(90, 0, 0); // Rotate 90 degrees around X
        avatarRoot.transform.rotation = Quaternion.Euler(0, 90, 0); // Rotate renderer around Y
        
        // Replace MeshRenderer with SkinnedMeshRenderer
        Object.DestroyImmediate(meshRenderer);
        var skinnedMeshRenderer = avatarRoot.AddComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = testMesh;
        skinnedMeshRenderer.rootBone = rootBone.transform;
        
        // Test RootBone reference frame
        var componentRootBone = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        componentRootBone.Center = Vector3.zero;
        componentRootBone.Axis = Vector3.up;
        componentRootBone.ReferenceFrame = ByAxisReferenceFrame.RootBone;
        
        var filterRootBone = new VertexFilterByAxis(componentRootBone, ComputeContext.NullContext);
        var filteredRootBone = new bool[testMesh.vertices.Length];
        filterRootBone.MarkFilteredVertices(skinnedMeshRenderer, testMesh, filteredRootBone);
        
        // Test Renderer reference frame for comparison
        var componentRenderer = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        componentRenderer.Center = Vector3.zero;
        componentRenderer.Axis = Vector3.up;
        componentRenderer.ReferenceFrame = ByAxisReferenceFrame.Renderer;
        
        var filterRenderer = new VertexFilterByAxis(componentRenderer, ComputeContext.NullContext);
        var filteredRenderer = new bool[testMesh.vertices.Length];
        filterRenderer.MarkFilteredVertices(skinnedMeshRenderer, testMesh, filteredRenderer);
        
        // The results should be different due to different reference frames
        bool foundDifference = false;
        for (int i = 0; i < testMesh.vertices.Length; i++)
        {
            if (filteredRootBone[i] != filteredRenderer[i])
            {
                foundDifference = true;
                break;
            }
        }
        
        Assert.IsTrue(foundDifference, 
            "RootBone and Renderer reference frames should produce different results when transforms differ");
        
        // Verify the root bone filtering is working correctly
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // Transform to root bone space for verification
            var worldVertex = avatarRoot.transform.TransformPoint(vertices[i]);
            var rootBoneSpaceVertex = rootBone.transform.InverseTransformPoint(worldVertex);
            
            bool shouldBeFilteredInRootBone = rootBoneSpaceVertex.y > 0.0f;
            Assert.AreEqual(shouldBeFilteredInRootBone, filteredRootBone[i], 
                $"Vertex {i} root bone filtering mismatch");
        }
    }
}
