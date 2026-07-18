using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Api.Connecter.Blm;
using Ee4v.AssetManager.Api.Connecter.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Api
{
    internal static partial class AssetManagerDatabase
    {
        public static IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query)
        {
            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                var where = new List<string>
                {
                    @"(item_info_id = ?
                       OR variant_group_id IN (SELECT id FROM variant_group WHERE item_info_id = ?)
                       OR version_group_id IN (SELECT id FROM version_group WHERE item_info_id = ?))"
                };
                var parameters = new List<object> { itemId, itemId, itemId };

                if (query != null && query.Lifecycle.HasValue)
                {
                    where.Add("lifecycle = ?");
                    parameters.Add(ToDbLifecycle(query.Lifecycle.Value));
                }

                if (query != null && !string.IsNullOrWhiteSpace(query.Extension))
                {
                    where.Add("extension = ?");
                    parameters.Add(query.Extension.TrimStart('.'));
                }

                if (query != null && query.SourceType.HasValue)
                {
                    where.Add(FileHasSourceClause(query.SourceType.Value));
                }

                var rows = connection.Query<FileRow>(
                    "SELECT * FROM file_info WHERE " + string.Join(" AND ", where.ToArray()) + " ORDER BY file_name COLLATE NOCASE, id",
                    parameters.ToArray());
                return rows.Select(row => ToAssetFile(connection, row)).ToArray();
            }
        }

        public static AssetFile RegisterFile(string itemId, RegisterFileRequest request)
        {
            if (string.IsNullOrWhiteSpace(itemId) || request == null || string.IsNullOrWhiteSpace(request.FilePath))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Item id and file path are required.");
            }

            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                var now = Now();
                var fileId = NewId();
                var fileName = string.IsNullOrWhiteSpace(request.FileName)
                    ? Path.GetFileName(request.FilePath)
                    : request.FileName;

                var parent = ResolveRegisterFileParent(connection, itemId, request, fileName);
                InsertFileInfo(connection, fileId, parent.ItemId, parent.VersionGroupId, parent.VariantGroupId, fileName, GetExtension(fileName), request.SizeBytes, null, now);
                EnsureVersionGroupPrimaryIfMissing(connection, parent.VersionGroupId, fileId, now);
                if (string.IsNullOrWhiteSpace(request.VersionGroupId) && string.IsNullOrWhiteSpace(request.VariantGroupId))
                {
                    ReconcileImportedFileGroups(connection, itemId, now);
                }

                connection.Execute(
                    "INSERT INTO ee4v_file_origin(file_info_id, ee4v_file_id, file_path_cache, imported_at) VALUES (?, ?, ?, ?)",
                    fileId,
                    NewId(),
                    request.FilePath,
                    now);
                return ToAssetFile(connection, connection.Query<FileRow>("SELECT * FROM file_info WHERE id = ?", fileId).First());
            }
        }

        public static void ArchiveFile(string fileId)
        {
            using (var connection = OpenConnection())
            {
                EnsureFileExists(connection, fileId);
                connection.Execute("UPDATE file_info SET lifecycle = 'archived', updated_at = ? WHERE id = ?", Now(), fileId);
            }
        }

        public static AssetFilePathResolution ResolveFilePath(string fileId)
        {
            using (var connection = OpenConnection())
            {
                var file = connection.Query<FileRow>("SELECT * FROM file_info WHERE id = ? LIMIT 1", fileId).FirstOrDefault();
                if (file == null)
                {
                    return new AssetFilePathResolution { Found = false, MissingReason = "file not found" };
                }

                var origins = LoadOrigins(connection, fileId).ToArray();
                var ordered = OrderOrigins(origins).ToArray();
                for (var i = 0; i < ordered.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(ordered[i].FilePathCache) &&
                        (File.Exists(ordered[i].FilePathCache) || Directory.Exists(ordered[i].FilePathCache)))
                    {
                        return new AssetFilePathResolution
                        {
                            Found = true,
                            Path = ordered[i].FilePathCache,
                            SourceType = ordered[i].SourceType
                        };
                    }
                }

                return new AssetFilePathResolution { Found = false, MissingReason = origins.Length == 0 ? "origin not found" : "path not found" };
            }
        }

        public static IReadOnlyList<AssetFileDependency> GetFileDependencies(string fileId)
        {
            using (var connection = OpenConnection())
            {
                EnsureFileExists(connection, fileId);
                return connection.Query<DependencyRow>(
                        "SELECT * FROM dependency WHERE source_file_info_id = ? AND target_file_info_id IS NOT NULL ORDER BY target_file_info_id",
                        fileId)
                    .Select(row => new AssetFileDependency
                    {
                        DependentFileId = row.source_file_info_id,
                        DependencyFileId = row.target_file_info_id
                    })
                    .ToArray();
            }
        }

        public static void SetFileDependencies(string dependentFileId, IReadOnlyList<string> dependencyFileIds)
        {
            using (var connection = OpenConnection())
            {
                EnsureFileExists(connection, dependentFileId);
                var ids = dependencyFileIds ?? Array.Empty<string>();
                for (var i = 0; i < ids.Count; i++)
                {
                    if (dependentFileId == ids[i])
                    {
                        throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Self dependency is not allowed.");
                    }

                    EnsureFileExists(connection, ids[i]);
                }

                connection.Execute("DELETE FROM dependency WHERE source_file_info_id = ?", dependentFileId);
                for (var i = 0; i < ids.Count; i++)
                {
                    connection.Execute(
                        "INSERT OR IGNORE INTO dependency(source_file_info_id, target_file_info_id) VALUES (?, ?)",
                        dependentFileId,
                        ids[i]);
                }
            }
        }

        private static string ItemHasSourceClause(AssetSourceType sourceType)
        {
            return @"EXISTS (
                SELECT 1
                FROM file_info
                WHERE " + FileHasSourceClause(sourceType) + @"
                  AND (
                    file_info.item_info_id = item_info.id
                    OR file_info.variant_group_id IN (SELECT id FROM variant_group WHERE item_info_id = item_info.id)
                    OR file_info.version_group_id IN (SELECT id FROM version_group WHERE item_info_id = item_info.id)
                  ))";
        }

        private static string FileHasSourceClause(AssetSourceType sourceType)
        {
            if (sourceType == AssetSourceType.Ee4v)
            {
                return "file_info.id IN (SELECT file_info_id FROM ee4v_file_origin)";
            }

            if (sourceType == AssetSourceType.Eagle)
            {
                return "file_info.id IN (SELECT file_info_id FROM eagle_file_origin)";
            }

            return "file_info.id IN (SELECT file_info_id FROM blm_file_origin)";
        }

        private static int OriginCount(SQLiteConnection connection, string fileId)
        {
            return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM ee4v_file_origin WHERE file_info_id = ?", fileId) +
                   connection.ExecuteScalar<int>("SELECT COUNT(*) FROM eagle_file_origin WHERE file_info_id = ?", fileId) +
                   connection.ExecuteScalar<int>("SELECT COUNT(*) FROM blm_file_origin WHERE file_info_id = ?", fileId);
        }

        private static IEnumerable<AssetFileOrigin> OrderOrigins(IEnumerable<AssetFileOrigin> origins)
        {
            var priorities = SourcePriorityUtility.GetPriority();
            return origins.OrderBy(origin =>
            {
                for (var i = 0; i < priorities.Count; i++)
                {
                    if (origin.SourceType == priorities[i])
                    {
                        return i;
                    }
                }

                return priorities.Count;
            });
        }

        private static ImportedFileParent ResolveRegisterFileParent(SQLiteConnection connection, string itemId, RegisterFileRequest request, string fileName)
        {
            var hasVersion = !string.IsNullOrWhiteSpace(request.VersionGroupId);
            var hasVariant = !string.IsNullOrWhiteSpace(request.VariantGroupId);
            if (hasVersion && hasVariant)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "File parent must be item, version group, or variant group.");
            }

            if (hasVersion)
            {
                EnsureVersionGroupBelongsToItem(connection, request.VersionGroupId, itemId);
                return new ImportedFileParent(null, request.VersionGroupId, null);
            }

            if (hasVariant)
            {
                EnsureVariantGroupBelongsToItem(connection, request.VariantGroupId, itemId);
                return new ImportedFileParent(null, null, request.VariantGroupId);
            }

            return ResolveImportedFileParent(connection, itemId, fileName);
        }

        private static void InsertFileInfo(SQLiteConnection connection, string fileId, string itemId, string versionGroupId, string variantGroupId, string fileName, string extension, long? sizeBytes, long? downloadId, string now)
        {
            connection.Execute(
                @"INSERT INTO file_info(id, item_info_id, version_group_id, variant_group_id, file_name, extension, size_bytes, download_id, lifecycle, created_at, updated_at)
                  VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'active', ?, ?)",
                fileId,
                itemId,
                versionGroupId,
                variantGroupId,
                fileName,
                extension,
                sizeBytes,
                downloadId,
                now,
                now);
        }

    }
}
