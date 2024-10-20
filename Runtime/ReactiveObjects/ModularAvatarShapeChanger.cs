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
    public class ChangedShape
    {
        public AvatarObjectReference Object;
        public string ShapeName;
        public ShapeChangeType ChangeType;
        public float Value;

        public ChangedShape Clone()
        {
            return new ChangedShape
            {
                Object = Object.Clone(),
                ShapeName = ShapeName,
                ChangeType = ChangeType,
                Value = Value
            };
        }
        
        public bool Equals(ChangedShape other)
        {
            return Equals(Object, other.Object) && ShapeName == other.ShapeName && ChangeType == other.ChangeType && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ChangedShape other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Object, ShapeName, (int)ChangeType, Value);
        }

        public override string ToString()
        {
            return $"{Object.referencePath} {ShapeName} {ChangeType} {Value}";
        }
    }

    [AddComponentMenu("Modular Avatar/MA Shape Changer")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/shape-changer?lang=auto")]
    public class ModularAvatarShapeChanger : ReactiveComponent, IHaveObjReferences
    {
        [SerializeField] [FormerlySerializedAs("Shapes")]
        private List<ChangedShape> m_shapes = new();

        public List<ChangedShape> Shapes
        {
            get => m_shapes;
            set => m_shapes = value;
        }

        public override void ResolveReferences()
        {
            foreach (var shape in m_shapes)
            {
                shape.Object?.Get(this);
            }
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            foreach (var shape in m_shapes)
                if (shape.Object != null)
                    yield return shape.Object;
        }
    }
}