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
            ModularAvatarMeshDeform deform
        )
        {
            if (ObjectRegistry.GetReference(mesh).Object == mesh)
            {
                // Clone mesh
                mesh = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, new (TargetProp, IVertexFilter)[] { });
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

                var v = vertices[i];
                var v4 = new float4(v.x, v.y, v.z, 1);

                var worldPos = math.mul(vertToWorld, v4);
                var worldPos3 = new Vector3(worldPos.x, worldPos.y, worldPos.z);
                var deformLocal = deform.transform.InverseTransformPoint(worldPos3);
                var deformRawDot = Vector3.Dot(deform.Direction, deformLocal);
                var deformDot = Mathf.Abs(Vector3.Dot(deform.Direction, deformLocal));

                if (deformDot >= 1)
                {
                    continue;
                }

                float falloff = 0;
                if (deform.Falloff?.keys?.Length > 0)
                {
                    falloff = deform.Falloff.Evaluate(1 - deformDot);
                }

                var normDirection = Vector3.Normalize(deform.Direction);
                var deformNormal =
                    Vector3.Normalize(deformLocal - normDirection * Vector3.Dot(normDirection, deformLocal));
                deformLocal += deformNormal * falloff * deform.Strength;

                // Transform back to world space
                var postTransformWorld = deform.transform.TransformPoint(deformLocal);
                // Transform back to local space
                var postTransformLocal = math.mul(worldToVert,
                    new float4(postTransformWorld.x, postTransformWorld.y, postTransformWorld.z, 1));
                deltaPositions[i] = new Vector3(postTransformLocal.x, postTransformLocal.y, postTransformLocal.z) - v;
            }

            mesh.AddBlendShapeFrame(shapeName, 1, deltaPositions, null, null);
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