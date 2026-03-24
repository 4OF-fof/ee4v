using System.Collections.Generic;

namespace Ee4v.AssetManager
{
    public sealed class BoothLibraryItemRecord
    {
        public BoothLibraryItemRecord(
            long boothItemId,
            string name,
            string itemUrl,
            string description,
            string thumbnailUrl,
            string shopName,
            string shopUrl,
            string shopThumbnailUrl,
            IReadOnlyList<string> tags)
        {
            BoothItemId = boothItemId;
            Name = name ?? string.Empty;
            ItemUrl = itemUrl ?? string.Empty;
            Description = description ?? string.Empty;
            ThumbnailUrl = thumbnailUrl ?? string.Empty;
            ShopName = shopName ?? string.Empty;
            ShopUrl = shopUrl ?? string.Empty;
            ShopThumbnailUrl = shopThumbnailUrl ?? string.Empty;
            Tags = tags ?? new string[0];
        }

        public long BoothItemId { get; private set; }

        public string Name { get; private set; }

        public string ItemUrl { get; private set; }

        public string Description { get; private set; }

        public string ThumbnailUrl { get; private set; }

        public string ShopName { get; private set; }

        public string ShopUrl { get; private set; }

        public string ShopThumbnailUrl { get; private set; }

        public IReadOnlyList<string> Tags { get; private set; }
    }
}
