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

            return SyncBlm(records, AssetSyncFingerprint.CreateBlm(records), false);
        }

        private static AssetSyncResult SyncBlm(IReadOnlyList<BlmItemRecord> records, string fingerprint, bool overwriteItemText)
        {
            using (var connection = OpenConnection())
            {
                var created = 0;
                var updated = 0;
                var unchanged = 0;
                var error = 0;
                var seenItemSourceIds = new HashSet<string>(
                    records.Where(record => !string.IsNullOrWhiteSpace(record.RegisteredItemId)).Select(record => record.RegisteredItemId),
                    StringComparer.Ordinal);
                var seenFileSourceIds = BuildBlmFileSourceKeys(records);
                var fileSnapshotComplete = records.All(record => record.FileSnapshotComplete);
                InTransaction(connection, () => CleanupBlmItemDirectoryOrigins(connection));
                foreach (var record in records)
                {
                    try
                    {
                        var item = InTransaction(connection, () =>
                        {
                            var result = UpsertBoothSnapshot(connection, record, overwriteItemText);
                            var originStatus = UpsertItemSourceOrigin(
                                connection,
                                "blm",
                                record.RegisteredItemId,
                                result.ItemId,
                                record.Name,
                                record.Description,
                                record.Tags);
                            result.Status = MergeStatus(result.Status, originStatus);
                            return result;
                        });
                        CountStatus(item.Status, ref created, ref updated, ref unchanged, ref error);
                        var files = record.Files ?? Array.Empty<BlmFileRecord>();
                        for (var i = 0; i < files.Count; i++)
                        {
                            try
                            {
                                var fileStatus = InTransaction(
                                    connection,
                                    () => UpsertBlmFile(connection, item.ItemId, record.RegisteredItemId, files[i]));
                                CountStatus(fileStatus, ref created, ref updated, ref unchanged, ref error);
                            }
                            catch (Exception)
                            {
                                error++;
                            }
                        }

                        InTransaction(connection, () => ReconcileImportedFileGroups(connection, item.ItemId, Now()));
                    }
                    catch (Exception)
                    {
                        error++;
                    }
                }

                InTransaction(connection, () =>
                {
                    ReconcileItemSourceOrigins(connection, "blm", seenItemSourceIds);
                    if (fileSnapshotComplete)
                    {
                        ReconcileBlmFileOrigins(connection, seenFileSourceIds);
                    }
                });

                var state = ResolveSyncState(created, updated, unchanged, error);
                UpsertSyncInfo(connection, "blm", state);
                if (state == AssetSyncState.Success)
                {
                    AssetSyncFingerprintCache.Save(AssetSourceType.Blm, fingerprint);
                }

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

            return SyncEagle(records, AssetSyncFingerprint.CreateEagle(records), false);
        }

        private static AssetSyncResult SyncEagle(IReadOnlyList<EagleItemRecord> records, string fingerprint, bool overwriteItemText)
        {
            using (var connection = OpenConnection())
            {
                var created = 0;
                var updated = 0;
                var unchanged = 0;
                var error = 0;
                var seenItemSourceIds = new HashSet<string>(
                    records.Where(record => !string.IsNullOrWhiteSpace(record.EagleItemId)).Select(record => record.EagleItemId),
                    StringComparer.Ordinal);
                var seenFileSourceIds = new HashSet<string>(
                    records.SelectMany(record => record.Files ?? Array.Empty<EagleFileRecord>())
                        .Where(file => !string.IsNullOrWhiteSpace(file.EagleItemId))
                        .Select(file => file.EagleItemId),
                    StringComparer.Ordinal);
                var seenDownloadIds = new HashSet<long>(
                    records.SelectMany(record => record.Files ?? Array.Empty<EagleFileRecord>())
                        .Where(file => file.DownloadId.HasValue)
                        .Select(file => file.DownloadId.Value));
                foreach (var record in records)
                {
                    try
                    {
                        var item = InTransaction(connection, () =>
                        {
                            var result = record.BoothItemId.HasValue
                                ? UpsertBoothSnapshot(connection, record, overwriteItemText)
                                : UpsertPlainItem(connection, "eagle", record.EagleItemId, record.ItemName, record.ItemDescription, overwriteItemText);
                            var originStatus = UpsertItemSourceOrigin(
                                connection,
                                "eagle",
                                record.EagleItemId,
                                result.ItemId,
                                record.ItemName,
                                record.ItemDescription,
                                record.Tags);
                            result.Status = MergeStatus(result.Status, originStatus);
                            return result;
                        });
                        CountStatus(item.Status, ref created, ref updated, ref unchanged, ref error);
                        var files = record.Files ?? Array.Empty<EagleFileRecord>();
                        for (var i = 0; i < files.Count; i++)
                        {
                            try
                            {
                                var fileStatus = InTransaction(connection, () => UpsertEagleFile(connection, item.ItemId, files[i]));
                                CountStatus(fileStatus, ref created, ref updated, ref unchanged, ref error);
                            }
                            catch (Exception)
                            {
                                error++;
                            }
                        }

                        InTransaction(connection, () => ReconcileImportedFileGroups(connection, item.ItemId, Now()));
                    }
                    catch (Exception)
                    {
                        error++;
                    }
                }

                InTransaction(connection, () =>
                {
                    ReconcileItemSourceOrigins(connection, "eagle", seenItemSourceIds);
                    ReconcileEagleFileOrigins(connection, seenFileSourceIds);
                    ReconcileEagleDownloadOnlyFiles(connection, seenDownloadIds);
                });

                var state = ResolveSyncState(created, updated, unchanged, error);
                UpsertSyncInfo(connection, "eagle", state);
                if (state == AssetSyncState.Success)
                {
                    AssetSyncFingerprintCache.Save(AssetSourceType.Eagle, fingerprint);
                }

                return new AssetSyncResult(created, updated, unchanged, error, state);
            }
        }

        private static SyncItemUpsertResult UpsertBoothSnapshot(SQLiteConnection connection, BlmItemRecord record, bool overwriteItemText)
        {
            return UpsertBoothSnapshot(connection, record.BoothItemId, record.Name, record.Description, record.ThumbnailUrl, record.ShopName, GetSubdomainFromUrl(record.ShopUrl), record.ShopThumbnailUrl, record.LastUpdatedAtUtc, overwriteItemText);
        }

        private static SyncItemUpsertResult UpsertBoothSnapshot(SQLiteConnection connection, EagleItemRecord record, bool overwriteItemText)
        {
            return UpsertBoothSnapshot(connection, record.BoothItemId.Value, record.BoothName, record.BoothDescription, record.BoothThumbnailUrl, record.ShopName, GetSubdomainFromUrl(record.ShopUrl), record.ShopThumbnailUrl, record.BoothLastUpdatedAtUtc, overwriteItemText);
        }

        private static SyncItemUpsertResult UpsertBoothSnapshot(SQLiteConnection connection, long boothItemId, string name, string description, string thumbnailUrl, string shopName, string shopSubdomain, string shopThumbnailUrl, DateTime? lastUpdatedAt, bool overwriteItemText)
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

            changed = NormalizeExistingItemInfoText(connection, booth.item_info_id, previousBoothName, previousBoothDescription, safeName, safeDescription, overwriteItemText) || changed;
            return new SyncItemUpsertResult(booth.item_info_id, changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged);
        }

        private static SyncItemUpsertResult UpsertPlainItem(SQLiteConnection connection, string sourceType, string sourceId, string name, string description, bool overwriteItemText)
        {
            var safeName = NormalizeDatasourceText(name);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Item";
            }

            var safeDescription = NormalizeDatasourceText(description);
            var origin = connection.Query<ItemSourceOriginRow>(
                "SELECT * FROM item_source_origin WHERE source_type = ? AND source_id = ? LIMIT 1",
                sourceType,
                sourceId).FirstOrDefault();
            if (origin != null)
            {
                var existing = connection.Query<ItemRow>("SELECT * FROM item_info WHERE id = ? LIMIT 1", origin.item_info_id).First();
                var nextName = overwriteItemText || StringEquals(existing.name, origin.source_name) ? safeName : existing.name;
                var nextDescription = overwriteItemText || StringEquals(existing.description, origin.source_description) ? safeDescription : existing.description;
                var changed = !StringEquals(existing.name, nextName) ||
                              !StringEquals(existing.description, nextDescription) ||
                              existing.is_available == 0;
                if (!changed)
                {
                    return new SyncItemUpsertResult(existing.id, AssetSyncStatus.Unchanged);
                }

                connection.Execute(
                    "UPDATE item_info SET name = ?, description = ?, is_available = 1, updated_at = ? WHERE id = ?",
                    nextName,
                    nextDescription,
                    Now(),
                    existing.id);
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
                var changed = !StringEquals(origin.file_path_cache, record.FilePath) || origin.is_missing != 0;
                if (changed)
                {
                    connection.Execute(
                        "UPDATE blm_file_origin SET file_path_cache = ?, is_missing = 0, imported_at = ? WHERE registered_item_id = ? AND relative_path = ?",
                        record.FilePath,
                        now,
                        registeredItemId,
                        record.RelativePath);
                }

                var parent = ResolveImportedFileParent(connection, itemId, fileName, origin.file_info_id);
                changed = UpdateFileInfoSnapshot(connection, origin.file_info_id, fileName, extension, record.SizeBytes, null, now) || changed;
                changed = UpdateFileInfoParentSnapshot(connection, origin.file_info_id, parent, now) || changed;
                EnsureVersionGroupPrimaryIfMissing(connection, parent.VersionGroupId, origin.file_info_id, now);
                RefreshFileAvailability(connection, origin.file_info_id);
                return changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged;
            }

            var fileId = CreateFileInfo(connection, itemId, fileName, extension, record.SizeBytes, null, now);
            connection.Execute(
                "INSERT INTO blm_file_origin(file_info_id, registered_item_id, relative_path, file_path_cache, is_missing, imported_at) VALUES (?, ?, ?, ?, 0, ?)",
                fileId,
                registeredItemId,
                record.RelativePath,
                record.FilePath,
                now);
            RefreshFileAvailability(connection, fileId);
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
                    connection.Execute("DELETE FROM dependency WHERE source_file_info_id = ? OR target_file_info_id = ?", origins[i].file_info_id, origins[i].file_info_id);
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

                var parent = ResolveImportedFileParent(connection, itemId, record.Name, origin.file_info_id);
                changed = UpdateFileInfoSnapshot(connection, origin.file_info_id, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now) || changed;
                changed = UpdateFileInfoParentSnapshot(connection, origin.file_info_id, parent, now) || changed;
                EnsureVersionGroupPrimaryIfMissing(connection, parent.VersionGroupId, origin.file_info_id, now);
                RefreshFileAvailability(connection, origin.file_info_id);
                return changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged;
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

                var parent = ResolveImportedFileParent(connection, itemId, record.Name, downloadOnlyFileId);
                var wasUnavailable = connection.ExecuteScalar<int>("SELECT is_available FROM file_info WHERE id = ?", downloadOnlyFileId) == 0;
                var changed = UpdateFileInfoSnapshot(connection, downloadOnlyFileId, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now) || wasUnavailable;
                changed = UpdateFileInfoParentSnapshot(connection, downloadOnlyFileId, parent, now) || changed;
                EnsureVersionGroupPrimaryIfMissing(connection, parent.VersionGroupId, downloadOnlyFileId, now);
                connection.Execute("UPDATE file_info SET is_available = 1 WHERE id = ?", downloadOnlyFileId);
                return changed
                    ? AssetSyncStatus.Updated
                    : AssetSyncStatus.Unchanged;
            }

            var fileId = GetFileInfoIdByDownloadId(connection, record.DownloadId);
            var status = AssetSyncStatus.Created;
            if (fileId == null)
            {
                fileId = CreateFileInfo(connection, itemId, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now);
            }
            else
            {
                var parent = ResolveImportedFileParent(connection, itemId, record.Name, fileId);
                var changed = UpdateFileInfoSnapshot(connection, fileId, record.Name, record.Extension, record.SizeBytes, record.DownloadId, now);
                changed = UpdateFileInfoParentSnapshot(connection, fileId, parent, now) || changed;
                EnsureVersionGroupPrimaryIfMissing(connection, parent.VersionGroupId, fileId, now);
                status = changed
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

                RefreshFileAvailability(connection, fileId);
                return MergeStatus(status, changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged);
            }

            connection.Execute(
                "INSERT INTO eagle_file_origin(file_info_id, eagle_item_id, file_path_cache, is_deleted, imported_at) VALUES (?, ?, ?, ?, ?)",
                fileId,
                record.EagleItemId,
                record.FilePath,
                record.IsDeleted ? 1 : 0,
                now);
            RefreshFileAvailability(connection, fileId);
            return status == AssetSyncStatus.Unchanged ? AssetSyncStatus.Updated : status;
        }

        private static AssetSyncStatus UpsertItemSourceOrigin(
            SQLiteConnection connection,
            string sourceType,
            string sourceId,
            string itemId,
            string sourceName,
            string sourceDescription,
            IReadOnlyList<string> tags)
        {
            if (string.IsNullOrWhiteSpace(sourceType) ||
                string.IsNullOrWhiteSpace(sourceId) ||
                string.IsNullOrWhiteSpace(itemId))
            {
                return AssetSyncStatus.Error;
            }

            var now = Now();
            var safeName = NormalizeDatasourceText(sourceName);
            var safeDescription = NormalizeDatasourceText(sourceDescription);
            var existing = connection.Query<ItemSourceOriginRow>(
                "SELECT * FROM item_source_origin WHERE source_type = ? AND source_id = ? LIMIT 1",
                sourceType,
                sourceId).FirstOrDefault();
            var changed = existing == null ||
                          !StringEquals(existing.item_info_id, itemId) ||
                          !StringEquals(existing.source_name, safeName) ||
                          !StringEquals(existing.source_description, safeDescription) ||
                          existing.is_missing != 0;
            if (existing == null)
            {
                connection.Execute(
                    @"INSERT INTO item_source_origin(
                        source_type, source_id, item_info_id, source_name, source_description, is_missing, imported_at)
                      VALUES (?, ?, ?, ?, ?, 0, ?)",
                    sourceType,
                    sourceId,
                    itemId,
                    safeName,
                    safeDescription,
                    now);
            }
            else if (changed)
            {
                connection.Execute(
                    @"UPDATE item_source_origin
                      SET item_info_id = ?, source_name = ?, source_description = ?, is_missing = 0, imported_at = ?
                      WHERE source_type = ? AND source_id = ?",
                    itemId,
                    safeName,
                    safeDescription,
                    now,
                    sourceType,
                    sourceId);
            }

            changed = SyncDatasourceTags(connection, sourceType, sourceId, itemId, tags) || changed;
            connection.Execute("UPDATE item_info SET is_available = 1 WHERE id = ?", itemId);
            if (existing != null && !StringEquals(existing.item_info_id, itemId))
            {
                ArchiveOrphanedManagedItem(connection, existing.item_info_id);
            }

            return existing == null
                ? AssetSyncStatus.Created
                : changed ? AssetSyncStatus.Updated : AssetSyncStatus.Unchanged;
        }

        private static bool SyncDatasourceTags(
            SQLiteConnection connection,
            string sourceType,
            string sourceId,
            string itemId,
            IReadOnlyList<string> tags)
        {
            var normalizedTags = (tags ?? Array.Empty<string>())
                .Select(NormalizeDatasourceText)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var existingTags = connection.Query<DatasourceTagRow>(
                    "SELECT * FROM datasource_tag WHERE source_type = ? AND source_id = ? ORDER BY name COLLATE NOCASE",
                    sourceType,
                    sourceId);
            if (existingTags.All(tag => StringEquals(tag.item_info_id, itemId)) &&
                existingTags.Select(tag => tag.name).SequenceEqual(normalizedTags, StringComparer.Ordinal))
            {
                return false;
            }

            connection.Execute(
                "DELETE FROM datasource_tag WHERE source_type = ? AND source_id = ?",
                sourceType,
                sourceId);
            for (var i = 0; i < normalizedTags.Length; i++)
            {
                connection.Execute(
                    "INSERT INTO datasource_tag(source_type, source_id, item_info_id, name) VALUES (?, ?, ?, ?)",
                    sourceType,
                    sourceId,
                    itemId,
                    normalizedTags[i]);
            }

            return true;
        }

        private static void ReconcileItemSourceOrigins(
            SQLiteConnection connection,
            string sourceType,
            ISet<string> seenSourceIds)
        {
            var origins = connection.Query<ItemSourceOriginRow>(
                "SELECT * FROM item_source_origin WHERE source_type = ?",
                sourceType);
            for (var i = 0; i < origins.Count; i++)
            {
                var missing = seenSourceIds == null || !seenSourceIds.Contains(origins[i].source_id);
                if (missing && origins[i].is_missing == 0)
                {
                    connection.Execute(
                        "UPDATE item_source_origin SET is_missing = 1, imported_at = ? WHERE source_type = ? AND source_id = ?",
                        Now(),
                        sourceType,
                        origins[i].source_id);
                }

                RefreshItemAvailability(connection, origins[i].item_info_id);
            }
        }

        private static void RefreshItemAvailability(SQLiteConnection connection, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            var originCount = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM item_source_origin WHERE item_info_id = ?",
                itemId);
            if (originCount == 0)
            {
                return;
            }

            var activeCount = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM item_source_origin WHERE item_info_id = ? AND is_missing = 0",
                itemId);
            connection.Execute(
                "UPDATE item_info SET is_available = ?, updated_at = ? WHERE id = ?",
                activeCount > 0 ? 1 : 0,
                Now(),
                itemId);
        }

        private static void ArchiveOrphanedManagedItem(SQLiteConnection connection, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            var sourceCount = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM item_source_origin WHERE item_info_id = ?",
                itemId);
            var boothCount = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM booth_info WHERE item_info_id = ?",
                itemId);
            var ee4vFileCount = connection.ExecuteScalar<int>(
                @"SELECT COUNT(*)
                  FROM file_info
                  INNER JOIN ee4v_file_origin ON ee4v_file_origin.file_info_id = file_info.id
                  WHERE " + FileBelongsToItemWhereClause(),
                itemId,
                itemId,
                itemId);
            if (sourceCount == 0 && boothCount == 0 && ee4vFileCount == 0)
            {
                connection.Execute("UPDATE item_info SET is_available = 0, updated_at = ? WHERE id = ?", Now(), itemId);
            }
        }

        private static string CreateBlmFileSourceKey(string registeredItemId, string relativePath)
        {
            return (registeredItemId ?? string.Empty) + "\n" + (relativePath ?? string.Empty);
        }

        private static HashSet<string> BuildBlmFileSourceKeys(IReadOnlyList<BlmItemRecord> records)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                var files = records[recordIndex].Files ?? Array.Empty<BlmFileRecord>();
                for (var fileIndex = 0; fileIndex < files.Count; fileIndex++)
                {
                    keys.Add(CreateBlmFileSourceKey(records[recordIndex].RegisteredItemId, files[fileIndex].RelativePath));
                }
            }

            return keys;
        }

        private static void ReconcileBlmFileOrigins(SQLiteConnection connection, ISet<string> seenSourceIds)
        {
            var origins = connection.Query<BlmOriginRow>("SELECT * FROM blm_file_origin");
            for (var i = 0; i < origins.Count; i++)
            {
                var key = CreateBlmFileSourceKey(origins[i].registered_item_id, origins[i].relative_path);
                if ((seenSourceIds == null || !seenSourceIds.Contains(key)) && origins[i].is_missing == 0)
                {
                    connection.Execute(
                        "UPDATE blm_file_origin SET is_missing = 1, imported_at = ? WHERE file_info_id = ?",
                        Now(),
                        origins[i].file_info_id);
                }

                RefreshFileAvailability(connection, origins[i].file_info_id);
            }
        }

        private static void ReconcileEagleFileOrigins(SQLiteConnection connection, ISet<string> seenSourceIds)
        {
            var origins = connection.Query<EagleOriginRow>("SELECT * FROM eagle_file_origin");
            for (var i = 0; i < origins.Count; i++)
            {
                if ((seenSourceIds == null || !seenSourceIds.Contains(origins[i].eagle_item_id)) &&
                    origins[i].is_deleted.GetValueOrDefault() == 0)
                {
                    connection.Execute(
                        "UPDATE eagle_file_origin SET is_deleted = 1, imported_at = ? WHERE file_info_id = ?",
                        Now(),
                        origins[i].file_info_id);
                }

                RefreshFileAvailability(connection, origins[i].file_info_id);
            }
        }

        private static void ReconcileEagleDownloadOnlyFiles(SQLiteConnection connection, ISet<long> seenDownloadIds)
        {
            var rows = connection.Query<FileRow>(
                @"SELECT * FROM file_info
                  WHERE download_id IS NOT NULL
                    AND id NOT IN (SELECT file_info_id FROM ee4v_file_origin)
                    AND id NOT IN (SELECT file_info_id FROM eagle_file_origin)
                    AND id NOT IN (SELECT file_info_id FROM blm_file_origin)");
            for (var i = 0; i < rows.Count; i++)
            {
                var available = rows[i].download_id.HasValue &&
                                seenDownloadIds != null &&
                                seenDownloadIds.Contains(rows[i].download_id.Value);
                if ((rows[i].is_available != 0) != available)
                {
                    connection.Execute(
                        "UPDATE file_info SET is_available = ?, updated_at = ? WHERE id = ?",
                        available ? 1 : 0,
                        Now(),
                        rows[i].id);
                }
            }
        }

        private static void RefreshFileAvailability(SQLiteConnection connection, string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return;
            }

            var available = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM ee4v_file_origin WHERE file_info_id = ?",
                fileId) > 0 ||
                connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM eagle_file_origin WHERE file_info_id = ? AND COALESCE(is_deleted, 0) = 0",
                    fileId) > 0 ||
                connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM blm_file_origin WHERE file_info_id = ? AND is_missing = 0",
                    fileId) > 0;
            connection.Execute(
                "UPDATE file_info SET is_available = ?, updated_at = ? WHERE id = ?",
                available ? 1 : 0,
                Now(),
                fileId);
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
            var parent = ResolveImportedFileParent(connection, itemId, safeFileName);
            InsertFileInfo(connection, fileId, parent.ItemId, parent.VersionGroupId, parent.VariantGroupId, safeFileName, extension, sizeBytes, downloadId, now);
            EnsureVersionGroupPrimaryIfMissing(connection, parent.VersionGroupId, fileId, now);
            return fileId;
        }

        private static bool NormalizeExistingItemInfoText(SQLiteConnection connection, string itemId, string previousSourceName, string previousSourceDescription, string nextSourceName, string nextSourceDescription, bool overwriteItemText)
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
            if (overwriteItemText || StringEquals(item.name, previousSourceName))
            {
                normalizedName = nextSourceName;
            }

            if (overwriteItemText || StringEquals(item.description, previousSourceDescription))
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

            public AssetSyncStatus Status { get; set; }
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
