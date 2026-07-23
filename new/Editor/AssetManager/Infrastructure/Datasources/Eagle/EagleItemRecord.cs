using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Infrastructure.Datasources.Eagle
{
    internal sealed class EagleItemRecord
    {
        internal string EagleItemId { get; set; }
        internal string ItemName { get; set; }
        internal string ItemDescription { get; set; }
        internal long? BoothItemId { get; set; }
        internal string BoothName { get; set; }
        internal string BoothDescription { get; set; }
        internal string BoothThumbnailUrl { get; set; }
        internal string ShopName { get; set; }
        internal string ShopUrl { get; set; }
        internal string ShopThumbnailUrl { get; set; }
        internal DateTime? BoothLastUpdatedAtUtc { get; set; }
        internal DateTime? SourceUpdatedAtUtc { get; set; }
        internal IReadOnlyList<string> Tags { get; set; }
        internal IReadOnlyList<EagleFileRecord> Files { get; set; }
    }

    internal sealed class EagleFileRecord
    {
        internal string EagleItemId { get; set; }
        internal long? DownloadId { get; set; }
        internal string Name { get; set; }
        internal long? SizeBytes { get; set; }
        internal string Extension { get; set; }
        internal bool IsDeleted { get; set; }
        internal string FilePath { get; set; }
    }
}
