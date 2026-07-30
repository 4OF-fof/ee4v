using Ee4v.AssetManager.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        public static IReadOnlyList<string> GetFileImportedAssetGuids(
            string fileId)
        {
            using (var connection = OpenConnection())
            {
                EnsureFileExists(connection, fileId);
                return connection.Query<ImportedAssetGuidRow>(
                        @"SELECT asset_guid
                          FROM file_imported_asset_guid
                          WHERE file_info_id = ?
                          ORDER BY asset_guid",
                        fileId)
                    .Select(row => row.asset_guid)
                    .ToArray();
            }
        }

        public static IReadOnlyList<string> GetItemImportedAssetGuids(
            string itemId)
        {
            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                return connection.Query<ImportedAssetGuidRow>(
                        @"SELECT DISTINCT imported.asset_guid
                          FROM file_imported_asset_guid imported
                          INNER JOIN file_info file
                            ON file.id = imported.file_info_id
                          LEFT JOIN version_group version
                            ON version.id = file.version_group_id
                          LEFT JOIN variant_group variant
                            ON variant.id = file.variant_group_id
                          WHERE COALESCE(
                            file.item_info_id,
                            version.item_info_id,
                            variant.item_info_id) = ?
                          ORDER BY imported.asset_guid",
                        itemId)
                    .Select(row => row.asset_guid)
                    .ToArray();
            }
        }

        public static IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations()
        {
            using (var connection = OpenConnection())
            {
                return connection.Query<ImportedAssetGuidRow>(
                        @"SELECT
                            COALESCE(
                              file.item_info_id,
                              version.item_info_id,
                              variant.item_info_id) AS item_info_id,
                            imported.file_info_id,
                            imported.asset_guid,
                            imported.imported_at
                          FROM file_imported_asset_guid imported
                          INNER JOIN file_info file
                            ON file.id = imported.file_info_id
                          LEFT JOIN version_group version
                            ON version.id = file.version_group_id
                          LEFT JOIN variant_group variant
                            ON variant.id = file.variant_group_id
                          ORDER BY imported.imported_at, imported.asset_guid")
                    .Select(row => new AssetImportedAssetAssociation
                    {
                        ItemId = row.item_info_id,
                        FileId = row.file_info_id,
                        AssetGuid = row.asset_guid,
                        ImportedAt = ParseDate(row.imported_at)
                    })
                    .ToArray();
            }
        }

        public static void ReplaceFileImportedAssetGuids(
            string fileId,
            IReadOnlyList<string> assetGuids)
        {
            using (var connection = OpenConnection())
            {
                InTransaction(connection, () =>
                {
                    EnsureFileExists(connection, fileId);
                    connection.Execute(
                        "DELETE FROM file_imported_asset_guid WHERE file_info_id = ?",
                        fileId);

                    var importedAt = Now();
                    for (var i = 0; i < assetGuids.Count; i++)
                    {
                        connection.Execute(
                            @"INSERT INTO file_imported_asset_guid(
                                file_info_id,
                                asset_guid,
                                imported_at)
                              VALUES (?, ?, ?)",
                            fileId,
                            assetGuids[i],
                            importedAt);
                    }
                });
            }
        }
    }
}
