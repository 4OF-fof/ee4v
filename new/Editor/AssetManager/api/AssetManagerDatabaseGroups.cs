using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.SQLite;
using SQLite;

namespace Ee4v.AssetManager.Api
{
    internal static partial class AssetManagerDatabase
    {
        public static IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId)
        {
            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                return connection.Query<VariantGroupRow>("SELECT * FROM variant_group WHERE item_info_id = ? ORDER BY name COLLATE NOCASE, id", itemId)
                    .Select(ToAssetVariantGroup)
                    .ToArray();
            }
        }

        public static AssetVariantGroup CreateVariantGroup(string itemId, CreateVariantGroupRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Variant group name is required.");
            }

            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                var now = Now();
                var id = NewId();
                connection.Execute(
                    "INSERT INTO variant_group(id, item_info_id, name, created_at, updated_at) VALUES (?, ?, ?, ?, ?)",
                    id,
                    itemId,
                    request.Name,
                    now,
                    now);
                return ToAssetVariantGroup(connection.Query<VariantGroupRow>("SELECT * FROM variant_group WHERE id = ?", id).First());
            }
        }

        public static IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId)
        {
            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                return connection.Query<VersionGroupRow>("SELECT * FROM version_group WHERE item_info_id = ? ORDER BY name COLLATE NOCASE, id", itemId)
                    .Select(ToAssetVersionGroup)
                    .ToArray();
            }
        }

        public static AssetVersionGroup CreateVersionGroup(string itemId, CreateVersionGroupRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Version group name is required.");
            }

            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                if (!string.IsNullOrWhiteSpace(request.VariantGroupId))
                {
                    EnsureVariantGroupBelongsToItem(connection, request.VariantGroupId, itemId);
                }

                var now = Now();
                var id = NewId();
                connection.Execute(
                    "INSERT INTO version_group(id, item_info_id, variant_group_id, name, primary_file_info_id, created_at, updated_at) VALUES (?, ?, ?, ?, NULL, ?, ?)",
                    id,
                    itemId,
                    string.IsNullOrWhiteSpace(request.VariantGroupId) ? null : request.VariantGroupId,
                    request.Name,
                    now,
                    now);

                return ToAssetVersionGroup(connection.Query<VersionGroupRow>("SELECT * FROM version_group WHERE id = ?", id).First());
            }
        }

        public static string SetVersionGroupPrimaryFile(string versionGroupId, string fileId)
        {
            using (var connection = OpenConnection())
            {
                SetVersionGroupPrimaryFile(connection, versionGroupId, fileId);
                return connection.ExecuteScalar<string>("SELECT primary_file_info_id FROM version_group WHERE id = ?", versionGroupId);
            }
        }

        private static void SetVersionGroupPrimaryFile(SQLiteConnection connection, string versionGroupId, string fileId)
        {
            EnsureVersionGroupExists(connection, versionGroupId);
            if (string.IsNullOrWhiteSpace(fileId))
            {
                connection.Execute("UPDATE version_group SET primary_file_info_id = NULL, updated_at = ? WHERE id = ?", Now(), versionGroupId);
                SelectAutomaticVersionGroupPrimary(connection, versionGroupId, Now());
                return;
            }

            EnsureFileExists(connection, fileId);
            var fileVersionGroupId = connection.ExecuteScalar<string>("SELECT version_group_id FROM file_info WHERE id = ?", fileId);
            if (fileVersionGroupId != versionGroupId)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Primary file must belong to the version group.");
            }

            connection.Execute("UPDATE version_group SET primary_file_info_id = ?, updated_at = ? WHERE id = ?", fileId, Now(), versionGroupId);
        }

        private static void EnsureVariantGroupExists(SQLiteConnection connection, string variantGroupId)
        {
            if (string.IsNullOrWhiteSpace(variantGroupId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Variant group id is required.");
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM variant_group WHERE id = ?", variantGroupId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "Variant group was not found.");
            }
        }

        private static void EnsureVersionGroupExists(SQLiteConnection connection, string versionGroupId)
        {
            if (string.IsNullOrWhiteSpace(versionGroupId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Version group id is required.");
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM version_group WHERE id = ?", versionGroupId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "Version group was not found.");
            }
        }

        private static void EnsureVariantGroupBelongsToItem(SQLiteConnection connection, string variantGroupId, string itemId)
        {
            EnsureVariantGroupExists(connection, variantGroupId);
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM variant_group WHERE id = ? AND item_info_id = ?", variantGroupId, itemId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Variant group does not belong to the item.");
            }
        }

        private static void EnsureVersionGroupBelongsToItem(SQLiteConnection connection, string versionGroupId, string itemId)
        {
            EnsureVersionGroupExists(connection, versionGroupId);
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM version_group WHERE id = ? AND item_info_id = ?", versionGroupId, itemId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Version group does not belong to the item.");
            }
        }
    }
}
