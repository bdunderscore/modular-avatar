using System;
using UnityEditor;
using UnityEngine;
namespace nadena.dev.modular_avatar.core
{

    public static class UnityObjectIDHelper
    {

#if !UNITY_6000_4_OR_NEWER
        public static EntityId GetEntityId(this UnityEngine.Object unityObject)
        {
            return new(unityObject.GetInstanceID());
        }
#endif

        public static UnityEngine.Object EntityIdToObject(EntityId entityId)
        {
#if UNITY_6000_4_OR_NEWER
            return EditorUtility.EntityIdToObject(entityId);
#else
            return EditorUtility.InstanceIDToObject(entityId.InstanceID);
#endif
        }

        public static EntityId ObjectReferenceEntityIdValue(this SerializedProperty unityObject)
        {
#if UNITY_6000_4_OR_NEWER
            return unityObject.objectReferenceEntityIdValue;
#else
            return new(unityObject.objectReferenceInstanceIDValue);
#endif
        }


        public static EntityId GetEntityId(this CreateGameObjectHierarchyEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static EntityId GetEntityId(this ChangeChildrenOrderEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static Unity.Collections.NativeArray<EntityId>.ReadOnly GetEntityIds(this UpdatePrefabInstancesEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityIds;
#else
            return data.instanceIds.Reinterpret<EntityId>();
#endif
        }
        public static EntityId GetEntityId(this ChangeAssetObjectPropertiesEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static EntityId GetEntityId(this DestroyAssetObjectEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static EntityId GetEntityId(this DestroyGameObjectHierarchyEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static EntityId GetEntityId(this ChangeGameObjectOrComponentPropertiesEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static EntityId GetEntityId(this ChangeGameObjectParentEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static EntityId GetEntityId(this ChangeGameObjectStructureEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }
        public static EntityId GetEntityId(this ChangeGameObjectStructureHierarchyEventArgs data)
        {
#if UNITY_6000_6_OR_NEWER
            return data.entityId;
#else
            return new(data.instanceId);
#endif
        }


    }
#if !UNITY_6000_4_OR_NEWER
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
