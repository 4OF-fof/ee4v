namespace Ee4v.UI
{
    internal sealed class AssetItemGrid : ItemGrid
    {
        public AssetItemGrid()
        {
        }

        public bool TrySetCachedItems(string cacheKey, out string statusText)
        {
            ItemGridState gridState;
            if (AssetItemGridCache.TryGet(cacheKey, out gridState, out statusText))
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
            ItemGridState gridState;
            AssetItemGridCache.Store(cacheKey, itemList, out gridState, out statusText);
            SetState(gridState);
        }

        public void ClearCachedItems()
        {
            AssetItemGridCache.Clear();
        }

        protected override ItemCard CreateItemCard()
        {
            return new ItemCard();
        }
    }
}
