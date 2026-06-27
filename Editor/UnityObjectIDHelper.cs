using System;
using UnityEditor;
using UnityEngine;
namespace nadena.dev.modular_avatar.core
{

    public static class UnityObjectIDHelper
    {

#if !UNITY_6000_2_OR_NEWER
        public static EntityId GetEntityId(this UnityEngine.Object unityObject)
        {
            return new(unityObject.GetInstanceID());
        }
#endif

        public static UnityEngine.Object EntityIdToObject(EntityId entityId)
        {
#if UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(entityId);
#elif UNITY_6000_2_OR_NEWER
            return EditorUtility.InstanceIDToObject(entityId);
#else
            return EditorUtility.InstanceIDToObject(entityId.InstanceID);
#endif
        }

        public static EntityId InvalidID =>
#if UNITY_6000_4_OR_NEWER
        EntityId.FromULong(ulong.MaxValue);// ulong.MaxValue == long -1
#elif UNITY_6000_2_OR_NEWER
        -1;
#else
        new(-1);
#endif

    }
#if !UNITY_6000_2_OR_NEWER
    public struct EntityId : IEquatable<EntityId>
    {
        public int InstanceID;

        public EntityId(int id)
        {
            InstanceID = id;
        }

        public bool Equals(EntityId other)
        {
            return InstanceID == other.InstanceID;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId eid && Equals(eid);
        }
        public override int GetHashCode() => InstanceID;
        public static EntityId None => default(EntityId);
        public static EntityId FromULong(ulong fromValue) => new(unchecked((int)fromValue));

        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
#endif
}
