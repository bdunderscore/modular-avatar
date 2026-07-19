using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MeshSelectorJob : IDisposable
    {
        internal interface IPositionFilter
        {
            public bool IsVertexSelected(float3 vertexPosition);
        }

        internal interface IUVFilter
        {
            public bool IsVertexSelected(float2 uv);
        }

        public readonly Renderer ReferenceRenderer;
        public readonly Mesh OriginalMesh;
        public Mesh.MeshData MeshData;
        public float4x4 MeshPositionToOriginalRenderer;

        private Mesh.MeshDataArray _meshDataArray;
        private Mesh.MeshDataArray? _bakedMeshDataArray;
        private Mesh.MeshData _positionMeshData;
        private Mesh? _bakedMesh;
        private JobHandle _allDependencies;
        private List<IDisposable> _disposables;
        
        private Dictionary<object, (IDisposable, JobHandle)> _tempResources;
        
        private (NativeArray<int>, JobHandle)[] _submeshIndexBuffers = null;

        private (NativeArray<float3>, JobHandle)? _vertexPositions;
        private (NativeArray<float2>, JobHandle)?[] _uv; 

        public MeshSelectorJob(Renderer referenceRenderer, Mesh mesh)
            : this(referenceRenderer, mesh, ResolveOriginalRenderer(referenceRenderer))
        {
        }

        internal MeshSelectorJob(Renderer referenceRenderer, Mesh mesh, GameObject? originalRenderer)
        {
            ReferenceRenderer = referenceRenderer;
            OriginalMesh = mesh;
            _meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            MeshData = _meshDataArray[0];
            _positionMeshData = MeshData;
            _tempResources = new();
            _disposables = new List<IDisposable>();
            _uv = new (NativeArray<float2>, JobHandle)?[8];

            if (originalRenderer != null)
            {
                // Translate the meshSpaceCenter coordinates from the baked preview renderer to the original
                // renderer's coordinate space; in preview, these don't match in general.
                var refRendererToWorld = (float4x4)referenceRenderer.transform.localToWorldMatrix;
                var worldtoOriginalRenderer = (float4x4)originalRenderer.transform.worldToLocalMatrix;
                MeshPositionToOriginalRenderer = math.mul(worldtoOriginalRenderer, refRendererToWorld);
            }
            else
            {
                MeshPositionToOriginalRenderer = float4x4.identity;
            }
            
            _submeshIndexBuffers = new (NativeArray<int>, JobHandle)[MeshData.subMeshCount];

            if (MeshData.indexFormat == IndexFormat.UInt16)
            {
                for (int i = 0; i < MeshData.subMeshCount; i++)
                {
                    var desc = MeshData.GetSubMesh(i);
                    var indexBuffer = MeshData.GetIndexData<ushort>();
                    var decompressedBuffer = new NativeArray<int>(desc.indexCount, Allocator.TempJob);
                    var jobHandle = new DecompressIndexBuffer
                    {
                        IndexBuffer = indexBuffer,
                        DecompressedIndexBuffer = decompressedBuffer,
                        InputIndexStart = desc.indexStart,
                        BaseIndex = desc.baseVertex
                    }.Schedule(desc.indexCount, 64);

                    _allDependencies = JobHandle.CombineDependencies(_allDependencies, jobHandle);
                    _submeshIndexBuffers[i] = (decompressedBuffer, jobHandle);
                }
            }
            else
            {
                for (int i = 0; i < MeshData.subMeshCount; i++)
                {
                    var desc = MeshData.GetSubMesh(i);
                    var indexBuffer = MeshData.GetIndexData<int>();
                    var decompressedBuffer = new NativeArray<int>(desc.indexCount, Allocator.TempJob);
                    var jobHandle = new AdjustIndexBuffer
                    {
                        IndexBuffer = indexBuffer,
                        DecompressedIndexBuffer = decompressedBuffer,
                        InputIndexStart = desc.indexStart,
                        BaseIndex = desc.baseVertex
                    }.Schedule(desc.indexCount, 64);

                    _allDependencies = JobHandle.CombineDependencies(_allDependencies, jobHandle);
                    _submeshIndexBuffers[i] = (decompressedBuffer, jobHandle);
                }
            }
        }

        private static GameObject? ResolveOriginalRenderer(Renderer referenceRenderer)
        {
            return referenceRenderer != null
                ? NDMFPreview.GetOriginalObjectForProxy(referenceRenderer.gameObject)
                : null;
        }

        /// <summary>
        /// Gets a NativeArray of vertex positions. These vertex positions are normalized to float3, and converted
        /// into the original renderer's local space using the MeshPositionToOriginalRenderer matrix.
        /// </summary>
        /// <param name="dependency">A ref JobHandle; the dependency for the job that prepares the vertex position data
        /// will be combined into this dependency.</param>
        /// <returns></returns>
        public NativeArray<float3> GetVertexPositions(ref JobHandle dependency)
        {
            if (_vertexPositions != null)
            {
                dependency = JobHandle.CombineDependencies(dependency, _vertexPositions.Value.Item2);
                return _vertexPositions.Value.Item1;
            }

            var positionMeshData = GetPositionMeshData();
            var vertCount = positionMeshData.vertexCount;

            var vertStream = positionMeshData.GetVertexAttributeStream(VertexAttribute.Position);
            var vertStride = positionMeshData.GetVertexBufferStride(vertStream);
            var vertOffset = positionMeshData.GetVertexAttributeOffset(VertexAttribute.Position);
            var vertFormat = positionMeshData.GetVertexAttributeFormat(VertexAttribute.Position);
            var vertDim = positionMeshData.GetVertexAttributeDimension(VertexAttribute.Position);

            if (vertDim != 3)
            {
                throw new NotSupportedException($"Unsupported vertex position dimension {vertDim} in mesh {ReferenceRenderer.name}");
            }
            
            var vertPositions = ConvertVertexStreamData.Convert(
                out var convertJobHandle,
                positionMeshData.GetVertexData<byte>(vertStream),
                vertStride,
                vertOffset,
                vertDim,
                vertFormat
            ).Reinterpret<float3>(4);

            var transformJob = new TransformVertexJob()
            {
                VertexPositions = vertPositions,
                Transform = MeshPositionToOriginalRenderer
            }.Schedule(vertCount, 64, convertJobHandle);
            
            _vertexPositions = (vertPositions, transformJob);

            _allDependencies = JobHandle.CombineDependencies(_allDependencies, convertJobHandle, transformJob);
            _disposables.Add(vertPositions);

            dependency = JobHandle.CombineDependencies(dependency, transformJob);
            return vertPositions;
        }

        private Mesh.MeshData GetPositionMeshData()
        {
            if (ReferenceRenderer is not SkinnedMeshRenderer smr)
            {
                return MeshData;
            }

            if (_bakedMeshDataArray.HasValue)
            {
                return _positionMeshData;
            }

            var bakedMesh = new Mesh { name = OriginalMesh.name + " (baked for mesh selection)" };
            Mesh.MeshDataArray? bakedDataArray = null;
            var oldMesh = smr.sharedMesh;

            try
            {
                smr.sharedMesh = OriginalMesh;
                smr.BakeMesh(bakedMesh, true);

                bakedDataArray = Mesh.AcquireReadOnlyMeshData(bakedMesh);
                var bakedMeshData = bakedDataArray.Value[0];
                if (bakedMeshData.vertexCount != MeshData.vertexCount)
                {
                    throw new InvalidOperationException(
                        $"Baked mesh vertex count mismatch for {ReferenceRenderer.name}: " +
                        $"{bakedMeshData.vertexCount} baked vertices, {MeshData.vertexCount} source vertices"
                    );
                }

                _bakedMesh = bakedMesh;
                _bakedMeshDataArray = bakedDataArray;
                _positionMeshData = bakedMeshData;
                return _positionMeshData;
            }
            catch
            {
                if (bakedDataArray.HasValue)
                {
                    bakedDataArray.Value.Dispose();
                }

                Object.DestroyImmediate(bakedMesh);
                throw;
            }
            finally
            {
                smr.sharedMesh = oldMesh;
            }
        }
        
        [BurstCompile]
        struct TransformVertexJob : IJobParallelFor
        {
            public NativeArray<float3> VertexPositions;

            public float4x4 Transform;

            public void Execute(int index)
            {
                VertexPositions[index] = math.transform(Transform, VertexPositions[index]);
            }
        }

        /// <summary>
        /// Gets a NativeArray of UV coordinates for the given UV channel. The UVs are normalized into float2.
        /// </summary>
        /// <param name="dependency"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public NativeArray<float2> GetUV(ref JobHandle dependency, int index)
        {
            if (_uv[index] != null)
            {
                dependency = JobHandle.CombineDependencies(dependency, _uv[index].Value.Item2);
                return _uv[index].Value.Item1;
            }

            var attr = (VertexAttribute)((int)VertexAttribute.TexCoord0 + index);
            var uvStream = MeshData.GetVertexAttributeStream(attr);
            var uvStride = MeshData.GetVertexBufferStride(uvStream);
            var uvOffset = MeshData.GetVertexAttributeOffset(attr);
            var uvFormat = MeshData.GetVertexAttributeFormat(attr);
            var uvDim = MeshData.GetVertexAttributeDimension(attr);

            if (uvDim != 2)
            {
                throw new NotSupportedException($"Unsupported UV dimension {uvDim} in mesh {ReferenceRenderer.name}");
            }

            var uvData = ConvertVertexStreamData.Convert(
                out var convertJobHandle,
                MeshData.GetVertexData<byte>(uvStream),
                uvStride,
                uvOffset,
                uvDim,
                uvFormat
            ).Reinterpret<float2>(4);

            _allDependencies = JobHandle.CombineDependencies(_allDependencies, convertJobHandle);
            _uv[index] = (uvData, convertJobHandle);
            _disposables.Add(uvData);

            dependency = JobHandle.CombineDependencies(dependency, convertJobHandle);
            return uvData;
        }
        
        public (T, JobHandle) GetTempResource<T>(object contextKey, Func<(T, JobHandle)> factory) where T : IDisposable
        {
            if (_tempResources.TryGetValue(contextKey, out var pair))
            {
                return ((T)pair.Item1, pair.Item2);
            }

            var (resource, jobHandle) = factory();
            _allDependencies = JobHandle.CombineDependencies(_allDependencies, jobHandle);
            _tempResources[contextKey] = (resource, jobHandle);
            return (resource, jobHandle);
        }

        public (NativeArray<int> indices, JobHandle dependency) GetSubmeshIndexBuffer(int submesh)
        {
            var (arr, handle) = _submeshIndexBuffers[submesh];
            return (arr, handle);
        }

        public void Dispose()
        {
            _allDependencies.Complete();

            foreach (var buf in _submeshIndexBuffers)
            {
                buf.Item1.Dispose();
            }
            
            foreach (var (resource, handle) in _tempResources.Values)
            {
                handle.Complete(); // should be done by now, but just in case
                resource.Dispose();
            }

            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            if (_bakedMeshDataArray.HasValue)
            {
                _bakedMeshDataArray.Value.Dispose();
            }

            _meshDataArray.Dispose();

            if (_bakedMesh != null)
            {
                Object.DestroyImmediate(_bakedMesh);
            }
        }

        public JobHandle MarkPrimitivesFromPositionFilter<TCond>(
            object filterKey,
            IPositionFilter posFilter,
            VertexSelectionMode mode,
            int submesh,
            NativeSlice<bool> primitiveMask
        ) where TCond: struct, IPositionFilter
        {
            var (submeshIndexBuffer, indexJobHandle) = _submeshIndexBuffers[submesh];
            int vertsPerPrim = VertsPerPrim(MeshData.GetSubMesh(submesh).topology);

            JobHandle vertPosDep = default;
            var vertexPositions = GetVertexPositions(ref vertPosDep);
            var deps = JobHandle.CombineDependencies(indexJobHandle, vertPosDep);

            switch (mode)
            {
                case VertexSelectionMode.Centroid:
                {
                    var job = new MarkPrimsFromVertCentroid<TCond>()
                    {
                        IndexBuffer = submeshIndexBuffer,
                        PrimitiveMask = primitiveMask,
                        VertexPositions = vertexPositions,
                        VertexCondition = (TCond)posFilter,
                        VertsPerPrim = vertsPerPrim
                    };
                    return TrackHandle(job.Schedule(primitiveMask.Length, 64, deps));
                }
                case VertexSelectionMode.AnyVertex:
                case VertexSelectionMode.AllVertices: // fall through
                {
                    int threshold = mode == VertexSelectionMode.AnyVertex ? 1 : vertsPerPrim;
                    var job = new MarkPrimsFromVertPos<TCond>()
                    {
                        IndexBuffer = submeshIndexBuffer,
                        PrimitiveMask = primitiveMask,
                        VertexPositions = vertexPositions,
                        VertexCondition = (TCond)posFilter,
                        VertsPerPrim = vertsPerPrim,
                        Threshold = threshold
                    };

                    return TrackHandle(job.Schedule(primitiveMask.Length, 64, deps));
                }
                default:
                    throw new ArgumentException("Unsupported vertex selection mode " + mode);
            }
        }

        public JobHandle MarkPrimitivesFromVertexIndices(
            object filterKey,
            Func<(NativeArray<bool>, JobHandle)> vertexMaskFactory,
            VertexSelectionMode mode,
            int submesh,
            NativeSlice<bool> primitiveMask
        )
        {
            if (mode == VertexSelectionMode.Centroid)
            {
                throw new ArgumentException("Centroid mode not supported for vertex index-based selection");
            }
            
            var (arr, maskJobHandle) = this.GetTempResource(filterKey, vertexMaskFactory);
            var (submeshIndexBuffer, indexJobHandle) = _submeshIndexBuffers[submesh];

            var deps = JobHandle.CombineDependencies(maskJobHandle, indexJobHandle);

            int vertsPerPrim = VertsPerPrim(MeshData.GetSubMesh(submesh).topology);
            int threshold = mode == VertexSelectionMode.AnyVertex ? 1 : vertsPerPrim;
            
            var job = new MarkPrimsFromVertMask
            {
                IndexBuffer = submeshIndexBuffer,
                VertexMask = arr,
                PrimitiveMask = primitiveMask,
                VertsPerPrim = vertsPerPrim,
                Threshold = threshold
            };

            return TrackHandle(job.Schedule(primitiveMask.Length, 64, deps));
        }

        public JobHandle MarkPrimitivesFromUVFilter<TCond>(
            object filterKey,
            TCond uvFilter,
            VertexSelectionMode mode,
            int submesh,
            NativeSlice<bool> primitiveMask,
            int uvChannel = 0
        ) where TCond : struct, IUVFilter
        {
            uvChannel = math.clamp(uvChannel, 0, 7);
            if (!MeshData.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + uvChannel)))
            {
                return default;
            }

            var (submeshIndexBuffer, indexJobHandle) = _submeshIndexBuffers[submesh];
            var vertsPerPrim = VertsPerPrim(MeshData.GetSubMesh(submesh).topology);

            JobHandle uvDep = default;
            var uv = GetUV(ref uvDep, uvChannel);
            var deps = JobHandle.CombineDependencies(indexJobHandle, uvDep);

            switch (mode)
            {
                case VertexSelectionMode.Centroid:
                {
                    var job = new MarkPrimsFromUVCentroid<TCond>
                    {
                        IndexBuffer = submeshIndexBuffer,
                        PrimitiveMask = primitiveMask,
                        UV = uv,
                        VertexCondition = uvFilter,
                        VertsPerPrim = vertsPerPrim
                    };
                    return TrackHandle(job.Schedule(primitiveMask.Length, 64, deps));
                }
                case VertexSelectionMode.AnyVertex:
                case VertexSelectionMode.AllVertices:
                {
                    var threshold = mode == VertexSelectionMode.AnyVertex ? 1 : vertsPerPrim;
                    var job = new MarkPrimsFromUV<TCond>
                    {
                        IndexBuffer = submeshIndexBuffer,
                        PrimitiveMask = primitiveMask,
                        UV = uv,
                        VertexCondition = uvFilter,
                        VertsPerPrim = vertsPerPrim,
                        Threshold = threshold
                    };

                    return TrackHandle(job.Schedule(primitiveMask.Length, 64, deps));
                }
                default:
                    throw new ArgumentException("Unsupported vertex selection mode " + mode);
            }
        }

        /// <summary>
        ///     Records a handed-out primitive-marking job handle as a dependency of this MeshSelectorJob, so that
        ///     <see cref="Dispose" /> completes it before deallocating the index buffers it reads from. Without this,
        ///     callers that only complete a <em>downstream</em> handle (e.g. an AND/OR combine job) can leave the
        ///     underlying mark jobs registered as outstanding readers of the index buffers, tripping the job safety
        ///     system when those buffers are disposed.
        /// </summary>
        private JobHandle TrackHandle(JobHandle handle)
        {
            _allDependencies = JobHandle.CombineDependencies(_allDependencies, handle);
            return handle;
        }

        private int VertsPerPrim(MeshTopology topology)
        {
            switch (topology)
            {
                case MeshTopology.Triangles: return 3;
                case MeshTopology.Quads: return 4;
                default: return 1;
            }
        }

        [BurstCompile]
        private struct DecompressIndexBuffer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ushort> IndexBuffer;
            [WriteOnly] public NativeArray<int> DecompressedIndexBuffer;

            public int InputIndexStart;
            public int BaseIndex;

            public void Execute(int index)
            {
                DecompressedIndexBuffer[index] = (int)(IndexBuffer[InputIndexStart + index] + (uint)BaseIndex);
            }
        }
        
        
        [BurstCompile]
        private struct AdjustIndexBuffer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> IndexBuffer;
            [WriteOnly] public NativeArray<int> DecompressedIndexBuffer;

            public int InputIndexStart;
            public int BaseIndex;

            public void Execute(int index)
            {
                DecompressedIndexBuffer[index] = IndexBuffer[InputIndexStart + index] + BaseIndex;
            }
        }
        
        
        [BurstCompile]
        private struct MarkPrimsFromVertMask : IJobParallelFor
        {
            [ReadOnly] public NativeSlice<int> IndexBuffer;
            [ReadOnly] public NativeArray<bool> VertexMask;
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeSlice<bool> PrimitiveMask;

            public int VertsPerPrim;
            public int Threshold;

            public void Execute(int index)
            {
                int matches = 0;

                for (int i = 0; i < VertsPerPrim; i++)
                {
                    int vertexIndex = IndexBuffer[index * VertsPerPrim + i];
                    if (VertexMask[vertexIndex]) matches++;
                }

                if (matches >= Threshold) PrimitiveMask[index] = true;
            }
        }
        
        [BurstCompile]
        private struct MarkPrimsFromVertPos<TCond> : IJobParallelFor
            where TCond: struct, IPositionFilter
        {
            [ReadOnly] public NativeSlice<int> IndexBuffer;
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeSlice<bool> PrimitiveMask;

            [ReadOnly] public NativeArray<float3> VertexPositions;

            public TCond VertexCondition;

            public int VertsPerPrim;
            public int Threshold;

            public void Execute(int index)
            {
                int matches = 0;

                for (int i = 0; i < VertsPerPrim; i++)
                {
                    int vertexIndex = IndexBuffer[index * VertsPerPrim + i];
                    if (VertexCondition.IsVertexSelected(VertexPositions[vertexIndex])) matches++;
                }

                if (matches >= Threshold) PrimitiveMask[index] = true;
            }
        }
        
        
        [BurstCompile]
        private struct MarkPrimsFromVertCentroid<TCond> : IJobParallelFor
            where TCond: struct, IPositionFilter
        {
            [ReadOnly] public NativeSlice<int> IndexBuffer;
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeSlice<bool> PrimitiveMask;

            [ReadOnly] public NativeArray<float3> VertexPositions;

            public TCond VertexCondition;

            public int VertsPerPrim;

            public void Execute(int index)
            {
                float3 pos = float3.zero;

                for (int i = 0; i < VertsPerPrim; i++)
                {
                    int vertexIndex = IndexBuffer[index * VertsPerPrim + i];
                    pos += VertexPositions[vertexIndex];
                }

                if (VertexCondition.IsVertexSelected(pos / VertsPerPrim))
                    PrimitiveMask[index] = true;
            }
        }

        [BurstCompile]
        private struct MarkPrimsFromUV<TCond> : IJobParallelFor
            where TCond : struct, IUVFilter
        {
            [ReadOnly] public NativeSlice<int> IndexBuffer;

            [NativeDisableParallelForRestriction] [WriteOnly]
            public NativeSlice<bool> PrimitiveMask;

            [ReadOnly] public NativeArray<float2> UV;

            public TCond VertexCondition;

            public int VertsPerPrim;
            public int Threshold;

            public void Execute(int index)
            {
                var matches = 0;

                for (var i = 0; i < VertsPerPrim; i++)
                {
                    var vertexIndex = IndexBuffer[index * VertsPerPrim + i];
                    if (VertexCondition.IsVertexSelected(UV[vertexIndex])) matches++;
                }

                if (matches >= Threshold) PrimitiveMask[index] = true;
            }
        }

        [BurstCompile]
        private struct MarkPrimsFromUVCentroid<TCond> : IJobParallelFor
            where TCond : struct, IUVFilter
        {
            [ReadOnly] public NativeSlice<int> IndexBuffer;

            [NativeDisableParallelForRestriction] [WriteOnly]
            public NativeSlice<bool> PrimitiveMask;

            [ReadOnly] public NativeArray<float2> UV;

            public TCond VertexCondition;

            public int VertsPerPrim;

            public void Execute(int index)
            {
                var uv = float2.zero;

                for (var i = 0; i < VertsPerPrim; i++)
                {
                    var vertexIndex = IndexBuffer[index * VertsPerPrim + i];
                    uv += UV[vertexIndex];
                }

                if (VertexCondition.IsVertexSelected(uv / VertsPerPrim))
                    PrimitiveMask[index] = true;
            }
        }

        
    }

}
