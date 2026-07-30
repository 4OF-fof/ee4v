using Ee4v.AssetManager.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        public static IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId)
        {
            using (var connection = OpenConnection())
            {
                EnsureFileExists(connection, fileId);
                return connection.Query<FileImportTargetRow>(
                        "SELECT * FROM file_import_target WHERE file_info_id = ? ORDER BY relative_path COLLATE NOCASE",
                        fileId)
                    .Select(ToAssetFileImportTarget)
                    .ToArray();
            }
        }

        public static void ReplaceFileImportTargets(
            string fileId,
            IReadOnlyList<string> normalizedRelativePaths)
        {
            using (var connection = OpenConnection())
            {
                InTransaction(connection, () =>
                {
                    EnsureFileExists(connection, fileId);
                    connection.Execute("DELETE FROM file_import_target WHERE file_info_id = ?", fileId);

                    for (var i = 0; i < normalizedRelativePaths.Count; i++)
                    {
                        connection.Execute(
                            @"INSERT INTO file_import_target(file_info_id, relative_path)
                              VALUES (?, ?)",
                            fileId,
                            normalizedRelativePaths[i]);
                    }
                });
            }
        }
    }
}
