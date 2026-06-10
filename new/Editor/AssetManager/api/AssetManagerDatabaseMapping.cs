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
        private static AssetItem ToAssetItem(SQLiteConnection connection, ItemRow row)
        {
            return new AssetItem
            {
                Id = row.id,
                Name = row.name,
                Description = row.description,
                Booth = LoadBoothSnapshot(connection, row.id),
                Tags = connection.Query<TagRow>(
                        @"SELECT tag_info.*
                          FROM tag_info
                          INNER JOIN item_tag ON item_tag.tag_info_id = tag_info.id
                          WHERE item_tag.item_info_id = ?
                          ORDER BY tag_info.name COLLATE NOCASE",
                        row.id)
                    .Select(ToAssetTag)
                    .ToArray(),
                Files = connection.Query<FileRow>("SELECT * FROM file_info WHERE item_info_id = ? ORDER BY file_name COLLATE NOCASE, id", row.id)
                    .Select(ToAssetFileSummary)
                    .ToArray(),
                CreatedAt = ParseDate(row.created_at),
                UpdatedAt = ParseDate(row.updated_at)
            };
        }

        private static BoothSnapshot LoadBoothSnapshot(SQLiteConnection connection, string itemId)
        {
            var row = connection.Query<BoothRow>(
                @"SELECT booth_info.*, shop_info.name AS shop_name, shop_info.subdomain AS shop_subdomain, shop_info.thumbnail_url AS shop_thumbnail_url
                  FROM booth_info
                  INNER JOIN shop_info ON shop_info.id = booth_info.shop_info_id
                  WHERE booth_info.item_info_id = ?
                  LIMIT 1",
                itemId).FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            return new BoothSnapshot
            {
                Id = row.id,
                BoothItemId = row.booth_item_id,
                ItemUrl = string.IsNullOrWhiteSpace(row.shop_subdomain) ? string.Empty : "https://" + row.shop_subdomain + ".booth.pm/items/" + row.booth_item_id,
                Name = row.name,
                Description = row.description,
                ThumbnailUrl = row.thumbnail_url,
                ShopName = row.shop_name,
                ShopUrl = string.IsNullOrWhiteSpace(row.shop_subdomain) ? string.Empty : "https://" + row.shop_subdomain + ".booth.pm",
                ShopThumbnailUrl = row.shop_thumbnail_url,
                LastUpdatedAt = string.IsNullOrWhiteSpace(row.last_updated_at) ? (DateTime?)null : ParseDate(row.last_updated_at)
            };
        }

        private static AssetFile ToAssetFile(SQLiteConnection connection, FileRow row)
        {
            return new AssetFile
            {
                Id = row.id,
                ItemId = row.item_info_id,
                FileName = row.file_name,
                Extension = row.extension,
                SizeBytes = row.size_bytes,
                DownloadId = row.download_id,
                Lifecycle = FromDbLifecycle(row.lifecycle),
                Origins = LoadOrigins(connection, row.id),
                CreatedAt = ParseDate(row.created_at),
                UpdatedAt = ParseDate(row.updated_at)
            };
        }

        private static AssetFileSummary ToAssetFileSummary(FileRow row)
        {
            return new AssetFileSummary
            {
                Id = row.id,
                FileName = row.file_name,
                Extension = row.extension,
                SizeBytes = row.size_bytes,
                DownloadId = row.download_id,
                Lifecycle = FromDbLifecycle(row.lifecycle)
            };
        }

        private static AssetFileImportTarget ToAssetFileImportTarget(FileImportTargetRow row)
        {
            return new AssetFileImportTarget
            {
                Id = row.id,
                FileId = row.file_info_id,
                RelativePath = row.relative_path,
                IsDirectory = row.is_directory != 0,
                CreatedAt = ParseDate(row.created_at),
                UpdatedAt = ParseDate(row.updated_at)
            };
        }

        private static IReadOnlyList<AssetFileOrigin> LoadOrigins(SQLiteConnection connection, string fileId)
        {
            var origins = new List<AssetFileOrigin>();
            var ee4v = connection.Query<Ee4vOriginRow>("SELECT * FROM ee4v_file_origin WHERE file_info_id = ?", fileId).FirstOrDefault();
            if (ee4v != null)
            {
                origins.Add(new AssetFileOrigin { SourceType = AssetSourceType.Ee4v, SourceId = ee4v.ee4v_file_id, FilePathCache = ee4v.file_path_cache, ImportedAt = ParseNullableDate(ee4v.imported_at) });
            }

            var eagle = connection.Query<EagleOriginRow>("SELECT * FROM eagle_file_origin WHERE file_info_id = ?", fileId).FirstOrDefault();
            if (eagle != null)
            {
                origins.Add(new AssetFileOrigin { SourceType = AssetSourceType.Eagle, SourceId = eagle.eagle_item_id, FilePathCache = eagle.file_path_cache, ImportedAt = ParseNullableDate(eagle.imported_at) });
            }

            var blm = connection.Query<BlmOriginRow>("SELECT * FROM blm_file_origin WHERE file_info_id = ?", fileId).FirstOrDefault();
            if (blm != null)
            {
                origins.Add(new AssetFileOrigin { SourceType = AssetSourceType.Blm, SourceId = blm.registered_item_id, FilePathCache = blm.file_path_cache, ImportedAt = ParseNullableDate(blm.imported_at) });
            }

            return origins;
        }

        private static AssetTag ToAssetTag(TagRow row)
        {
            return new AssetTag { Id = row.id, Name = row.name, CreatedAt = ParseDate(row.created_at), UpdatedAt = ParseDate(row.updated_at) };
        }

        private static AssetCollection ToAssetCollection(SQLiteConnection connection, CollectionRow row)
        {
            var smart = connection.Query<SmartCollectionRow>("SELECT * FROM smart_collection_info WHERE collection_info_id = ?", row.id).FirstOrDefault();
            var parent = connection.Query<CollectionCollectionRow>("SELECT * FROM collection_collection WHERE child_collection_id = ? LIMIT 1", row.id).FirstOrDefault();
            return new AssetCollection
            {
                Id = row.id,
                Name = row.name,
                IsSmartCollection = smart != null,
                ParentCollectionId = parent != null ? parent.parent_collection_id : null,
                SmartRule = smart == null ? null : new SmartCollectionRule
                {
                    MatchMode = smart.match_mode == "any" ? SmartCollectionMatchMode.Any : SmartCollectionMatchMode.All,
                    Conditions = connection.Query<SmartConditionRow>("SELECT * FROM smart_collection_condition WHERE collection_info_id = ? ORDER BY id", row.id)
                        .Select(ToSmartCondition)
                        .ToArray()
                },
                CreatedAt = ParseDate(row.created_at),
                UpdatedAt = ParseDate(row.updated_at)
            };
        }

        private static SmartCollectionCondition ToSmartCondition(SmartConditionRow row)
        {
            return new SmartCollectionCondition
            {
                Id = row.id,
                Field = FromDbSmartField(row.field),
                Operator = FromDbSmartOperator(row.@operator),
                QueryText = row.query_text
            };
        }
    }
}
