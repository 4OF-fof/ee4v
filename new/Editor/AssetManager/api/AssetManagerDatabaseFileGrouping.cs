using System;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.Settings;
using SQLite;

namespace Ee4v.AssetManager.Api
{
    internal static partial class AssetManagerDatabase
    {
        private static ImportedFileParent ResolveImportedFileParent(SQLiteConnection connection, string itemId, string fileName, string classifierText)
        {
            var text = BuildGroupingClassifierText(fileName, classifierText);
            var variantName = ExtractRegexGroupName(SettingApi.Get(AssetManagerDefinitions.VariantGroupRegex), text);
            var versionName = ExtractRegexGroupName(SettingApi.Get(AssetManagerDefinitions.VersionGroupRegex), text);

            var variantGroupId = string.IsNullOrWhiteSpace(variantName)
                ? null
                : EnsureImportedVariantGroup(connection, itemId, variantName);
            var versionGroupId = string.IsNullOrWhiteSpace(versionName)
                ? null
                : EnsureImportedVersionGroup(connection, itemId, variantGroupId, versionName);

            if (!string.IsNullOrWhiteSpace(versionGroupId))
            {
                return new ImportedFileParent(null, versionGroupId, null);
            }

            if (!string.IsNullOrWhiteSpace(variantGroupId))
            {
                return new ImportedFileParent(null, null, variantGroupId);
            }

            return new ImportedFileParent(itemId, null, null);
        }

        private static bool UpdateFileInfoParentSnapshot(SQLiteConnection connection, string fileId, ImportedFileParent parent, string now)
        {
            var row = connection.Query<FileRow>("SELECT * FROM file_info WHERE id = ? LIMIT 1", fileId).FirstOrDefault();
            if (row == null)
            {
                return false;
            }

            var changed = !StringEquals(row.item_info_id, parent.ItemId) ||
                          !StringEquals(row.version_group_id, parent.VersionGroupId) ||
                          !StringEquals(row.variant_group_id, parent.VariantGroupId);
            if (!changed)
            {
                return false;
            }

            var previousVersionGroupId = row.version_group_id;
            connection.Execute(
                @"UPDATE file_info
                  SET item_info_id = ?, version_group_id = ?, variant_group_id = ?, updated_at = ?
                  WHERE id = ?",
                parent.ItemId,
                parent.VersionGroupId,
                parent.VariantGroupId,
                now,
                fileId);
            if (!string.IsNullOrWhiteSpace(previousVersionGroupId) &&
                !StringEquals(previousVersionGroupId, parent.VersionGroupId))
            {
                connection.Execute(
                    "UPDATE version_group SET primary_file_info_id = NULL, updated_at = ? WHERE id = ? AND primary_file_info_id = ?",
                    now,
                    previousVersionGroupId,
                    fileId);
            }

            return true;
        }

        private static void EnsureVersionGroupPrimaryIfMissing(SQLiteConnection connection, string versionGroupId, string fileId, string now)
        {
            if (string.IsNullOrWhiteSpace(versionGroupId) || string.IsNullOrWhiteSpace(fileId))
            {
                return;
            }

            var primaryFileId = connection.ExecuteScalar<string>("SELECT primary_file_info_id FROM version_group WHERE id = ? LIMIT 1", versionGroupId);
            if (!string.IsNullOrWhiteSpace(primaryFileId))
            {
                return;
            }

            connection.Execute("UPDATE version_group SET primary_file_info_id = ?, updated_at = ? WHERE id = ?", fileId, now, versionGroupId);
        }

        private static string BuildGroupingClassifierText(string fileName, string classifierText)
        {
            return (fileName ?? string.Empty) + " " + (classifierText ?? string.Empty);
        }

        private static string ExtractRegexGroupName(string pattern, string text)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            Match match;
            try
            {
                match = Regex.Match(text, pattern);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!match.Success)
            {
                return null;
            }

            var value = match.Groups["name"] != null && match.Groups["name"].Success
                ? match.Groups["name"].Value
                : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                for (var i = 1; i < match.Groups.Count; i++)
                {
                    if (match.Groups[i].Success)
                    {
                        value = match.Groups[i].Value;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = match.Value;
            }

            value = NormalizeDatasourceText(value);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string EnsureImportedVariantGroup(SQLiteConnection connection, string itemId, string name)
        {
            var existing = connection.Query<VariantGroupRow>(
                    "SELECT * FROM variant_group WHERE item_info_id = ? AND name = ? LIMIT 1",
                    itemId,
                    name)
                .FirstOrDefault();
            if (existing != null)
            {
                return existing.id;
            }

            var now = Now();
            var id = NewId();
            connection.Execute(
                "INSERT INTO variant_group(id, item_info_id, name, created_at, updated_at) VALUES (?, ?, ?, ?, ?)",
                id,
                itemId,
                name,
                now,
                now);
            return id;
        }

        private static string EnsureImportedVersionGroup(SQLiteConnection connection, string itemId, string variantGroupId, string name)
        {
            var sql = string.IsNullOrWhiteSpace(variantGroupId)
                ? "SELECT * FROM version_group WHERE item_info_id = ? AND variant_group_id IS NULL AND name = ? LIMIT 1"
                : "SELECT * FROM version_group WHERE item_info_id = ? AND variant_group_id = ? AND name = ? LIMIT 1";
            var parameters = string.IsNullOrWhiteSpace(variantGroupId)
                ? new object[] { itemId, name }
                : new object[] { itemId, variantGroupId, name };
            var existing = connection.Query<VersionGroupRow>(sql, parameters).FirstOrDefault();
            if (existing != null)
            {
                return existing.id;
            }

            var now = Now();
            var id = NewId();
            connection.Execute(
                "INSERT INTO version_group(id, item_info_id, variant_group_id, name, primary_file_info_id, created_at, updated_at) VALUES (?, ?, ?, ?, NULL, ?, ?)",
                id,
                itemId,
                variantGroupId,
                name,
                now,
                now);
            return id;
        }

        private sealed class ImportedFileParent
        {
            public ImportedFileParent(string itemId, string versionGroupId, string variantGroupId)
            {
                ItemId = itemId;
                VersionGroupId = versionGroupId;
                VariantGroupId = variantGroupId;
            }

            public string ItemId { get; private set; }
            public string VersionGroupId { get; private set; }
            public string VariantGroupId { get; private set; }
        }
    }
}
