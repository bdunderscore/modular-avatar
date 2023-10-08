//-----------------------------------------------------------------------
// <copyright file="NativeIntPtr.cs" company="Jackson Dunstan">
// Copyright 2018 Jackson Dunstan
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// 	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace nadena.dev.modular_avatar.JacksonDunstan.NativeCollections
{
    /// <summary>
    /// A pointer to an int stored in native (i.e. unmanaged) memory
    /// </summary>
    [NativeContainer]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [DebuggerTypeProxy(typeof(NativeIntPtrDebugView))]
    [DebuggerDisplay("Value = {Value}")]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeIntPtr : IDisposable
    {
        /// <summary>
        /// An atomic write-only version of the object suitable for use in a
        /// ParallelFor job
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct Parallel
        {
            /// <summary>
            /// Pointer to the value in native memory
            /// </summary>
            [NativeDisableUnsafePtrRestriction] internal readonly int* m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            /// <summary>
            /// A handle to information about what operations can be safely
            /// performed on the object at any given time.
            /// </summary>
            internal AtomicSafetyHandle m_Safety;

            /// <summary>
            /// Create a parallel version of the object
            /// </summary>
            /// 
            /// <param name="value">
            /// Pointer to the value
            /// </param>
            /// 
            /// <param name="safety">
            /// Atomic safety handle for the object
            /// </param>
            internal Parallel(int* value, AtomicSafetyHandle safety)
            {
                m_Buffer = value;
                m_Safety = safety;
            }
#else
			/// <summary>
			/// Create a parallel version of the object
			/// </summary>
			/// 
			/// <param name="value">
			/// Pointer to the value
			/// </param>
			internal Parallel(int* value)
			{
				m_Buffer = value;
			}
#endif
            /// <summary>
            /// Sets this flag value to one.
            /// </summary>
            /// (added in Modular Avatar)
            [WriteAccessRequired]
            public void SetOne()
            {
                RequireWriteAccess();
                *m_Buffer = 1;
            }

            /// <summary>
            /// Increment the stored value
            /// </summary>
            /// 
            /// <returns>
            /// This object
            /// </returns>
            [WriteAccessRequired]
            public void Increment()
            {
                RequireWriteAccess();
                Interlocked.Increment(ref *m_Buffer);
            }

            /// <summary>
            /// Decrement the stored value
            /// </summary>
            /// 
            /// <returns>
            /// This object
            /// </returns>
            [WriteAccessRequired]
            public void Decrement()
            {
                RequireWriteAccess();
                Interlocked.Decrement(ref *m_Buffer);
            }

            /// <summary>
            /// Add to the stored value
            /// </summary>
            /// 
            /// <param name="value">
            /// Value to add. Use negative values for subtraction.
            /// </param>
            /// 
            /// <returns>
            /// This object
            /// </returns>
            [WriteAccessRequired]
            public void Add(int value)
            {
                RequireWriteAccess();
                Interlocked.Add(ref *m_Buffer, value);
            }

            /// <summary>
            /// Throw an exception if the object isn't writable
            /// </summary>
            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void RequireWriteAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            }
        }

        /// <summary>
        /// Pointer to the value in native memory. Must be named exactly this
        /// way to allow for [NativeContainerSupportsDeallocateOnJobCompletion]
        /// </summary>
        [NativeDisableUnsafePtrRestriction] internal int* m_Buffer;

        /// <summary>
        /// Allocator used to create the backing memory
        /// 
        /// This field must be named this way to comply with
        /// [NativeContainerSupportsDeallocateOnJobCompletion]
        /// </summary>
        internal readonly Allocator m_AllocatorLabel;

        // These fields are all required when safety checks are enabled
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// A handle to information about what operations can be safely
        /// performed on the object at any given time.
        /// </summary>
        private AtomicSafetyHandle m_Safety;

        /// <summary>
        /// A handle that can be used to tell if the object has been disposed
        /// yet or not, which allows for error-checking double disposal.
        /// </summary>
        [NativeSetClassTypeToNullOnSchedule] private DisposeSentinel m_DisposeSentinel;
#endif

        /// <summary>
        /// Allocate memory and set the initial value
        /// </summary>
        /// 
        /// <param name="allocator">
        /// Allocator to allocate and deallocate with. Must be valid.
        /// </param>
        /// 
        /// <param name="initialValue">
        /// Initial value of the allocated memory
        /// </param>
        public NativeIntPtr(Allocator allocator, int initialValue = 0)
        {
            // Require a valid allocator
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException(
                    "Allocator must be Temp, TempJob or Persistent",
                    "allocator");
            }

            // Allocate the memory for the value
            m_Buffer = (int*) UnsafeUtility.Malloc(
                sizeof(int),
                UnsafeUtility.AlignOf<int>(),
                allocator);

            // Store the allocator to use when deallocating
            m_AllocatorLabel = allocator;

            // Create the dispose sentinel
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#else
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif

            // Set the initial value
            *m_Buffer = initialValue;
        }

        /// <summary>
        /// Get or set the contained value
        /// 
        /// This operation requires read access to the node for 'get' and write
        /// access to the node for 'set'.
        /// </summary>
        /// 
        /// <value>
        /// The contained value
        /// </value>
        public int Value
        {
            get
            {
                RequireReadAccess();
                return *m_Buffer;
            }

            [WriteAccessRequired]
            set
            {
                RequireWriteAccess();
                *m_Buffer = value;
            }
        }

        /// <summary>
        /// Get a version of this object suitable for use in a ParallelFor job
        /// </summary>
        /// 
        /// <returns>
        /// A version of this object suitable for use in a ParallelFor job
        /// </returns>
        public Parallel GetParallel()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Parallel parallel = new Parallel(m_Buffer, m_Safety);
            AtomicSafetyHandle.UseSecondaryVersion(ref parallel.m_Safety);
#else
			Parallel parallel = new Parallel(m_Buffer);
#endif
            return parallel;
        }

        /// <summary>
        /// Check if the underlying unmanaged memory has been created and not
        /// freed via a call to <see cref="Dispose"/>.
        /// 
        /// This operation has no access requirements.
        ///
        /// This operation is O(1).
        /// </summary>
        /// 
        /// <value>
        /// Initially true when a non-default constructor is called but
        /// initially false when the default constructor is used. After
        /// <see cref="Dispose"/> is called, this becomes false. Note that
        /// calling <see cref="Dispose"/> on one copy of this object doesn't
        /// result in this becoming false for all copies if it was true before.
        /// This property should <i>not</i> be used to check whether the object
        /// is usable, only to check whether it was <i>ever</i> usable.
        /// </value>
        public bool IsCreated
        {
            get { return m_Buffer != null; }
        }

        /// <summary>
        /// Release the object's unmanaged memory. Do not use it after this. Do
        /// not call <see cref="Dispose"/> on copies of the object either.
        /// 
        /// This operation requires write access.
        /// 
        /// This complexity of this operation is O(1) plus the allocator's
        /// deallocation complexity.
        /// </summary>
        [WriteAccessRequired]
        public void Dispose()
        {
            RequireWriteAccess();

// Make sure we're not double-disposing
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
			DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif

            UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
            m_Buffer = null;
        }

        /// <summary>
        /// Set whether both read and write access should be allowed. This is
        /// used for automated testing purposes only.
        /// </summary>
        /// 
        /// <param name="allowReadOrWriteAccess">
        /// If both read and write access should be allowed
        /// </param>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void TestUseOnlySetAllowReadAndWriteAccess(
            bool allowReadOrWriteAccess)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetAllowReadOrWriteAccess(
                m_Safety,
                allowReadOrWriteAccess);
#endif
        }

        /// <summary>
        /// Throw an exception if the object isn't readable
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void RequireReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        /// <summary>
        /// Throw an exception if the object isn't writable
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void RequireWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }
    }

    /// <summary>
    /// Provides a debugger view of <see cref="NativeIntPtr"/>.
    /// </summary>
    internal sealed class NativeIntPtrDebugView
    {
        /// <summary>
        /// The object to provide a debugger view for
        /// </summary>
        private NativeIntPtr m_Ptr;

        /// <summary>
        /// Create the debugger view
        /// </summary>
        /// 
        /// <param name="ptr">
        /// The object to provide a debugger view for
        /// </param>
        public NativeIntPtrDebugView(NativeIntPtr ptr)
        {
            m_Ptr = ptr;
        }

        /// <summary>
        /// Get the viewed object's value
        /// </summary>
        /// 
        /// <value>
        /// The viewed object's value
        /// </value>
        public int Value
        {
            get { return m_Ptr.Value; }
        }
    }
}