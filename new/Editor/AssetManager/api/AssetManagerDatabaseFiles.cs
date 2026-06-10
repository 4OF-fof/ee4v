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
                var where = new List<string> { "item_info_id = ?" };
                var parameters = new List<object> { itemId };

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

                connection.Execute(
                    @"INSERT INTO file_info(id, item_info_id, file_name, extension, size_bytes, download_id, lifecycle, created_at, updated_at)
                      VALUES (?, ?, ?, ?, ?, NULL, 'active', ?, ?)",
                    fileId,
                    itemId,
                    fileName,
                    GetExtension(fileName),
                    request.SizeBytes,
                    now,
                    now);
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
                return connection.Query<FileDependencyRow>(
                        "SELECT * FROM file_dependency WHERE dependent_file_info_id = ? ORDER BY dependency_file_info_id",
                        fileId)
                    .Select(row => new AssetFileDependency
                    {
                        DependentFileId = row.dependent_file_info_id,
                        DependencyFileId = row.dependency_file_info_id
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

                connection.Execute("DELETE FROM file_dependency WHERE dependent_file_info_id = ?", dependentFileId);
                var now = Now();
                for (var i = 0; i < ids.Count; i++)
                {
                    connection.Execute(
                        "INSERT OR IGNORE INTO file_dependency(dependent_file_info_id, dependency_file_info_id, dependency_type, created_at) VALUES (?, ?, 'requires', ?)",
                        dependentFileId,
                        ids[i],
                        now);
                }
            }
        }

        private static string ItemHasSourceClause(AssetSourceType sourceType)
        {
            return "item_info.id IN (SELECT file_info.item_info_id FROM file_info WHERE " + FileHasSourceClause(sourceType) + ")";
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
    }
}
