using System;
using System.Collections.Generic;

namespace Ee4v.UI
{
    internal sealed class AssetItemGridList
    {
        public AssetItemGridList(IReadOnlyList<AssetItemGridListItem> items, string emptyText = null, int itemsPerRow = 6)
        {
            Items = items ?? Array.Empty<AssetItemGridListItem>();
            EmptyText = emptyText ?? string.Empty;
            ItemsPerRow = Math.Max(1, itemsPerRow);
        }

        public IReadOnlyList<AssetItemGridListItem> Items { get; }

        public string EmptyText { get; }

        public int ItemsPerRow { get; }
    }

    internal sealed class AssetItemGridListItem
    {
        public AssetItemGridListItem(string itemName, byte[] thumbnailData = null)
        {
            ItemName = itemName ?? string.Empty;
            ThumbnailData = thumbnailData ?? Array.Empty<byte>();
        }

        public string ItemName { get; }

        public byte[] ThumbnailData { get; }
    }
}
