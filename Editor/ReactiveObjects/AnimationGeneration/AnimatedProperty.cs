using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class AnimatedProperty
    {
        public TargetProp TargetProp { get; }
        public string ControlParam { get; set; }

        public bool alwaysDeleted;
        public object currentState;

        // Objects which trigger deletion of this shape key. 
        public List<ReactionRule> actionGroups = new List<ReactionRule>();

        public AnimatedProperty(TargetProp key, float currentState)
        {
            TargetProp = key;
            this.currentState = currentState;
        }
            
        public AnimatedProperty(TargetProp key, Object currentState)
        {
            TargetProp = key;
            this.currentState = currentState;
        }
    }
}