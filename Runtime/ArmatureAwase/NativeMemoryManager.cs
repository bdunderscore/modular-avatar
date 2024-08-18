#region

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class NativeArrayRef<T> : INativeArrayRef where T : unmanaged
    {
        internal NativeArray<T> Array;

        public static implicit operator NativeArray<T>(NativeArrayRef<T> arrayRef) => arrayRef.Array;

        public void Dispose()
        {
            Array.Dispose();
        }

        public void Resize(int n)
        {
            if (Array.Length == n) return;

            var newArray = new NativeArray<T>(n, Allocator.Persistent);
            
            unsafe
            {
                UnsafeUtility.MemCpy(newArray.GetUnsafePtr(), Array.GetUnsafePtr(),
                    Math.Min(n, Array.Length) * UnsafeUtility.SizeOf<T>());
            }

            /*
            for (int i = 0; i < Math.Min(n, Array.Length); i++)
            {
                newArray[i] = Array[i];
            }*/

            Array.Dispose();

            Array = newArray;
        }

        public void MemMove(int srcOffset, int dstOffset, int count)
        {
            if (srcOffset < 0 || dstOffset < 0
                              || count < 0
                              || srcOffset + count > Array.Length
                              || dstOffset + count > Array.Length
               )
            {
                throw new ArgumentOutOfRangeException();
            }

            
            unsafe
            {
                UnsafeUtility.MemMove(((T*)Array.GetUnsafePtr()) + dstOffset, ((T*)Array.GetUnsafePtr()) + srcOffset,
                    count * UnsafeUtility.SizeOf<T>());
            }

            /*
            // We assume dstOffset < srcOffset
            for (int i = 0; i < count; i++)
            {
                Array[dstOffset + i] = Array[srcOffset + i];
            }*/
        }
    }

    internal interface INativeArrayRef : IDisposable
    {
        void Resize(int n);
        void MemMove(int srcOffset, int dstOffset, int count);
    }

    internal class NativeMemoryManager : IDisposable
    {
        private List<INativeArrayRef> arrays = new List<INativeArrayRef>();
        public NativeArrayRef<bool> InUseMask { get; private set; }

        public event AllocationMap.DefragmentCallback OnSegmentMove;

        private int _allocatedLength = 1;
        public int AllocatedLength => _allocatedLength;
        private AllocationMap _allocationMap = new AllocationMap();
        private bool _isDisposed;

        public NativeMemoryManager()
        {
            // Bootstrap
            InUseMask = new NativeArrayRef<bool>()
            {
                Array = new NativeArray<bool>(1, Allocator.Persistent)
            };
            arrays.Add(InUseMask);

            _allocationMap.OnSegmentDispose += seg =>
            {
                if (!_isDisposed) SetInUseMask(seg.Offset, seg.Length, false);
            };
        }

        public NativeArrayRef<T> CreateArray<T>() where T : unmanaged
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(NativeMemoryManager));
            }
            
            var arrayRef = new NativeArrayRef<T>()
            {
                Array = new NativeArray<T>(_allocatedLength, Allocator.Persistent)
            };

            arrays.Add(arrayRef);

            return arrayRef;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            
            foreach (var array in arrays)
            {
                array.Dispose();
            }
        }

        void SetInUseMask(int offset, int length, bool value)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            
            // We perform trial creations of segments (and then immediately free them if they exceed the bounds of the
            // array). As such, we clamp the length, rather than throwing an exception.
            length = Math.Min(length, InUseMask.Array.Length - offset);
            
            unsafe
            {
                UnsafeUtility.MemSet((byte*)InUseMask.Array.GetUnsafePtr() + offset, value ? (byte)1 : (byte)0, length);
            }

            /*
            for (int i = 0; i < length; i++)
            {
                try
                {
                    InUseMask.Array[offset + i] = value;
                }
                catch (Exception e)
                {
                    throw;
                }
            }*/
        }

        public ISegment Allocate(int requested)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(NativeMemoryManager));
            }
            
            var segment = _allocationMap.Allocate(requested);

            if (segment.Offset + segment.Length > _allocatedLength)
            {
                // Try defragmenting first.

                // First, deallocate that segment we just created, since it'll be beyond the end of the array and break
                // the memmove operations we'll be doing.
                _allocationMap.FreeSegment(segment);
                
                Defragment();

                segment = _allocationMap.Allocate(requested);
            }

            if (segment.Offset + segment.Length > _allocatedLength)
            {
                // We're still using more space than we have allocated, so allocate some more memory now
                ResizeNativeArrays(segment.Offset + segment.Length);
            }

            SetInUseMask(segment.Offset, segment.Length, true);

            return segment;
        }

        private void Defragment()
        {
            SetInUseMask(0, _allocatedLength, false);
            
            _allocationMap.Defragment((src, dst, length) =>
            {
                foreach (var array in arrays)
                {
                    array.MemMove(src, dst, length);
                }

                SetInUseMask(dst, length, true);

                OnSegmentMove?.Invoke(src, dst, length);
            });
        }
        
        private void ResizeNativeArrays(int minimumLength)
        {
            int targetLength = Math.Max((int)(1.5 * _allocatedLength), minimumLength);

            foreach (var array in arrays)
            {
                array.Resize(targetLength);
            }

            SetInUseMask(_allocatedLength, targetLength - _allocatedLength, false);
            _allocatedLength = targetLength;
        }

        public void Free(ISegment segment)
        {
            if (_isDisposed) return;
            
            _allocationMap.FreeSegment(segment);
            SetInUseMask(segment.Offset, segment.Length, false);
        }
    }
}