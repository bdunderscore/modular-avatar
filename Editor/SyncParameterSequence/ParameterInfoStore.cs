#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.SyncParameterSequence
{
    internal interface IParameterInfoStore
    {
        AvatarRecord GetRecordForBlueprintId(string blueprintId);
        AvatarRecord UpdateRecordForPlatform(string blueprintId, bool isPrimary, ParameterInfoRecord record);
    }

    internal abstract class AbstractParameterInfoStore : IParameterInfoStore
    {
        protected abstract void Store(string blueprintId, string serialized);
        protected abstract string? Load(string blueprintId);

        public AvatarRecord GetRecordForBlueprintId(string blueprintId)
        {
            try
            {
                var serialized = Load(blueprintId);
                return serialized == null
                    ? new AvatarRecord()
                    : JsonConvert.DeserializeObject<AvatarRecord>(serialized) ?? new AvatarRecord();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new AvatarRecord();
            }
        }

        public AvatarRecord UpdateRecordForPlatform(string blueprintId, bool isPrimary, ParameterInfoRecord record)
        {
            try
            {
                var current = Load(blueprintId);
                var avatarRecord = current == null
                    ? new AvatarRecord()
                    : JsonConvert.DeserializeObject<AvatarRecord>(current) ?? new AvatarRecord();

                if (isPrimary)
                {
                    avatarRecord.PrimaryPlatformRecord = record;
                }
                else
                {
                    avatarRecord.SecondaryPlatformRecords.RemoveAll(m => m.Target == record.Target);
                    avatarRecord.SecondaryPlatformRecords.Add(record);
                }

                Store(blueprintId, JsonConvert.SerializeObject(avatarRecord));
                return avatarRecord;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new AvatarRecord();
            }
        }
    }

    internal sealed class ParameterInfoStore : AbstractParameterInfoStore
    {
        public static IParameterInfoStore Instance { get; } = new ParameterInfoStore();

        private const string LibraryFolderPath = "Library/nadena.dev.modular-avatar/AvatarParameterInfo";

        private ParameterInfoStore()
        {
        }

        private void EnsureFolderExists()
        {
            if (!Directory.Exists(LibraryFolderPath))
            {
                Directory.CreateDirectory(LibraryFolderPath);
            }
        }

        protected override void Store(string blueprintId, string serialized)
        {
            try
            {
                EnsureFolderExists();
                var path = Path.Combine(LibraryFolderPath, blueprintId);
                File.WriteAllText(path, serialized);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected override string? Load(string blueprintId)
        {
            try
            {
                EnsureFolderExists();
                var path = Path.Combine(LibraryFolderPath, blueprintId);
                if (!File.Exists(path)) return null;
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
    }
}

#endif