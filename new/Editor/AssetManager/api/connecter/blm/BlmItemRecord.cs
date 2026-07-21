using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Api.Connecter.Blm
{
    internal sealed class BlmItemRecord
    {
        internal BlmItemRecord(
            long boothItemId,
            string name,
            string itemUrl,
            string description,
            string thumbnailUrl,
            string shopName,
            string shopUrl,
            string shopThumbnailUrl,
            DateTime? lastUpdatedAtUtc,
            IReadOnlyList<string> tags = null,
            string registeredItemId = null,
            IReadOnlyList<BlmFileRecord> files = null,
            bool fileSnapshotComplete = false)
        {
            BoothItemId = boothItemId;
            Name = name ?? string.Empty;
            ItemUrl = itemUrl ?? string.Empty;
            Description = description ?? string.Empty;
            ThumbnailUrl = thumbnailUrl ?? string.Empty;
            ShopName = shopName ?? string.Empty;
            ShopUrl = shopUrl ?? string.Empty;
            ShopThumbnailUrl = shopThumbnailUrl ?? string.Empty;
            LastUpdatedAtUtc = lastUpdatedAtUtc;
            Tags = tags ?? Array.Empty<string>();
            RegisteredItemId = registeredItemId;
            Files = files ?? new BlmFileRecord[0];
            FileSnapshotComplete = fileSnapshotComplete;
        }

        internal long BoothItemId { get; private set; }

        internal string Name { get; private set; }

        internal string ItemUrl { get; private set; }

        internal string Description { get; private set; }

        internal string ThumbnailUrl { get; private set; }

        internal string ShopName { get; private set; }

        internal string ShopUrl { get; private set; }

        internal string ShopThumbnailUrl { get; private set; }

        internal DateTime? LastUpdatedAtUtc { get; private set; }

        internal IReadOnlyList<string> Tags { get; private set; }

        internal string RegisteredItemId { get; private set; }

        internal IReadOnlyList<BlmFileRecord> Files { get; private set; }

        internal bool FileSnapshotComplete { get; private set; }
    }

    internal sealed class BlmFileRecord
    {
        internal BlmFileRecord(string relativePath, string filePath, long? sizeBytes)
        {
            RelativePath = relativePath ?? string.Empty;
            FilePath = filePath;
            SizeBytes = sizeBytes;
        }

        internal string RelativePath { get; private set; }

        internal string FilePath { get; private set; }

        internal long? SizeBytes { get; private set; }
    }
}
