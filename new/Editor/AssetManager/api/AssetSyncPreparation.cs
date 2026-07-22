using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ee4v.AssetManager.Api.Connecter.Blm;
using Ee4v.AssetManager.Api.Connecter.Eagle;
using Ee4v.Core.Settings;

namespace Ee4v.AssetManager.Api
{
    internal sealed class AssetSyncFieldDiff
    {
        internal AssetSyncFieldDiff(string field, string unityValue, string datasourceValue)
        {
            Field = field ?? string.Empty;
            UnityValue = unityValue ?? string.Empty;
            DatasourceValue = datasourceValue ?? string.Empty;
        }

        internal string Field { get; }

        internal string UnityValue { get; }

        internal string DatasourceValue { get; }
    }

    internal sealed class AssetSyncConflict
    {
        internal AssetSyncConflict(
            AssetSourceType sourceType,
            string sourceId,
            string itemId,
            string itemName,
            DateTime unityUpdatedAtUtc,
            DateTime? datasourceUpdatedAtUtc,
            IReadOnlyList<AssetSyncFieldDiff> fields)
        {
            SourceType = sourceType;
            SourceId = sourceId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            UnityUpdatedAtUtc = unityUpdatedAtUtc;
            DatasourceUpdatedAtUtc = datasourceUpdatedAtUtc;
            Fields = fields ?? Array.Empty<AssetSyncFieldDiff>();
        }

        internal AssetSourceType SourceType { get; }

        internal string SourceId { get; }

        internal string ItemId { get; }

        internal string ItemName { get; }

        internal DateTime UnityUpdatedAtUtc { get; }

        internal DateTime? DatasourceUpdatedAtUtc { get; }

        internal IReadOnlyList<AssetSyncFieldDiff> Fields { get; }
    }

    internal sealed class AssetSyncPreview
    {
        internal AssetSyncPreview(AssetSourceType sourceType, string fingerprint, bool hasChanges, IReadOnlyList<AssetSyncConflict> conflicts)
        {
            SourceType = sourceType;
            Fingerprint = fingerprint ?? string.Empty;
            HasChanges = hasChanges;
            Conflicts = conflicts ?? Array.Empty<AssetSyncConflict>();
        }

        internal AssetSourceType SourceType { get; }

        internal string Fingerprint { get; }

        internal bool HasChanges { get; }

        internal IReadOnlyList<AssetSyncConflict> Conflicts { get; }
    }

    internal sealed class PreparedBlmSync
    {
        internal PreparedBlmSync(IReadOnlyList<BlmItemRecord> records, AssetSyncPreview preview)
        {
            Records = records ?? Array.Empty<BlmItemRecord>();
            Preview = preview;
        }

        internal IReadOnlyList<BlmItemRecord> Records { get; }

        internal AssetSyncPreview Preview { get; }
    }

    internal sealed class PreparedEagleSync
    {
        internal PreparedEagleSync(IReadOnlyList<EagleItemRecord> records, AssetSyncPreview preview)
        {
            Records = records ?? Array.Empty<EagleItemRecord>();
            Preview = preview;
        }

        internal IReadOnlyList<EagleItemRecord> Records { get; }

        internal AssetSyncPreview Preview { get; }
    }

    internal static class AssetSyncFingerprint
    {
        internal static string CreateBlm(IReadOnlyList<BlmItemRecord> records)
        {
            using (var writer = new FingerprintWriter())
            {
                foreach (var record in (records ?? Array.Empty<BlmItemRecord>())
                             .OrderBy(value => value.RegisteredItemId, StringComparer.Ordinal)
                             .ThenBy(value => value.BoothItemId))
                {
                    writer.Add(record.RegisteredItemId);
                    writer.Add(record.BoothItemId);
                    writer.Add(record.Name);
                    writer.Add(record.Description);
                    writer.Add(record.ThumbnailUrl);
                    writer.Add(record.ShopName);
                    writer.Add(record.ShopUrl);
                    writer.Add(record.ShopThumbnailUrl);
                    writer.Add(record.LastUpdatedAtUtc);
                    writer.Add(record.FileSnapshotComplete);
                    AddValues(writer, record.Tags);
                    foreach (var file in (record.Files ?? Array.Empty<BlmFileRecord>())
                                 .OrderBy(value => value.RelativePath, StringComparer.Ordinal))
                    {
                        writer.Add(file.RelativePath);
                        writer.Add(file.FilePath);
                        writer.Add(file.SizeBytes);
                    }
                }

                return writer.Complete();
            }
        }

        internal static string CreateEagle(IReadOnlyList<EagleItemRecord> records)
        {
            using (var writer = new FingerprintWriter())
            {
                foreach (var record in (records ?? Array.Empty<EagleItemRecord>())
                             .OrderBy(value => value.EagleItemId, StringComparer.Ordinal))
                {
                    writer.Add(record.EagleItemId);
                    writer.Add(record.ItemName);
                    writer.Add(record.ItemDescription);
                    writer.Add(record.BoothItemId);
                    writer.Add(record.BoothName);
                    writer.Add(record.BoothDescription);
                    writer.Add(record.BoothThumbnailUrl);
                    writer.Add(record.ShopName);
                    writer.Add(record.ShopUrl);
                    writer.Add(record.ShopThumbnailUrl);
                    writer.Add(record.BoothLastUpdatedAtUtc);
                    AddValues(writer, record.Tags);
                    foreach (var file in (record.Files ?? Array.Empty<EagleFileRecord>())
                                 .OrderBy(value => value.EagleItemId, StringComparer.Ordinal)
                                 .ThenBy(value => value.Name, StringComparer.Ordinal)
                                 .ThenBy(value => value.FilePath, StringComparer.Ordinal))
                    {
                        writer.Add(file.EagleItemId);
                        writer.Add(file.DownloadId);
                        writer.Add(file.Name);
                        writer.Add(file.SizeBytes);
                        writer.Add(file.Extension);
                        writer.Add(file.IsDeleted);
                        writer.Add(file.FilePath);
                    }
                }

                return writer.Complete();
            }
        }

        private static void AddValues(FingerprintWriter writer, IReadOnlyList<string> values)
        {
            foreach (var value in (values ?? Array.Empty<string>()).OrderBy(item => item, StringComparer.Ordinal))
            {
                writer.Add(value);
            }
        }

        private sealed class FingerprintWriter : IDisposable
        {
            private readonly SHA256 _hash = SHA256.Create();
            private bool _completed;

            internal void Add(object value)
            {
                var text = value is DateTime dateTime
                    ? dateTime.ToUniversalTime().ToString("O")
                    : value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
                var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
                var length = BitConverter.GetBytes(bytes.Length);
                _hash.TransformBlock(length, 0, length.Length, null, 0);
                if (bytes.Length > 0)
                {
                    _hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
            }

            internal string Complete()
            {
                if (!_completed)
                {
                    _hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    _completed = true;
                }

                return BitConverter.ToString(_hash.Hash ?? Array.Empty<byte>()).Replace("-", string.Empty).ToLowerInvariant();
            }

            public void Dispose()
            {
                _hash.Dispose();
            }
        }
    }

    internal static class AssetSyncFingerprintCache
    {
        private static readonly object Gate = new object();

        internal static bool Matches(AssetSourceType sourceType, string fingerprint)
        {
            try
            {
                var path = GetPath(sourceType);
                return File.Exists(path) && string.Equals(File.ReadAllText(path).Trim(), fingerprint ?? string.Empty, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        internal static void Save(AssetSourceType sourceType, string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return;
            }

            lock (Gate)
            {
                try
                {
                    var path = GetPath(sourceType);
                    var directory = Path.GetDirectoryName(path);
                    Directory.CreateDirectory(directory);
                    var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    File.WriteAllText(temporaryPath, fingerprint, Encoding.ASCII);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    File.Move(temporaryPath, path);
                }
                catch
                {
                    // Fingerprint persistence is an optimization and must not make synchronization fail.
                }
            }
        }

        internal static string GetPath(AssetSourceType sourceType)
        {
            var globalPath = Environment.ExpandEnvironmentVariables(SettingApi.Get(AssetManagerDefinitions.Ee4vGlobalPath));
            var sourceName = sourceType == AssetSourceType.Eagle ? "eagle" : "blm";
            return Path.GetFullPath(Path.Combine(globalPath, "cache", "sync", sourceName + ".fingerprint"));
        }
    }
}
