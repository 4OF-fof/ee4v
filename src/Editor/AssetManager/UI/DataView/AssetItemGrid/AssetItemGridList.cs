using System;
using System.Collections.Generic;
using Ee4v.UI;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetItemGridList
    {
        public AssetItemGridList(
            IReadOnlyList<AssetItemGridListItem> items,
            string emptyText = null,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<ItemCardState>>
                collectionPreviewStates = null)
        {
            Items = items ?? Array.Empty<AssetItemGridListItem>();
            EmptyText = emptyText ?? string.Empty;
            CollectionPreviewStates =
                collectionPreviewStates ??
                new Dictionary<
                    string,
                    IReadOnlyList<ItemCardState>>(
                    StringComparer.Ordinal);
        }

        public IReadOnlyList<AssetItemGridListItem> Items { get; }

        public string EmptyText { get; }

        public IReadOnlyDictionary<
            string,
            IReadOnlyList<ItemCardState>>
            CollectionPreviewStates { get; }

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
            : this(itemId, itemName, imageState, null)
        {
        }

        public AssetItemGridListItem(string itemId, string itemName, ItemImageState imageState, IconState iconState)
            : this(itemId, itemName, imageState, iconState, string.Empty)
        {
        }

        public AssetItemGridListItem(
            string itemId,
            string itemName,
            ItemImageState imageState,
            IconState iconState,
            string parentItemId,
            IReadOnlyList<ItemCardState> stackStates = null,
            IconState nameIconState = null)
        {
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            ImageState = imageState ?? new ItemImageState();
            IconState = iconState;
            ParentItemId = parentItemId ?? string.Empty;
            StackStates =
                stackStates ??
                Array.Empty<ItemCardState>();
            NameIconState = nameIconState;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public ItemImageState ImageState { get; }

        public IconState IconState { get; }

        public string ParentItemId { get; }

        public IReadOnlyList<ItemCardState> StackStates { get; }

        public IconState NameIconState { get; }
    }

    internal sealed class AssetItemGridArtworkState
    {
        public AssetItemGridArtworkState(
            ItemImageState imageState = null,
            IconState iconState = null,
            IReadOnlyList<ItemCardState> stackStates = null)
        {
            ImageState =
                imageState ??
                new ItemImageState();
            IconState = iconState;
            StackStates =
                stackStates ??
                Array.Empty<ItemCardState>();
        }

        public ItemImageState ImageState { get; }

        public IconState IconState { get; }

        public IReadOnlyList<ItemCardState> StackStates { get; }
    }
}
