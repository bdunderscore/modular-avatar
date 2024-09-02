#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
            public int _requestedLength, _trueLength;
            public bool _inUse;

            private Action<Segment> _onDispose;
            public DefragmentCallback Defragment { get; set; }
            public int Offset => _offset;
            public int Length => _requestedLength;

            internal Segment(Action<Segment> onDispose, int offset, int requestedLength, int trueLength, bool inUse)
            {
                if (trueLength < 0 || requestedLength > trueLength)
                {
                    throw new ArgumentException("TrueLength: " + trueLength + " requested " + requestedLength);
                }
                _onDispose = onDispose;
                _offset = offset;
                _requestedLength = requestedLength;
                _trueLength = trueLength;
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

        public event Action<ISegment> OnSegmentDispose;

        public ISegment Allocate(int requested)
        {
            int needed = Math.Max(1, requested);
            
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment._inUse) continue;

                if (segment._trueLength == needed)
                {
                    segment._inUse = true;
                    return segment;
                }

                if (segment._trueLength > needed)
                {
                    var remaining = new Segment(
                        FreeSegment,
                        segment._offset + needed,
                        segment._trueLength - needed,
                        segment._trueLength - needed,
                        false
                    );

                    segment._trueLength = needed;
                    segment._requestedLength = requested;
                    segment._inUse = true;
                    segments.Insert(i + 1, remaining);

                    return segment;
                }
            }

            // Add a new in-use segment at the end
            var newSegment = new Segment(
                FreeSegment,
                segments.Count == 0 ? 0 : segments[segments.Count - 1]._offset + segments[segments.Count - 1]._trueLength,
                requested,
                needed,
                true
            );
            segments.Add(newSegment);

            return newSegment;
        }

        public void FreeSegment(ISegment inputSegment)
        {
            var s = inputSegment as Segment;
            if (s == null) throw new ArgumentException("Passed a foreign segment???");

            OnSegmentDispose?.Invoke(inputSegment);

            int index = segments.BinarySearch(s, Comparer<Segment>.Create((a, b) => a._offset.CompareTo(b._offset)));
            if (index < 0 || segments[index] != s)
            {
                var segmentDump = string.Join("\n", segments.ConvertAll(seg => $"{seg._offset} {seg._requestedLength}/{seg._trueLength} {seg._inUse} id={RuntimeHelpers.GetHashCode(seg)}"));
                segmentDump += "\n\nTarget segment " + s._offset + " " + s._requestedLength + "/" + s._trueLength + " " + s._inUse + " id=" + RuntimeHelpers.GetHashCode(s);
                
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
                    next._trueLength += s._trueLength;
                    next._requestedLength = next._trueLength;
                    segments.RemoveAt(index);
                    return;
                }
            }

            // Replace with a fresh segment object to avoid any issues with leaking old references to the segment
            segments[index] = new Segment(FreeSegment, s._offset, s._requestedLength, s._trueLength, false);
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
                    callback(seg._offset, offset, seg._requestedLength);
                    seg.Defragment?.Invoke(seg._offset, offset, seg._requestedLength);
                    seg._offset = offset;
                }

                offset += seg._trueLength;
            }
        } 
    }
}