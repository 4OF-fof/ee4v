using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure
{
    public sealed class DerivedAssetRecord
    {
        public string ParentItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<string> Tags { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string SourceGuid { get; set; }
        public string AssetPath { get; set; }
        public bool IsArchived { get; set; }
    }

    public static class DerivedAssetCatalog
    {
        public const string VariantRoot = "Assets/!ee4vAsset/Variant";
        private const string MetadataPrefix = "ee4v-derived-asset:v1:";

        [Serializable]
        private sealed class Metadata
        {
            public string parentItemId;
            public string name;
            public string description;
            public string[] tags;
            public string createdAt;
            public string updatedAt;
            public string sourceGuid;
            public bool isArchived;
        }

        public static string Serialize(
            string parentItemId,
            string name,
            string description,
            string sourceGuid, bool isArchived = false,
            IReadOnlyList<string> tags = null, DateTime? createdAt = null)
        {
            return MetadataPrefix + JsonUtility.ToJson(new Metadata
            {
                parentItemId = parentItemId ?? string.Empty,
                name = name ?? string.Empty,
                description = description ?? string.Empty,
                tags = (tags ?? Array.Empty<string>()).ToArray(),
                createdAt = (createdAt ?? DateTime.UtcNow).ToString("O"),
                updatedAt = DateTime.UtcNow.ToString("O"),
                sourceGuid = sourceGuid ?? string.Empty,
                isArchived = isArchived
            });
        }

        public static IReadOnlyList<DerivedAssetRecord> FindByParentItem(
            string parentItemId)
        {
            if (string.IsNullOrEmpty(parentItemId) ||
                !AssetDatabase.IsValidFolder(VariantRoot))
            {
                return Array.Empty<DerivedAssetRecord>();
            }

            return AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { VariantRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(Read)
                .Where(record => record != null && string.Equals(
                    record.ParentItemId,
                    parentItemId,
                    StringComparison.Ordinal))
                .OrderBy(
                    record => record.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    record => record.AssetPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<DerivedAssetRecord> FindAll()
        {
            if (!AssetDatabase.IsValidFolder(VariantRoot))
            {
                return Array.Empty<DerivedAssetRecord>();
            }

            return AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { VariantRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(Read)
                .Where(record => record != null)
                .OrderBy(
                    record => record.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    record => record.AssetPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static DerivedAssetRecord Read(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var importer = AssetImporter.GetAtPath(assetPath);
            var data = importer?.userData ?? string.Empty;
            if (!data.StartsWith(MetadataPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                var metadata = JsonUtility.FromJson<Metadata>(
                    data.Substring(MetadataPrefix.Length));
                if (metadata == null)
                {
                    return null;
                }

                return new DerivedAssetRecord
                {
                    ParentItemId = metadata.parentItemId ?? string.Empty,
                    Name = string.IsNullOrEmpty(metadata.name)
                        ? Path.GetFileNameWithoutExtension(assetPath)
                        : metadata.name,
                    Description = metadata.description ?? string.Empty,
                    Tags = metadata.tags ?? Array.Empty<string>(),
                    CreatedAt = ParseTimestamp(metadata.createdAt, File.GetCreationTimeUtc(assetPath)),
                    UpdatedAt = new[] { ParseTimestamp(metadata.updatedAt, default),
                        File.GetLastWriteTimeUtc(assetPath) }.Max(),
                    SourceGuid = metadata.sourceGuid ?? string.Empty,
                    AssetPath = assetPath,
                    IsArchived = metadata.isArchived
                };
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static DateTime ParseTimestamp(string value, DateTime fallback) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out var timestamp) ? timestamp.ToUniversalTime() : fallback;
    }
}
