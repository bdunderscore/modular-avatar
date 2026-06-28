using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEngine;

namespace modular_avatar_tests
{
    public class BlendshapeSyncEditorUpdateTest : TestBase
    {
        private Mesh CreateMeshWithBlendshapes(params string[] shapeNames)
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            var zeros = new Vector3[3];
            var deltas = new[] { Vector3.up, Vector3.up, Vector3.up };
            foreach (var name in shapeNames)
                mesh.AddBlendShapeFrame(name, 100f, deltas, zeros, zeros);
            return mesh;
        }

        [Test]
        public void NullTargetMesh_ContinuesLoop_RatherThanAbortingEarly()
        {
            // Local mesh has two blendshapes: index 0 (shape_a) and index 1 (shape_b).
            var root = CreateRoot("root");
            var localGo = CreateChild(root, "local");
            var localMesh = TrackObject(CreateMeshWithBlendshapes("shape_a", "shape_b"));
            var localSmr = localGo.AddComponent<SkinnedMeshRenderer>();
            localSmr.sharedMesh = localMesh;

            // Source mesh: one blendshape to be synced into local shape_b.
            var sourceGo = CreateChild(root, "source");
            var sourceMesh = TrackObject(CreateMeshWithBlendshapes("shape_b"));
            var sourceSmr = sourceGo.AddComponent<SkinnedMeshRenderer>();
            sourceSmr.sharedMesh = sourceMesh;
            sourceSmr.SetBlendShapeWeight(0, 77f);

            var sync = localGo.AddComponent<ModularAvatarBlendshapeSync>();
            // A single dummy Bindings entry so BindingIndex=0 doesn't throw.
            sync.Bindings.Add(new BlendshapeBinding());

            // First binding: TargetMesh null — simulates a mesh that was destroyed.
            // Second binding: valid source → syncs sourceSmr[0] into local[1].
            sync._editorBindings = new List<ModularAvatarBlendshapeSync.EditorBlendshapeBinding>
            {
                new ModularAvatarBlendshapeSync.EditorBlendshapeBinding
                {
                    TargetMesh = null,
                    LocalBlendshapeIndex = 0,
                    BindingIndex = 0,
                },
                new ModularAvatarBlendshapeSync.EditorBlendshapeBinding
                {
                    TargetMesh = sourceSmr,
                    RemoteBlendshapeIndex = 0,
                    LocalBlendshapeIndex = 1,
                    BindingIndex = 0,
                },
            };

            sync.EditorUpdate();

            // With the bug (`return` instead of `continue`), the loop aborts on the null
            // binding and shape_b (index 1) stays at 0.  With the fix it should be 77.
            Assert.AreEqual(77f, localSmr.GetBlendShapeWeight(1), 0.001f,
                "EditorUpdate must skip null-TargetMesh bindings and still sync subsequent valid ones");
        }
    }
}
