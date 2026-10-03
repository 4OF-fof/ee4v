using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarInfo
{
    public static class AvatarBuildSizeCache
    {
        private const string StoreKey = "Ee4v.AvatarInfo.BuildSizes";

        public sealed class Record
        {
            public string Key;
            public bool Mobile;
            public long DownloadBytes;
            public long? UncompressedBytes;
            public DateTime CapturedAt;
        }

        private static readonly List<Record> Records =
            JsonConvert.DeserializeObject<List<Record>>(SessionState.GetString(StoreKey, "[]"))
            ?? new List<Record>();

        public static event Action Changed;

        public static Record Get(GameObject avatar, bool mobile)
        {
            if (avatar == null) return null;
            var key = AvatarPlayModePerformanceCache.GetKey(avatar);
            return Records.FirstOrDefault(record => record.Key == key && record.Mobile == mobile);
        }

        public static string GetCaptureKey(GameObject avatar) =>
            avatar == null ? null : AvatarPlayModePerformanceCache.GetKey(avatar);

        // Called only after a build the user started in the official SDK succeeds.
        public static void Capture(string key, bool mobile, string bundlePath, long? uncompressedBytes)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(bundlePath) || !File.Exists(bundlePath)) return;
            var record = new Record
            {
                Key = key, Mobile = mobile,
                DownloadBytes = new FileInfo(bundlePath).Length,
                UncompressedBytes = uncompressedBytes,
                CapturedAt = DateTime.Now
            };
            Records.RemoveAll(value => value.Key == key && value.Mobile == mobile);
            Records.Add(record);
            SessionState.SetString(StoreKey, JsonConvert.SerializeObject(Records));
            Changed?.Invoke();
        }
    }
}
