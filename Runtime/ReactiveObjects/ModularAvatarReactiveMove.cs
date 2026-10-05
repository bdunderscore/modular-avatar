#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Reactive Move")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/reactive-move?lang=auto")]
    public class ModularAvatarReactiveMove : ReactiveComponent, IHaveObjReferences
    {
        [SerializeField] private AvatarObjectReference m_toMove = new();
        [SerializeField] private AvatarObjectReference m_whereTo = new();
        [SerializeField] private bool m_fixToWorld;
        [SerializeField] private float m_transitionTime;
        
        public AvatarObjectReference ToMove
        {
            get => m_toMove;
            set => m_toMove = value.Clone();
        }

        public AvatarObjectReference WhereTo
        {
            get => m_whereTo;
            set => m_whereTo = value.Clone();
        }

        public bool FixToWorld
        {
            get => m_fixToWorld;
            set => m_fixToWorld = value;
        }

        public float TransitionTime
        {
            get => m_transitionTime;
            set => m_transitionTime = value;
        }

        [SerializeField] private bool m_setPosition;
        [SerializeField] private bool m_setRotation;
        [SerializeField] private bool m_setScale;

        public bool SetsPosition
        {
            get => m_setPosition;
            set => m_setPosition = value;
        }

        public bool SetsRotation
        {
            get => m_setRotation;
            set => m_setRotation = value;
        }

        public bool SetsScale
        {
            get => m_setScale;
            set => m_setScale = value;
        }

        public override void ResolveReferences()
        {
            m_toMove?.Get(this);
            m_whereTo?.Get(this);
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (m_toMove != null) yield return m_toMove;
            if (m_whereTo != null) yield return m_whereTo;
        }
    }
}