using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetSyncFieldDiff
    {
        internal AssetSyncFieldDiff(string field, string unityValue, string datasourceValue)
        {
            Field = field ?? string.Empty;
            UnityValue = unityValue ?? string.Empty;
            DatasourceValue = datasourceValue ?? string.Empty;
        }

        internal string Field { get; }
        internal string UnityValue { get; }
        internal string DatasourceValue { get; }
    }

    internal sealed class AssetSyncConflict
    {
        internal AssetSyncConflict(
            AssetSourceType sourceType,
            string sourceId,
            string itemId,
            string itemName,
            DateTime unityUpdatedAtUtc,
            DateTime? datasourceUpdatedAtUtc,
            IReadOnlyList<AssetSyncFieldDiff> fields)
        {
            SourceType = sourceType;
            SourceId = sourceId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            UnityUpdatedAtUtc = unityUpdatedAtUtc;
            DatasourceUpdatedAtUtc = datasourceUpdatedAtUtc;
            Fields = fields ?? Array.Empty<AssetSyncFieldDiff>();
        }

        internal AssetSourceType SourceType { get; }
        internal string SourceId { get; }
        internal string ItemId { get; }
        internal string ItemName { get; }
        internal DateTime UnityUpdatedAtUtc { get; }
        internal DateTime? DatasourceUpdatedAtUtc { get; }
        internal IReadOnlyList<AssetSyncFieldDiff> Fields { get; }
    }

    internal sealed class AssetSyncPreview
    {
        internal AssetSyncPreview(
            AssetSourceType sourceType,
            string fingerprint,
            bool hasChanges,
            IReadOnlyList<AssetSyncConflict> conflicts)
        {
            SourceType = sourceType;
            Fingerprint = fingerprint ?? string.Empty;
            HasChanges = hasChanges;
            Conflicts = conflicts ?? Array.Empty<AssetSyncConflict>();
        }

        internal AssetSourceType SourceType { get; }
        internal string Fingerprint { get; }
        internal bool HasChanges { get; }
        internal IReadOnlyList<AssetSyncConflict> Conflicts { get; }
    }

    internal sealed class PreparedAssetSync
    {
        internal PreparedAssetSync(AssetSyncPreview preview, object adapterState)
        {
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
            AdapterState = adapterState ?? throw new ArgumentNullException(nameof(adapterState));
        }

        internal AssetSyncPreview Preview { get; }
        internal object AdapterState { get; }
    }
}
