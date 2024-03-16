using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.armature_lock;
using NUnit.Framework;

namespace UnitTests.ArmatureAwase
{
    public class AllocationMapTest
    {
        [Test]
        public void Test()
        {
            AllocationMap map = new AllocationMap();
            
            ISegment s1 = map.Allocate(10);
            AssertSegment(s1, 0, 10, true);
            
            ISegment s2 = map.Allocate(5);
            AssertSegment(s2, 10, 5, true);
            
            map.FreeSegment(s1);
            s1 = map.Allocate(5);
            AssertSegment(s1, 0, 5, true);
            
            var s1a = map.Allocate(3);
            AssertSegment(s1a, 5, 3, true);
            
            var s3 = map.Allocate(3);
            AssertSegment(s3, 15, 3, true);

            List<(ISegment, int, int, int)> segmentDefrags = new List<(ISegment, int, int, int)>();
            List<(int, int, int)> globalDefrags = new List<(int, int, int)>();

            s1.Defragment = (src, dst, length) => segmentDefrags.Add((s1, src, dst, length));
            s1a.Defragment = (src, dst, length) => segmentDefrags.Add((s1a, src, dst, length));
            s2.Defragment = (src, dst, length) => segmentDefrags.Add((s2, src, dst, length));
            s3.Defragment = (src, dst, length) => segmentDefrags.Add((s3, src, dst, length));
            
            map.Defragment((src, dst, length) => globalDefrags.Add((src, dst, length)));
            
            Assert.AreEqual(segmentDefrags, new List<(ISegment, int, int, int)>()
            {
                (s2, 10, 8, 5),
                (s3, 15, 13, 3),
            });
            
            Assert.AreEqual(globalDefrags, new List<(int, int, int)>()
            {
                (10, 8, 5),
                (15, 13, 3),
            });
        }

        [Test]
        public void SegmentCoalescing()
        {
            var map = new AllocationMap();
            var s1 = map.Allocate(10);
            var s2 = map.Allocate(10);
            var s3 = map.Allocate(10);
            
            map.FreeSegment(s2);
            map.FreeSegment(s1);
            
            var s4 = map.Allocate(20);
            
            AssertSegment(s4, 0, 20, true);
        }

        enum Op
        {
            Allocate, Deallocate, Defrag
        }

        [Test]
        public void SegmentRandomOps()
        {
            for (int i = 0; i < 1000; i++)
            {
                AllocationMap map = new AllocationMap();
                
                List<ISegment> segments = new List<ISegment>();
                List<(Op, int)> ops = new List<(Op, int)>();

                try
                {
                    Random r = new Random();
                    for (int j = 0; j < 100; j++)
                    {
                        switch (r.Next(0, segments.Count == 0 ? 1 : 3))
                        {
                            case 0:
                            {
                                int segSize = r.Next(1, 16);
                                ISegment s = map.Allocate(segSize);
                                ops.Add((Op.Allocate, segSize));
                                segments.Add(s);
                                break;
                            }
                            case 1:
                            {
                                int idx = r.Next(0, segments.Count);
                                ISegment s = segments[idx];
                                map.FreeSegment(s);
                                ops.Add((Op.Deallocate, idx));
                                segments.RemoveAt(idx);
                                break;
                            }
                            case 2:
                            {
                                ops.Add((Op.Defrag, 0));
                                map.Defragment((src, dst, length) => {});
                                break;
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    var trace = string.Join("\n", ops.ConvertAll(op => $"{op.Item1} {op.Item2}"));
                    Assert.Fail($"Failed at iteration {i} with exception {e}\n{trace}");
                    throw;
                }
            }
        }

        private void AssertSegment(ISegment segment, int offset, int length, bool inUse)
        {
            var s = segment as AllocationMap.Segment;
            
            Assert.AreEqual(offset, segment.Offset);
            Assert.AreEqual(length, segment.Length);
            Assert.AreEqual(inUse, s._inUse);
        }
    }
}