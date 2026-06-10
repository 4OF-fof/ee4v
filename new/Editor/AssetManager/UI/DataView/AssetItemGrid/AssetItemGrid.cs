using System.Collections.Generic;
using Ee4v.UI;

namespace Ee4v.AssetManager
{
    internal sealed class AssetItemGrid : SelectableItemGrid
    {
        public AssetItemGrid()
        {
            History = new AssetItemGridHistory();
        }

        public AssetItemGridHistory History { get; }

        public bool TrySetCachedItems(string cacheKey, out string statusText)
        {
            ItemGridState gridState;
            if (ItemGridStateCache.TryGet(cacheKey, out gridState, out statusText))
            {
                SetState(gridState);
                return true;
            }

            return false;
        }

        public void SetLoading()
        {
            SetState(new ItemGridState(null));
        }

        public void SetAssetItems(string cacheKey, AssetItemGridList itemList, out string statusText)
        {
            var gridState = CreateGridState(itemList, out statusText);
            ItemGridStateCache.Store(cacheKey, gridState, statusText);
            SetState(gridState);
        }

        public void ClearCachedItems()
        {
            ItemGridStateCache.Clear();
        }

        protected override ItemCard CreateItemCard()
        {
            return new ItemCard();
        }

        private static ItemGridState CreateGridState(AssetItemGridList itemList, out string statusText)
        {
            var list = itemList ?? new AssetItemGridList(null);
            var itemCardStates = new List<ItemCardState>(list.Items.Count);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var item = list.Items[i];
                if (item == null)
                {
                    continue;
                }

                itemCardStates.Add(new ItemCardState(item.ItemId, item.ItemName, item.ImageState, item.IconState, item.ParentItemId));
            }

            statusText = itemCardStates.Count == 0 ? list.EmptyText : string.Empty;
            return new ItemGridState(itemCardStates, list.ItemsPerRow);
        }
    }
}
