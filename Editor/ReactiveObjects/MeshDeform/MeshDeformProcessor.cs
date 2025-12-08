#nullable enable

using nadena.dev.ndmf;
using Unity.Mathematics;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    internal class MeshDeformProcessor
    {
        public static void AddMeshDeform(
            ref Mesh mesh,
            Renderer renderer,
            string shapeName,
            AbstractMeshDeformComponent deformComponent,
            IMeshDeformation? deformation
        )
        {
            var componentTransform = deformComponent.transform;
            
            if (ObjectRegistry.GetReference(mesh).Object == mesh)
            {
                // Clone mesh
                mesh = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, new (TargetProp, IVertexFilter)[] { });
            }

            if (deformation == null)
            {
                // Add a null blendshape
                var zero = new Vector3[mesh.vertexCount];
                mesh.AddBlendShapeFrame(
                    shapeName,
                    100,
                    zero,
                    zero,
                    zero
                );
                return;
            }


            Transform[]? bones = (renderer as SkinnedMeshRenderer)?.bones;
            float4x4[]? boneToWorld = null;
            float4x4[]? worldToBone = null;

            if (bones != null)
            {
                var bindposes = ((SkinnedMeshRenderer)renderer).sharedMesh.bindposes;
                boneToWorld = new float4x4[bones.Length];

                for (var i = 0; i < bones.Length; i++)
                {
                    var bone = bones[i];
                    if (bone != null)
                    {
                        boneToWorld[i] = bindposes[i] * bone.localToWorldMatrix;
                    }
                    else
                    {
                        boneToWorld[i] = float4x4.identity;
                    }
                }
            }

            var boneCounts = mesh.GetBonesPerVertex();
            var boneWeights = mesh.GetAllBoneWeights();

            float4x4 rootToWorld = renderer.transform.localToWorldMatrix;
            float4x4 worldToRoot = renderer.transform.worldToLocalMatrix;

            var boneIndex = 0;
            var vertexCount = mesh.vertexCount;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var deltaPositions = new Vector3[vertexCount];

            for (var i = 0; i < vertexCount; i++)
            {
                // Compute the weighted transformation matrices for this vertex.
                var vertToWorld = rootToWorld;
                if (boneToWorld != null)
                {
                    vertToWorld = Matrix4x4.zero;

                    float totalWeight = 0;

                    for (var bone = 0; bone < boneCounts[i]; bone++)
                    {
                        var boneWeight = boneWeights[boneIndex + bone];
                        if (boneWeight.weight == 0)
                            continue; // we're avoiding actual floating point zero here specifically
                        if (boneWeight.boneIndex < 0) continue;

                        totalWeight += boneWeight.weight;
                        vertToWorld += boneToWorld[boneWeight.boneIndex] * boneWeight.weight;
                    }

                    if (totalWeight == 0)
                    {
                        vertToWorld = rootToWorld;
                    }
                    else
                    {
                        vertToWorld /= totalWeight;
                    }

                    boneIndex += boneCounts[i];
                }

                var worldToVert = math.inverse(vertToWorld);

                var vertToWorldDirection = vertToWorld;
                vertToWorldDirection.c3 = new float4(0, 0, 0, 1);
                var worldtoVertDirection = worldToVert;
                worldtoVertDirection.c3 = new float4(0, 0, 0, 1);

                var v = vertices[i];
                var v4 = new float4(v.x, v.y, v.z, 1);

                var norm = normals[i];
                var norm4 = new float4(norm.x, norm.y, norm.z, 1);
                var worldNorm4 = math.mul(vertToWorldDirection, norm4);
                var worldNorm = new Vector3(worldNorm4.x, worldNorm4.y, worldNorm4.z);

                var worldPos = math.mul(vertToWorld, v4);
                var worldPos3 = new Vector3(worldPos.x, worldPos.y, worldPos.z);

                var deformLocal = componentTransform.InverseTransformPoint(worldPos3);
                // todo norm,tangent
                var deformNormal = componentTransform.InverseTransformDirection(worldNorm);
                var deformTangent = Vector3.zero;

                deformation.ProcessPoint(ref deformLocal, ref deformNormal, ref deformTangent);
                
                // Transform back to world space
                var postTransformWorld = componentTransform.TransformPoint(deformLocal);
                // Transform back to local space
                var postTransformLocal = math.mul(worldToVert,
                    new float4(postTransformWorld.x, postTransformWorld.y, postTransformWorld.z, 1));
                deltaPositions[i] = new Vector3(postTransformLocal.x, postTransformLocal.y, postTransformLocal.z) - v;
            }

            mesh.AddBlendShapeFrame(shapeName, 100, deltaPositions, null, null);
        }

        private static Matrix4x4 ScalarMulMatrix(float scalar)
        {
            var mat = Matrix4x4.zero;

            mat.m00 = scalar;
            mat.m11 = scalar;
            mat.m22 = scalar;
            mat.m33 = scalar;

            return mat;
        }
    }
}