using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ConvertVertexStreamData
    {
        /// <summary>
        /// Extracts a vertex attribute from the given vertex attribute stream. The elements of the vertex attribute
        /// will be placed into a returned float array after being converted to 32-bit floats.
        /// </summary>
        public static NativeArray<float> Convert(
            out JobHandle dependency,
            NativeArray<byte> rawVertexAttributeData,
            int stride,
            int offset,
            int dimension,
            VertexAttributeFormat format
        )
        {
            var elementCount = rawVertexAttributeData.Length / stride * dimension;
            var convertedData = new NativeArray<float>(elementCount, Allocator.TempJob);

            switch (format)
            {
                case VertexAttributeFormat.Float32:
                    dependency = Schedule(new ReadFloat32(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.Float16:
                    dependency = Schedule(new ReadFloat16(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.UNorm8:
                    dependency = Schedule(new ReadUNorm8(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.SNorm8:
                    dependency = Schedule(new ReadSNorm8(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.UNorm16:
                    dependency = Schedule(new ReadUNorm16(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.SNorm16:
                    dependency = Schedule(new ReadSNorm16(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.UInt8:
                    dependency = Schedule(new ReadUInt8(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.SInt8:
                    dependency = Schedule(new ReadSInt8(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.UInt16:
                    dependency = Schedule(new ReadUInt16(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.SInt16:
                    dependency = Schedule(new ReadSInt16(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.UInt32:
                    dependency = Schedule(new ReadUInt32(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                case VertexAttributeFormat.SInt32:
                    dependency = Schedule(new ReadSInt32(), rawVertexAttributeData, convertedData, stride, offset,
                        dimension, elementCount);
                    break;
                default:
                    convertedData.Dispose();
                    throw new NotSupportedException($"Unsupported vertex attribute format: {format}");
            }

            return convertedData;
        }

        private static JobHandle Schedule<TReader>(
            TReader reader,
            NativeArray<byte> rawData,
            NativeArray<float> convertedData,
            int stride, int offset, int dimension, int vertexCount
        ) where TReader : struct, IComponentReader
        {
            return new ConvertJob<TReader>
            {
                RawData = rawData,
                ConvertedData = convertedData,
                Stride = stride,
                Offset = offset,
                Dimension = dimension,
                Reader = reader
            }.Schedule(vertexCount, 64);
        }

        interface IComponentReader
        {
            int ByteSize { get; }
            float Read(NativeArray<byte> data, int byteOffset);
        }

        [BurstCompile]
        struct ConvertJob<TReader> : IJobParallelFor where TReader : struct, IComponentReader
        {
            [ReadOnly] public NativeArray<byte> RawData;
            [WriteOnly] public NativeArray<float> ConvertedData;
            public int Stride, Offset, Dimension;
            public TReader Reader;

            public void Execute(int index)
            {
                var element = index / Dimension;
                var subIndex = index % Dimension;

                var byteInde = element * Stride + Offset + subIndex * Reader.ByteSize;
                ConvertedData[index] = Reader.Read(RawData, byteInde);
            }
        }

        struct ReadFloat32 : IComponentReader
        {
            public int ByteSize => 4;
            public float Read(NativeArray<byte> data, int offset) => data.ReinterpretLoad<float>(offset);
        }

        struct ReadFloat16 : IComponentReader
        {
            public int ByteSize => 2;
            public float Read(NativeArray<byte> data, int offset) => (float)data.ReinterpretLoad<half>(offset);
        }

        struct ReadUNorm8 : IComponentReader
        {
            public int ByteSize => 1;
            public float Read(NativeArray<byte> data, int offset) => data[offset] / 255f;
        }

        struct ReadSNorm8 : IComponentReader
        {
            public int ByteSize => 1;
            public float Read(NativeArray<byte> data, int offset) => math.max((sbyte)data[offset] / 127f, -1f);
        }

        struct ReadUNorm16 : IComponentReader
        {
            public int ByteSize => 2;
            public float Read(NativeArray<byte> data, int offset) => data.ReinterpretLoad<ushort>(offset) / 65535f;
        }

        struct ReadSNorm16 : IComponentReader
        {
            public int ByteSize => 2;
            public float Read(NativeArray<byte> data, int offset) => math.max(data.ReinterpretLoad<short>(offset) / 32767f, -1f);
        }

        struct ReadUInt8 : IComponentReader
        {
            public int ByteSize => 1;
            public float Read(NativeArray<byte> data, int offset) => data[offset];
        }

        struct ReadSInt8 : IComponentReader
        {
            public int ByteSize => 1;
            public float Read(NativeArray<byte> data, int offset) => (sbyte)data[offset];
        }

        struct ReadUInt16 : IComponentReader
        {
            public int ByteSize => 2;
            public float Read(NativeArray<byte> data, int offset) => data.ReinterpretLoad<ushort>(offset);
        }

        struct ReadSInt16 : IComponentReader
        {
            public int ByteSize => 2;
            public float Read(NativeArray<byte> data, int offset) => data.ReinterpretLoad<short>(offset);
        }

        struct ReadUInt32 : IComponentReader
        {
            public int ByteSize => 4;
            public float Read(NativeArray<byte> data, int offset) => data.ReinterpretLoad<uint>(offset);
        }

        struct ReadSInt32 : IComponentReader
        {
            public int ByteSize => 4;
            public float Read(NativeArray<byte> data, int offset) => data.ReinterpretLoad<int>(offset);
        }
    }
}
