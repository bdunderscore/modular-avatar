#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public enum ShapeChangeType
    {
        Delete,
        Set
    }

    [Serializable]
    public struct ChangedShape
    {
        public string ShapeName;
        public ShapeChangeType ChangeType;
        public float Value;

        public bool Equals(ChangedShape other)
        {
            return ShapeName == other.ShapeName && ChangeType == other.ChangeType && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ChangedShape other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ShapeName, (int)ChangeType, Value);
        }

        public override string ToString()
        {
            return $"{ShapeName} {ChangeType} {Value}";
        }
    }

    [AddComponentMenu("Modular Avatar/MA Shape Changer")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/shape-changer?lang=auto")]
    public class ModularAvatarShapeChanger : ReactiveComponent
    {
        [SerializeField] [FormerlySerializedAs("targetRenderer")]
        private AvatarObjectReference m_targetRenderer;

        public AvatarObjectReference targetRenderer
        {
            get => m_targetRenderer;
            set => m_targetRenderer = value;
        }

        [SerializeField] [FormerlySerializedAs("Shapes")]
        private List<ChangedShape> m_shapes = new();

        public List<ChangedShape> Shapes
        {
            get => m_shapes;
            set => m_shapes = value;
        }

        public override void ResolveReferences()
        {
            m_targetRenderer?.Get(this);
        }
    }
}