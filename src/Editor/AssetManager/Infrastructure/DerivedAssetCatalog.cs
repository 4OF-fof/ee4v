using System;
using System.Collections.Generic;
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
        public string SourceGuid { get; set; }
        public string AssetPath { get; set; }
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
            public string sourceGuid;
        }

        public static string Serialize(
            string parentItemId,
            string name,
            string description,
            string sourceGuid)
        {
            return MetadataPrefix + JsonUtility.ToJson(new Metadata
            {
                parentItemId = parentItemId ?? string.Empty,
                name = name ?? string.Empty,
                description = description ?? string.Empty,
                sourceGuid = sourceGuid ?? string.Empty
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
                    SourceGuid = metadata.sourceGuid ?? string.Empty,
                    AssetPath = assetPath
                };
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
