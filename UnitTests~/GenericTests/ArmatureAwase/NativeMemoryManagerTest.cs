using System.Collections.Generic;
using nadena.dev.modular_avatar.core.armature_lock;
using NUnit.Framework;
using Unity.Collections;

namespace UnitTests.ArmatureAwase
{
    public class NativeMemoryManagerTest
    {
        [Test]
        public void Test()
        { 
            var mm = new NativeMemoryManager();
            var arr = mm.CreateArray<int>();

            var s1 = mm.Allocate(8);
            SetRange(arr, s1, 101);

            var s2 = mm.Allocate(8);
            SetRange(arr, s2, 102);
            
            mm.Free(s1);
            AssertRange(mm.InUseMask, 0, 8, false);
            AssertRange(mm.InUseMask, 8, 16, true);
            AssertRange(mm.InUseMask, 16, -1, false);

            List<(int, int, int)> defragOps = new List<(int, int, int)>();
            mm.OnSegmentMove += (src, dst, length) => defragOps.Add((src, dst, length));
            var s3 = mm.Allocate(16); // Forces reallocation/defragment
            Assert.AreEqual(s2.Offset, 0);
            Assert.AreEqual(defragOps, new List<(int, int, int)>()
            {
                (8, 0, 8),
            });
            SetRange(arr, s3, 103);
            
            AssertRange(arr, s2, 102);
            
            AssertRange(mm.InUseMask, s2, true);
            AssertRange(mm.InUseMask, s3, true);
            AssertRange(mm.InUseMask, s3.Offset, -1, false);
            
            mm.Dispose();
            
            Assert.IsFalse(arr.Array.IsCreated);
        }

        [Test]
        public void TestNonMovingSegmentInUseMaskRestored()
        {
            // Regression: Defragment cleared all InUseMask bits and relied on the move-callback to restore them.
            // Segments already at their packed offset never triggered the callback, leaving their bits false.
            var mm = new NativeMemoryManager();
            var arr = mm.CreateArray<int>();

            var s1 = mm.Allocate(8); // offset 0 — will not move during defrag
            SetRange(arr, s1, 101);

            var s2 = mm.Allocate(8); // offset 8 — will be freed
            SetRange(arr, s2, 102);

            var s3 = mm.Allocate(8); // offset 16 — will move to 8 during defrag
            SetRange(arr, s3, 103);

            mm.Free(s2); // gap at 8–16; s1 at 0, s3 at 16

            List<(int, int, int)> defragOps = new List<(int, int, int)>();
            mm.OnSegmentMove += (src, dst, length) => defragOps.Add((src, dst, length));

            var s4 = mm.Allocate(16); // gap (8) too small → forces defrag

            Assert.AreEqual(0, s1.Offset);
            Assert.AreEqual(8, s3.Offset);
            Assert.AreEqual(new List<(int, int, int)> { (16, 8, 8) }, defragOps);

            // s1 did not move — its InUseMask bits must be true after defrag
            AssertRange(mm.InUseMask, s1, true);
            AssertRange(mm.InUseMask, s3, true);
            AssertRange(mm.InUseMask, s4, true);

            mm.Dispose();
        }

        private void SetRange<T>(NativeArray<T> arr, ISegment segment, T value) where T : unmanaged
        {
            for (int i = 0; i < segment.Length; i++)
            {
                arr[i + segment.Offset] = value;
            }
        }
        
        private void AssertRange<T>(NativeArray<T> arr, ISegment segment, T value) where T : unmanaged
        {
            AssertRange<T>(arr, segment.Offset, segment.Offset + segment.Length, value);
        }
        
        private void AssertRange<T>(NativeArray<T> arr, int start, int end, T value) where T : unmanaged
        {
            for (int i = start; i < end; i++)
            {
                Assert.AreEqual(value, arr[i]);
            }
        }
    }
}