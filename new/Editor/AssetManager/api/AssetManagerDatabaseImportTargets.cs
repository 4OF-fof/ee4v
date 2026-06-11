using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.AssetManager.Api
{
    internal static partial class AssetManagerDatabase
    {
        public static IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId)
        {
            using (var connection = OpenConnection())
            {
                EnsureFileExists(connection, fileId);
                return connection.Query<FileImportTargetRow>(
                        "SELECT * FROM file_import_target WHERE file_info_id = ? ORDER BY relative_path COLLATE NOCASE, id",
                        fileId)
                    .Select(ToAssetFileImportTarget)
                    .ToArray();
            }
        }

        public static void SetFileImportTargets(string fileId, IReadOnlyList<AssetFileImportTargetRequest> targets)
        {
            using (var connection = OpenConnection())
            {
                EnsureFileExists(connection, fileId);
                var normalizedTargets = NormalizeImportTargets(targets);
                connection.Execute("DELETE FROM file_import_target WHERE file_info_id = ?", fileId);

                var now = Now();
                for (var i = 0; i < normalizedTargets.Count; i++)
                {
                    connection.Execute(
                        @"INSERT INTO file_import_target(id, file_info_id, relative_path, created_at, updated_at)
                          VALUES (?, ?, ?, ?, ?)",
                        NewId(),
                        fileId,
                        normalizedTargets[i].RelativePath,
                        now,
                        now);
                }
            }
        }

        private static IReadOnlyList<AssetFileImportTargetRequest> NormalizeImportTargets(IReadOnlyList<AssetFileImportTargetRequest> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return Array.Empty<AssetFileImportTargetRequest>();
            }

            var results = new List<AssetFileImportTargetRequest>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                var relativePath = NormalizeImportTargetPath(target.RelativePath);
                if (!seen.Add(relativePath))
                {
                    continue;
                }

                results.Add(new AssetFileImportTargetRequest
                {
                    RelativePath = relativePath
                });
            }

            return results;
        }

        private static string NormalizeImportTargetPath(string relativePath)
        {
            var normalized = (relativePath ?? string.Empty)
                .Replace('\\', '/')
                .Trim();

            while (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            if (normalized.IndexOf('\0') >= 0 ||
                normalized == "." ||
                normalized == ".." ||
                normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.EndsWith("/..", StringComparison.Ordinal) ||
                normalized.IndexOf("/../", StringComparison.Ordinal) >= 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Import target path must be relative to the file.");
            }

            return normalized.TrimEnd('/');
        }
    }
}
