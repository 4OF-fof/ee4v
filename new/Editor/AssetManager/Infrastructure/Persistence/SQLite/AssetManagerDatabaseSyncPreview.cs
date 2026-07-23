using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application;
using Ee4v.AssetManager.Infrastructure.Datasources.Blm;
using Ee4v.AssetManager.Infrastructure.Datasources.Eagle;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        internal static PreparedBlmSync PrepareBlmSync(BlmSyncRequest request)
        {
            try
            {
                var records = BlmConnectorApi.ReadItems(request);
                var fingerprint = AssetSyncFingerprint.CreateBlm(records);
                var hasChanges =
                    !HasSuccessfulSyncState("blm") ||
                    !AssetSyncFingerprintCache.Matches(
                        AssetSourceType.Blm,
                        fingerprint);
                return new PreparedBlmSync(
                    records,
                    new AssetSyncPreview(
                        AssetSourceType.Blm,
                        fingerprint,
                        hasChanges,
                        hasChanges ? FindBlmConflicts(records) : Array.Empty<AssetSyncConflict>()));
            }
            catch
            {
                RecordSyncInfoSafely("blm", AssetSyncState.Failed);
                throw;
            }
        }

        internal static PreparedEagleSync PrepareEagleSync(EagleSyncRequest request)
        {
            try
            {
                var records = EagleConnectorApi.ReadItems(request);
                var fingerprint = AssetSyncFingerprint.CreateEagle(records);
                var hasChanges =
                    !HasSuccessfulSyncState("eagle") ||
                    !AssetSyncFingerprintCache.Matches(
                        AssetSourceType.Eagle,
                        fingerprint);
                return new PreparedEagleSync(
                    records,
                    new AssetSyncPreview(
                        AssetSourceType.Eagle,
                        fingerprint,
                        hasChanges,
                        hasChanges ? FindEagleConflicts(records) : Array.Empty<AssetSyncConflict>()));
            }
            catch
            {
                RecordSyncInfoSafely("eagle", AssetSyncState.Failed);
                throw;
            }
        }

        internal static AssetSyncResult ApplyPreparedBlmSync(PreparedBlmSync prepared, bool overwriteItemText)
        {
            if (prepared == null || prepared.Preview == null || !prepared.Preview.HasChanges)
            {
                return new AssetSyncResult(0, 0, 0, 0, AssetSyncState.Success);
            }

            return SyncBlm(prepared.Records, prepared.Preview.Fingerprint, overwriteItemText);
        }

        internal static AssetSyncResult ApplyPreparedEagleSync(PreparedEagleSync prepared, bool overwriteItemText)
        {
            if (prepared == null || prepared.Preview == null || !prepared.Preview.HasChanges)
            {
                return new AssetSyncResult(0, 0, 0, 0, AssetSyncState.Success);
            }

            return SyncEagle(prepared.Records, prepared.Preview.Fingerprint, overwriteItemText);
        }

        private static bool HasSuccessfulSyncState(string sourceType)
        {
            using (var connection = OpenConnection())
            {
                var syncInfo = connection.Query<SyncInfoRow>(
                    "SELECT * FROM sync_info WHERE source_type = ? LIMIT 1",
                    sourceType).FirstOrDefault();
                return syncInfo != null &&
                       string.Equals(
                           syncInfo.last_sync_status,
                           "success",
                           StringComparison.Ordinal);
            }
        }

        private static IReadOnlyList<AssetSyncConflict> FindBlmConflicts(IReadOnlyList<BlmItemRecord> records)
        {
            using (var connection = OpenConnection())
            {
                return (records ?? Array.Empty<BlmItemRecord>())
                    .Select(record => FindConflict(
                        connection,
                        AssetSourceType.Blm,
                        "blm",
                        record.RegisteredItemId,
                        record.Name,
                        record.Description,
                        record.LastUpdatedAtUtc))
                    .Where(conflict => conflict != null)
                    .ToArray();
            }
        }

        private static IReadOnlyList<AssetSyncConflict> FindEagleConflicts(IReadOnlyList<EagleItemRecord> records)
        {
            using (var connection = OpenConnection())
            {
                return (records ?? Array.Empty<EagleItemRecord>())
                    .Select(record => FindConflict(
                        connection,
                        AssetSourceType.Eagle,
                        "eagle",
                        record.EagleItemId,
                        record.ItemName,
                        record.ItemDescription,
                        record.SourceUpdatedAtUtc ?? record.BoothLastUpdatedAtUtc))
                    .Where(conflict => conflict != null)
                    .ToArray();
            }
        }

        private static AssetSyncConflict FindConflict(
            SQLiteConnection connection,
            AssetSourceType sourceType,
            string sourceTypeName,
            string sourceId,
            string sourceName,
            string sourceDescription,
            DateTime? sourceUpdatedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return null;
            }

            var origin = connection.Query<ItemSourceOriginRow>(
                "SELECT * FROM item_source_origin WHERE source_type = ? AND source_id = ? LIMIT 1",
                sourceTypeName,
                sourceId).FirstOrDefault();
            if (origin == null)
            {
                return null;
            }

            var safeName = NormalizeDatasourceText(sourceName);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Item";
            }

            var safeDescription = NormalizeDatasourceText(sourceDescription);
            var sourceNameChanged = !StringEquals(origin.source_name, safeName);
            var sourceDescriptionChanged = !StringEquals(origin.source_description, safeDescription);
            if (!sourceNameChanged && !sourceDescriptionChanged)
            {
                return null;
            }

            var item = connection.Query<ItemRow>("SELECT * FROM item_info WHERE id = ? LIMIT 1", origin.item_info_id).FirstOrDefault();
            if (item == null)
            {
                return null;
            }

            var importedAtUtc = ParseNullableDate(origin.imported_at);
            var datasourceComparisonTimeUtc = sourceUpdatedAtUtc ?? importedAtUtc;
            var unityUpdatedAtUtc = ParseDate(item.updated_at).ToUniversalTime();
            if (datasourceComparisonTimeUtc.HasValue && unityUpdatedAtUtc <= datasourceComparisonTimeUtc.Value.ToUniversalTime())
            {
                return null;
            }

            var fields = new List<AssetSyncFieldDiff>();
            if (sourceNameChanged && !StringEquals(item.name, safeName))
            {
                fields.Add(new AssetSyncFieldDiff("name", item.name, safeName));
            }

            if (sourceDescriptionChanged && !StringEquals(item.description, safeDescription))
            {
                fields.Add(new AssetSyncFieldDiff("description", item.description, safeDescription));
            }

            if (fields.Count == 0)
            {
                return null;
            }

            return new AssetSyncConflict(
                sourceType,
                sourceId,
                item.id,
                item.name,
                unityUpdatedAtUtc,
                sourceUpdatedAtUtc.HasValue ? sourceUpdatedAtUtc.Value.ToUniversalTime() : datasourceComparisonTimeUtc,
                fields);
        }
    }
}
