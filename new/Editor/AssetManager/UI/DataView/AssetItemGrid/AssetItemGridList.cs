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
            : this(string.Empty, itemName, new ItemImageState(thumbnailData))
        {
        }

        public AssetItemGridListItem(string itemName, ItemImageState imageState)
            : this(string.Empty, itemName, imageState)
        {
        }

        public AssetItemGridListItem(string itemId, string itemName, ItemImageState imageState)
        {
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            ImageState = imageState ?? new ItemImageState();
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public ItemImageState ImageState { get; }
    }
}
