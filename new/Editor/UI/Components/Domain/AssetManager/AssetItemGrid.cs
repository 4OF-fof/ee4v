namespace Ee4v.UI
{
    internal sealed class AssetItemGrid : ItemGrid
    {
        public AssetItemGrid()
        {
            AssetItemGridCache.EnsureCacheInvalidationRegistered();
        }

        public bool TrySetCachedItems(AssetManagerItemListRequest request, out string statusText)
        {
            ItemGridState gridState;
            if (AssetItemGridCache.TryGet(request, out gridState, out statusText))
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

        public void SetAssetItems(AssetManagerItemListRequest request, AssetManagerItemList itemList, out string statusText)
        {
            ItemGridState gridState;
            AssetItemGridCache.Store(request, itemList, out gridState, out statusText);
            SetState(gridState);
        }

        protected override ItemCard CreateItemCard()
        {
            return new ItemCard();
        }
    }
}
