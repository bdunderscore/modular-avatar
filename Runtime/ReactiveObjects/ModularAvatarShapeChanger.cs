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
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/shape-changer?lang=auto")]
    public class ModularAvatarShapeChanger : ReactiveComponent, IHaveObjReferences
    {
        // Migration field to help with 1.10-beta series avatar data. Since this was never in a released version of MA,
        // this migration support will be removed in 1.10.0.
        [SerializeField] [FormerlySerializedAs("targetRenderer")] [HideInInspector]
        private AvatarObjectReference m_targetRenderer = new();

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

        private void OnEnable()
        {
            MigrateTargetRenderer();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            MigrateTargetRenderer();
        }

        // Migrate early versions of MASC (from Modular Avatar 1.10.0-beta.4 or earlier) to the new format, where the
        // target renderer is stored separately for each shape.
        // This logic will be removed in 1.10.0.
        private void MigrateTargetRenderer()
        {
            // Note: This method runs in the context of OnValidate, and therefore cannot touch any other unity objects.
            if (!string.IsNullOrEmpty(m_targetRenderer.referencePath) || m_targetRenderer.targetObject != null)
            {
                foreach (var shape in m_shapes)
                {
                    if (shape.Object == null) shape.Object = new AvatarObjectReference();
                    
                    if (string.IsNullOrEmpty(shape.Object.referencePath) && shape.Object.targetObject == null)
                    {
                        shape.Object.referencePath = m_targetRenderer.referencePath;
                        shape.Object.targetObject = m_targetRenderer.targetObject;
                    }
                }
                m_targetRenderer.referencePath = null;
                m_targetRenderer.targetObject = null;
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