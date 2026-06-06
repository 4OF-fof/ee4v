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
        public static AssetSyncResult SyncBlm(BlmSyncRequest request)
        {
            IReadOnlyList<BlmItemRecord> records;
            try
            {
                records = BlmConnectorApi.ReadItems(request);
            }
            catch (Exception)
            {
                RecordSyncInfoSafely("blm", AssetSyncState.Failed);
                return new AssetSyncResult(0, 0, 0, 1, AssetSyncState.Failed);
            }

            using (var connection = OpenConnection())
            {
                var created = 0;
                var updated = 0;
                var unchanged = 0;
                var error = 0;
                CleanupBlmItemDirectoryOrigins(connection);
                foreach (var record in records)
                {
                    try
                    {
                        var item = UpsertBoothSnapshot(connection, record);
                        CountStatus(item.Status, ref created, ref updated, ref unchanged, ref error);
                        var files = record.Files ?? Array.Empty<BlmFileRecord>();
                        for (var i = 0; i < files.Count; i++)
                        {
                            try
                            {
                                CountStatus(UpsertBlmFile(connection, item.ItemId, record.RegisteredItemId, files[i]), ref created, ref updated, ref unchanged, ref error);
                            }
                            catch (Exception)
                            {
                                error++;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        error++;
                    }
                }

                var state = ResolveSyncState(created, updated, unchanged, error);
                UpsertSyncInfo(connection, "blm", state);
                return new AssetSyncResult(created, updated, unchanged, error, state);
            }
        }

        public static AssetSyncResult SyncEagle(EagleSyncRequest request)
        {
            IReadOnlyList<EagleItemRecord> records;
            try
            {
                records = EagleConnectorApi.ReadItems(request);
            }
            catch (Exception)
            {
                RecordSyncInfoSafely("eagle", AssetSyncState.Failed);
                return new AssetSyncResult(0, 0, 0, 1, AssetSyncState.Failed);
            }

            using (var connection = OpenConnection())
            {
                var created = 0;
                var updated = 0;
                var unchanged = 0;
                var error = 0;
                foreach (var record in records)
                {
                    try
                    {
                        var item = record.BoothItemId.HasValue
                            ? UpsertBoothSnapshot(connection, record)
                            : UpsertPlainItem(connection, record.ItemName, record.ItemDescription);
                        CountStatus(item.Status, ref created, ref updated, ref unchanged, ref error);
                        var files = record.Files ?? Array.Empty<EagleFileRecord>();
                        for (var i = 0; i < files.Count; i++)
                        {
                            try
                            {
                                CountStatus(UpsertEagleFile(connection, item.ItemId, files[i]), ref created, ref updated, ref unchanged, ref error);
                            }
                            catch (Exception)
                            {
                                error++;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        error++;
                    }
                }

                var state = ResolveSyncState(created, updated, unchanged, error);
                UpsertSyncInfo(connection, "eagle", state);
                return new AssetSyncResult(created, updated, unchanged, error, state);
            }
        }

        private static SyncItemUpsertResult UpsertBoothSnapshot(SQLiteConnection connection, BlmItemRecord record)
        {
            return UpsertBoothSnapshot(connection, record.BoothItemId, record.Name, record.Description, record.ThumbnailUrl, record.ShopName, GetSubdomainFromUrl(record.ShopUrl), record.ShopThumbnailUrl, record.LastUpdatedAtUtc);
        }

        private static SyncItemUpsertResult UpsertBoothSnapshot(SQLiteConnection connection, EagleItemRecord record)
        {
            return UpsertBoothSnapshot(connection, record.BoothItemId.Value, record.BoothName, record.BoothDescription, record.BoothThumbnailUrl, record.ShopName, GetSubdomainFromUrl(record.ShopUrl), record.ShopThumbnailUrl, record.BoothLastUpdatedAtUtc);
        }

        private static SyncItemUpsertResult UpsertBoothSnapshot(SQLiteConnection connection, long boothItemId, string name, string description, string thumbnailUrl, string shopName, string shopSubdomain, string shopThumbnailUrl, DateTime? lastUpdatedAt)
        {
            var now = Now();
            var safeName = NormalizeDatasourceText(name);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Item";
            }

            var safeDescription = NormalizeDatasourceText(description);
            var shop = EnsureShop(connection, shopName, shopSubdomain, shopThumbnailUrl);
            var booth = connection.Query<BoothRow>("SELECT * FROM booth_info WHERE booth_item_id = ? LIMIT 1", boothItemId).FirstOrDefault();
            if (booth == null)
            {
                var itemId = NewId();
                connection.Execute("INSERT INTO item_info(id, name, description, created_at, updated_at) VALUES (?, ?, ?, ?, ?)", itemId, safeName, safeDescription, now, now);
                connection.Execute(
                    "INSERT INTO booth_info(id, item_info_id, booth_item_id, shop_info_id, name, description, thumbnail_url, last_updated_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    NewId(),
                    itemId,
                    boothItemId,
                    shop.Row.id,
                    safeName,
                    safeDescription,
                    thumbnailUrl,
                    ToDbDate(lastUpdatedAt));
                return new SyncItemUpsertResult(itemId, AssetSyncStatus.Created);
            }

            var nextLastUpdatedAt = ToDbDate(lastUpdatedAt);
            var previousBoothName = booth.name;
            var previousBoothDescription = booth.description;
            var changed = shop.Status != AssetSyncStatus.Unchanged ||
                          !StringEquals(booth.shop_info_id, shop.Row.id) ||
                          !StringEquals(booth.name, safeName) ||
                          !StringEquals(booth.description, safeDescription) ||
                          !StringEquals(booth.thumbnail_url, thumbnailUrl) ||
                          !StringEquals(booth.last_updated_at, nextLastUpdatedAt);
            if (changed)
            {
                connection.Execute(
                    "UPDATE booth_info SET shop_info_id = ?, name = ?, description = ?, thumbnail_url = ?, last_updated_at = ? WHERE id = ?",
                    shop.Row.id,
                    safeName,
                    safeDescription,
                    thumbnailUrl,
                    nextLastUpdatedAt,
                    booth.id);
            }

            changed = NormalizeExistingItemInfoText(connection, booth.item_info_id, previousBoothName, previousBoothDescription, safeName, safeDescription) || changed;
            return new SyncItemUpsertResult(booth.item_info_id, changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged);
        }

        private static SyncItemUpsertResult UpsertPlainItem(SQLiteConnection connection, string name, string description)
        {
            var safeName = NormalizeDatasourceText(name);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Item";
            }

            var safeDescription = NormalizeDatasourceText(description);
            var existing = connection.Query<ItemRow>("SELECT * FROM item_info WHERE name = ? LIMIT 1", safeName).FirstOrDefault();
            if (existing != null)
            {
                if (StringEquals(existing.description, safeDescription))
                {
                    return new SyncItemUpsertResult(existing.id, AssetSyncStatus.Unchanged);
                }

                connection.Execute("UPDATE item_info SET description = ?, updated_at = ? WHERE id = ?", safeDescription, Now(), existing.id);
                return new SyncItemUpsertResult(existing.id, AssetSyncStatus.Updated);
            }

            var now = Now();
            var id = NewId();
            connection.Execute("INSERT INTO item_info(id, name, description, created_at, updated_at) VALUES (?, ?, ?, ?, ?)", id, safeName, safeDescription, now, now);
            return new SyncItemUpsertResult(id, AssetSyncStatus.Created);
        }

        private static AssetSyncStatus UpsertBlmFile(SQLiteConnection connection, string itemId, string registeredItemId, BlmFileRecord record)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                string.IsNullOrWhiteSpace(registeredItemId) ||
                record == null ||
                string.IsNullOrWhiteSpace(record.RelativePath))
            {
                return AssetSyncStatus.Error;
            }

            var origin = connection.Query<BlmOriginRow>(
                    "SELECT * FROM blm_file_origin WHERE registered_item_id = ? AND relative_path = ?",
                    registeredItemId,
                    record.RelativePath)
                .FirstOrDefault();
            var now = Now();
            var fileName = Path.GetFileName(record.RelativePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = record.RelativePath;
            }

            var extension = Directory.Exists(record.FilePath) ? string.Empty : GetExtension(fileName);
            if (origin != null)
            {
                var changed = !StringEquals(origin.file_path_cache, record.FilePath);
                if (changed)
                {
                    connection.Execute(
                        "UPDATE blm_file_origin SET file_path_cache = ?, imported_at = ? WHERE registered_item_id = ? AND relative_path = ?",
                        record.FilePath,
                        now,
                        registeredItemId,
                        record.RelativePath);
                }

                changed = UpdateFileInfoSnapshot(connection, origin.file_info_id, fileName, extension, record.SizeBytes, null, now) || changed;
                return changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged;
            }

            var fileId = CreateFileInfo(connection, itemId, fileName, extension, record.SizeBytes, null, now);
            connection.Execute(
                "INSERT INTO blm_file_origin(file_info_id, registered_item_id, relative_path, file_path_cache, imported_at) VALUES (?, ?, ?, ?, ?)",
                fileId,
                registeredItemId,
                record.RelativePath,
                record.FilePath,
                now);
            return AssetSyncStatus.Created;
        }

        private static void CleanupBlmItemDirectoryOrigins(SQLiteConnection connection)
        {
            var origins = connection.Query<BlmOriginRow>("SELECT * FROM blm_file_origin WHERE relative_path = ''");
            for (var i = 0; i < origins.Count; i++)
            {
                connection.Execute("DELETE FROM blm_file_origin WHERE file_info_id = ?", origins[i].file_info_id);
                if (OriginCount(connection, origins[i].file_info_id) == 0)
                {
                    connection.Execute("DELETE FROM file_dependency WHERE dependent_file_info_id = ? OR dependency_file_info_id = ?", origins[i].file_info_id, origins[i].file_info_id);
                    connection.Execute("DELETE FROM file_info WHERE id = ?", origins[i].file_info_id);
                }
            }
        }

        private static AssetSyncStatus UpsertEagleFile(SQLiteConnection connection, string itemId, EagleFileRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(itemId))
            {
                return AssetSyncStatus.Error;
            }

            var now = Now();
            var hasEagleOrigin = !string.IsNullOrWhiteSpace(record.EagleItemId);
            var origin = hasEagleOrigin
                ? connection.Query<EagleOriginRow>("SELECT * FROM eagle_file_origin WHERE eagle_item_id = ?", record.EagleItemId).FirstOrDefault()
                : null;
            if (origin != null)
            {
                var isDeleted = record.IsDeleted ? 1 : 0;
                var changed = !StringEquals(origin.file_path_cache, record.FilePath) ||
                              origin.is_deleted.GetValueOrDefault() != isDeleted;
                if (changed)
                {
                    connection.Execute("UPDATE eagle_file_origin SET file_path_cache = ?, is_deleted = ?, imported_at = ? WHERE eagle_item_id = ?", record.FilePath, isDeleted, now, record.EagleItemId);
                }

                changed = UpdateFileInfoSnapshot(connection, origin.file_info_id, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now) || changed;
                return changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged;
            }

            var fileId = GetFileInfoIdByDownloadId(connection, record.DownloadId);
            var status = AssetSyncStatus.Created;
            if (fileId == null)
            {
                fileId = CreateFileInfo(connection, itemId, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now);
            }
            else
            {
                status = UpdateFileInfoSnapshot(connection, fileId, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now)
                    ? AssetSyncStatus.Updated
                    : AssetSyncStatus.Unchanged;
            }

            if (!hasEagleOrigin)
            {
                if (!record.DownloadId.HasValue)
                {
                    return AssetSyncStatus.Error;
                }

                var downloadOnlyFileId = GetFileInfoIdByDownloadId(connection, record.DownloadId);
                if (downloadOnlyFileId == null)
                {
                    CreateFileInfo(connection, itemId, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now);
                    return AssetSyncStatus.Created;
                }

                return UpdateFileInfoSnapshot(connection, downloadOnlyFileId, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now)
                    ? AssetSyncStatus.Updated
                    : AssetSyncStatus.Unchanged;
            }

            var existingFileOrigin = connection.Query<EagleOriginRow>("SELECT * FROM eagle_file_origin WHERE file_info_id = ?", fileId).FirstOrDefault();
            if (existingFileOrigin != null)
            {
                var isDeleted = record.IsDeleted ? 1 : 0;
                var changed = !StringEquals(existingFileOrigin.eagle_item_id, record.EagleItemId) ||
                              !StringEquals(existingFileOrigin.file_path_cache, record.FilePath) ||
                              existingFileOrigin.is_deleted.GetValueOrDefault() != isDeleted;
                if (changed)
                {
                    connection.Execute(
                        "UPDATE eagle_file_origin SET eagle_item_id = ?, file_path_cache = ?, is_deleted = ?, imported_at = ? WHERE file_info_id = ?",
                        record.EagleItemId,
                        record.FilePath,
                        isDeleted,
                        now,
                        fileId);
                }

                return MergeStatus(status, changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged);
            }

            connection.Execute(
                "INSERT INTO eagle_file_origin(file_info_id, eagle_item_id, file_path_cache, is_deleted, imported_at) VALUES (?, ?, ?, ?, ?)",
                fileId,
                record.EagleItemId,
                record.FilePath,
                record.IsDeleted ? 1 : 0,
                now);
            return status == AssetSyncStatus.Unchanged ? AssetSyncStatus.Updated : status;
        }

        private static string GetFileInfoIdByDownloadId(SQLiteConnection connection, long? downloadId)
        {
            return downloadId.HasValue
                ? connection.ExecuteScalar<string>("SELECT id FROM file_info WHERE download_id = ? LIMIT 1", downloadId.Value)
                : null;
        }

        private static bool UpdateFileInfoSnapshot(SQLiteConnection connection, string fileId, string fileName, string extension, long? sizeBytes, long? downloadId, string now)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return false;
            }

            var safeFileName = NormalizeDatasourceText(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                safeFileName = "file";
            }

            var row = connection.Query<FileRow>("SELECT * FROM file_info WHERE id = ? LIMIT 1", fileId).FirstOrDefault();
            if (row == null)
            {
                return false;
            }

            var nextSizeBytes = sizeBytes ?? row.size_bytes;
            var nextDownloadId = downloadId ?? row.download_id;
            var changed = !StringEquals(row.file_name, safeFileName) ||
                          !StringEquals(row.extension, extension) ||
                          row.size_bytes != nextSizeBytes ||
                          row.download_id != nextDownloadId;
            if (changed)
            {
                connection.Execute(
                    @"UPDATE file_info
                      SET file_name = ?, extension = ?, size_bytes = ?, download_id = ?, updated_at = ?
                      WHERE id = ?",
                    safeFileName,
                    extension,
                    nextSizeBytes,
                    nextDownloadId,
                    now,
                    fileId);
            }

            return changed;
        }

        private static string CreateFileInfo(SQLiteConnection connection, string itemId, string fileName, string extension, long? sizeBytes, long? downloadId, string now)
        {
            var safeFileName = NormalizeDatasourceText(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                safeFileName = "file";
            }

            var fileId = NewId();
            connection.Execute(
                @"INSERT INTO file_info(id, item_info_id, file_name, extension, size_bytes, download_id, is_primary, lifecycle, created_at, updated_at)
                  VALUES (?, ?, ?, ?, ?, ?, ?, 'active', ?, ?)",
                fileId,
                itemId,
                safeFileName,
                extension,
                sizeBytes,
                downloadId,
                HasPrimaryFile(connection, itemId) ? 0 : 1,
                now,
                now);
            return fileId;
        }

        private static bool NormalizeExistingItemInfoText(SQLiteConnection connection, string itemId, string previousSourceName, string previousSourceDescription, string nextSourceName, string nextSourceDescription)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            var item = connection.Query<ItemRow>("SELECT * FROM item_info WHERE id = ? LIMIT 1", itemId).FirstOrDefault();
            if (item == null)
            {
                return false;
            }

            var normalizedName = NormalizeDatasourceText(item.name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "Item";
            }

            var normalizedDescription = NormalizeDatasourceText(item.description);
            if (StringEquals(item.name, previousSourceName))
            {
                normalizedName = nextSourceName;
            }

            if (StringEquals(item.description, previousSourceDescription))
            {
                normalizedDescription = nextSourceDescription;
            }

            if (StringEquals(item.name, normalizedName) &&
                StringEquals(item.description, normalizedDescription))
            {
                return false;
            }

            connection.Execute(
                "UPDATE item_info SET name = ?, description = ?, updated_at = ? WHERE id = ?",
                normalizedName,
                normalizedDescription,
                Now(),
                itemId);
            return true;
        }

        private static ShopUpsertResult EnsureShop(SQLiteConnection connection, string shopName, string subdomain, string thumbnailUrl)
        {
            var safeSubdomain = string.IsNullOrWhiteSpace(subdomain) ? "unknown-" + NewId() : subdomain;
            var safeShopName = NormalizeDatasourceText(shopName);
            var existing = connection.Query<ShopRow>("SELECT * FROM shop_info WHERE subdomain = ? LIMIT 1", safeSubdomain).FirstOrDefault();
            if (existing != null)
            {
                var nextName = safeShopName;
                var changed = !StringEquals(existing.name, nextName) ||
                              !StringEquals(existing.thumbnail_url, thumbnailUrl);
                if (changed)
                {
                    connection.Execute("UPDATE shop_info SET name = ?, thumbnail_url = ? WHERE id = ?", nextName, thumbnailUrl, existing.id);
                    existing.name = nextName;
                    existing.thumbnail_url = thumbnailUrl;
                }

                return new ShopUpsertResult(existing, changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged);
            }

            var id = NewId();
            connection.Execute("INSERT INTO shop_info(id, name, subdomain, thumbnail_url) VALUES (?, ?, ?, ?)", id, safeShopName, safeSubdomain, thumbnailUrl);
            return new ShopUpsertResult(connection.Query<ShopRow>("SELECT * FROM shop_info WHERE id = ?", id).First(), AssetSyncStatus.Created);
        }

        private sealed class SyncItemUpsertResult
        {
            public SyncItemUpsertResult(string itemId, AssetSyncStatus status)
            {
                ItemId = itemId;
                Status = status;
            }

            public string ItemId { get; private set; }

            public AssetSyncStatus Status { get; private set; }
        }

        private sealed class ShopUpsertResult
        {
            public ShopUpsertResult(ShopRow row, AssetSyncStatus status)
            {
                Row = row;
                Status = status;
            }

            public ShopRow Row { get; private set; }

            public AssetSyncStatus Status { get; private set; }
        }
    }
}
