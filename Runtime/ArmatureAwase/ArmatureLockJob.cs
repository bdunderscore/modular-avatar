#region

using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal sealed class ArmatureLockJob : IDisposable
    {
        private bool _didLoop = false;

        private Action _dispose;

        private bool _isValid = true;
        private Action _update;

        internal ImmutableList<(Transform, Transform)> RecordedParents;
        internal ImmutableList<(Transform, Transform)> Transforms;

        internal ISegment Segment { get; private set; }

        internal ArmatureLockJob(ISegment Segment, ImmutableList<(Transform, Transform)> transforms, Action dispose,
            Action update)
        {
            this.Segment = Segment;
            Transforms = transforms;
            RecordedParents = transforms.Select(((tuple, _) => (tuple.Item1.parent, tuple.Item2.parent)))
                .ToImmutableList();
            _dispose = dispose;
            _update = update;
        }

        internal bool FailedOnStartup => !_isValid && !_didLoop;

        internal bool HierarchyChanged
        {
            get
            {
                var unchanged = RecordedParents.Zip(Transforms,
                    (p, t) =>
                    {
                        return t.Item1 != null && t.Item2 != null && t.Item1.parent == p.Item1 &&
                               t.Item2.parent == p.Item2;
                    }).All(b => b);

                return !unchanged;
            }
        }

        internal bool IsValid
        {
            get => _isValid;
            set
            {
                var transitioned = (_isValid && !value);
                _isValid = value;
                
                if (transitioned)
                {
#if UNITY_EDITOR
                    EditorApplication.delayCall += () => OnInvalidation?.Invoke();
#endif
                }
            }
        }

        internal bool WroteAny { get; set; }

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
            _update = null;
        }

        internal event Action OnInvalidation;

        internal void MarkLoop()
        {
            _didLoop = _didLoop || _isValid;
        }

        internal bool BoneChanged(int boneIndex)
        {
            return Transforms[boneIndex].Item1 == null || Transforms[boneIndex].Item2 == null
                                                       || Transforms[boneIndex].Item1.parent !=
                                                       RecordedParents[boneIndex].Item1
                                                       || Transforms[boneIndex].Item2.parent !=
                                                       RecordedParents[boneIndex].Item2;
        }

        public void UpdateNow()
        {
            _update?.Invoke();
        }
    }
}