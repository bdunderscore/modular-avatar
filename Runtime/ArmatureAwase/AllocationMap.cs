#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal interface ISegment : IDisposable
    {
        AllocationMap.DefragmentCallback Defragment { get; set; }
        int Offset { get; }
        int Length { get; }
    }

    internal class AllocationMap
    {
        public delegate void DefragmentCallback(int oldOffset, int newOffset, int length);

        // Visible for unit tests
        internal class Segment : ISegment
        {
            public int _offset;
            public int _length;
            public bool _inUse;

            private Action<Segment> _onDispose;
            public DefragmentCallback Defragment { get; set; }
            public int Offset => _offset;
            public int Length => _length;

            internal Segment(Action<Segment> onDispose, int offset, int length, bool inUse)
            {
                _onDispose = onDispose;
                _offset = offset;
                _length = length;
                _inUse = inUse;
            }

            public void Dispose()
            {
                _onDispose?.Invoke(this);
                _onDispose = null;
            }
        }

        /// <summary>
        /// A list of allocated (and unallocated) segments.
        ///
        /// Invariant: The last element (if any) is always inUse.
        /// Invariant: No two consecutive elements are free (inUse = false).
        /// 
        /// </summary>
        List<Segment> segments = new List<Segment>();

        public ISegment Allocate(int requestedLength)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment._inUse) continue;

                if (segment._length == requestedLength)
                {
                    segment._inUse = true;
                    return segment;
                }

                if (segment._length > requestedLength)
                {
                    var remaining = new Segment(
                        FreeSegment,
                        segment._offset + requestedLength,
                        segment._length - requestedLength,
                        false
                    );

                    segment._length = requestedLength;
                    segment._inUse = true;
                    segments.Insert(i + 1, remaining);

                    return segment;
                }
            }

            // Add a new in-use segment at the end
            var newSegment = new Segment(
                FreeSegment,
                segments.Count == 0 ? 0 : segments[segments.Count - 1]._offset + segments[segments.Count - 1]._length,
                requestedLength,
                true
            );
            segments.Add(newSegment);

            return newSegment;
        }

        public void FreeSegment(ISegment inputSegment)
        {
            var s = inputSegment as Segment;
            if (s == null) throw new ArgumentException("Passed a foreign segment???");

            int index = segments.BinarySearch(s, Comparer<Segment>.Create((a, b) => a._offset.CompareTo(b._offset)));
            if (index < 0 || segments[index] != s)
            {
                var segmentDump = string.Join("\n", segments.ConvertAll(seg => $"{seg._offset} {seg._length} {seg._inUse} id={RuntimeHelpers.GetHashCode(seg)}"));
                segmentDump += "\n\nTarget segment " + s._offset + " " + s._length + " " + s._inUse + " id=" + RuntimeHelpers.GetHashCode(s);
                
                throw new Exception("Segment not found in FreeSegment\nCurrent segments:\n" + segmentDump);
            }

            if (index == segments.Count - 1)
            {
                segments.RemoveAt(index);
                return;
            }

            if (index + 1 < segments.Count)
            {
                var next = segments[index + 1];
                if (!next._inUse)
                {
                    next._offset = s._offset;
                    next._length += s._length;
                    segments.RemoveAt(index);
                    return;
                }
            }

            // Replace with a fresh segment object to avoid any issues with leaking old references to the segment
            segments[index] = new Segment(FreeSegment, s._offset, s._length, false);
        }

        /// <summary>
        /// Defragments all free space. When a segment is moved, the passed callback is called with the old and new offsets,
        /// and then the callback associated with the segment (if any) is also invoked.
        /// </summary>
        /// <param name="callback"></param>
        public void Defragment(DefragmentCallback callback)
        {
            int offset = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (!seg._inUse)
                {
                    segments.RemoveAt(i);
                    i--;
                    continue;
                }

                if (seg._offset != offset)
                {
                    callback(seg._offset, offset, seg._length);
                    seg.Defragment?.Invoke(seg._offset, offset, seg._length);
                    seg._offset = offset;
                }

                offset += seg.Length;
            }
        } 
    }
}