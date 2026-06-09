using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;

namespace Ee4v.UI
{
    internal sealed class AssetManagerViewItemState
    {
        public AssetManagerViewItemState(
            string id,
            string label,
            string meta,
            string eyebrow,
            string title,
            string description,
            string[] rows,
            IconState iconState = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Meta = meta ?? string.Empty;
            Eyebrow = eyebrow ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Rows = rows ?? Array.Empty<string>();
            IconState = iconState;
        }

        public string Id { get; }

        public string Label { get; }

        public string Meta { get; }

        public string Eyebrow { get; }

        public string Title { get; }

        public string Description { get; }

        public string[] Rows { get; }

        public IconState IconState { get; }
    }

    internal static class AssetManagerViewState
    {
        private static readonly string[] ItemIds =
        {
            "all-assets",
            "favorites",
            "booth-library",
            "packages"
        };

        private static string _selectedItemId = ItemIds[0];
        private static ItemCardState[] _selectedAssetItems = Array.Empty<ItemCardState>();

        public static event Action<string> SelectedItemChanged;

        public static event Action<ItemCardState> SelectedAssetItemChanged;

        public static event Action<IReadOnlyList<ItemCardState>> SelectedAssetItemsChanged;

        public static AssetManagerViewItemState[] Items
        {
            get { return CreateItems(); }
        }

        public static string SelectedItemId
        {
            get { return _selectedItemId; }
        }

        public static AssetManagerViewItemState SelectedItem
        {
            get { return GetItem(SelectedItemId); }
        }

        public static ItemCardState SelectedAssetItem
        {
            get { return _selectedAssetItems.Length > 0 ? _selectedAssetItems[0] : null; }
        }

        public static IReadOnlyList<ItemCardState> SelectedAssetItems
        {
            get { return _selectedAssetItems; }
        }

        public static AssetManagerViewItemState GetItem(string itemId)
        {
            var resolvedId = NormalizeSelectedItemId(itemId);
            var items = CreateItems();
            for (var i = 0; i < items.Length; i++)
            {
                if (string.Equals(items[i].Id, resolvedId, StringComparison.Ordinal))
                {
                    return items[i];
                }
            }

            return items[0];
        }

        public static void SetSelectedItem(string itemId, bool notify = true)
        {
            var resolvedId = NormalizeSelectedItemId(itemId);
            if (string.Equals(_selectedItemId, resolvedId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedItemId = resolvedId;

            if (notify)
            {
                SelectedItemChanged?.Invoke(_selectedItemId);
            }
        }

        public static void SetSelectedAssetItem(ItemCardState item, bool notify = true)
        {
            SetSelectedAssetItems(item != null ? new[] { item } : null, notify);
        }

        public static void SetSelectedAssetItems(IReadOnlyList<ItemCardState> items, bool notify = true)
        {
            if (items == null || items.Count == 0)
            {
                _selectedAssetItems = Array.Empty<ItemCardState>();
            }
            else
            {
                var nextItems = new List<ItemCardState>(items.Count);
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        nextItems.Add(items[i]);
                    }
                }

                _selectedAssetItems = nextItems.ToArray();
            }

            if (notify)
            {
                SelectedAssetItemChanged?.Invoke(SelectedAssetItem);
                SelectedAssetItemsChanged?.Invoke(_selectedAssetItems);
            }
        }

        private static string NormalizeSelectedItemId(string itemId)
        {
            for (var i = 0; i < ItemIds.Length; i++)
            {
                if (string.Equals(ItemIds[i], itemId, StringComparison.Ordinal))
                {
                    return ItemIds[i];
                }
            }

            return ItemIds[0];
        }

        private static AssetManagerViewItemState[] CreateItems()
        {
            return new[]
            {
                new AssetManagerViewItemState(
                    "all-assets",
                    I18N.Get("assetManager.navigation.allAssets.label"),
                    I18N.Get("assetManager.navigation.allAssets.meta"),
                    I18N.Get("assetManager.navigation.allAssets.eyebrow"),
                    I18N.Get("assetManager.navigation.allAssets.title"),
                    I18N.Get("assetManager.navigation.allAssets.description"),
                    new[]
                    {
                        I18N.Get("assetManager.navigation.allAssets.row.search"),
                        I18N.Get("assetManager.navigation.allAssets.row.viewMode"),
                        I18N.Get("assetManager.navigation.allAssets.row.bulkActions")
                    },
                    IconState.FromBuiltinIcon(UiBuiltinIcon.Search, size: 12f)),
                new AssetManagerViewItemState(
                    "favorites",
                    I18N.Get("assetManager.navigation.favorites.label"),
                    I18N.Get("assetManager.navigation.favorites.meta"),
                    I18N.Get("assetManager.navigation.favorites.eyebrow"),
                    I18N.Get("assetManager.navigation.favorites.title"),
                    I18N.Get("assetManager.navigation.favorites.description"),
                    new[]
                    {
                        I18N.Get("assetManager.navigation.favorites.row.list"),
                        I18N.Get("assetManager.navigation.favorites.row.recent"),
                        I18N.Get("assetManager.navigation.favorites.row.preview")
                    },
                    IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureOpen, size: 12f)),
                new AssetManagerViewItemState(
                    "booth-library",
                    I18N.Get("assetManager.navigation.boothLibrary.label"),
                    I18N.Get("assetManager.navigation.boothLibrary.meta"),
                    I18N.Get("assetManager.navigation.boothLibrary.eyebrow"),
                    I18N.Get("assetManager.navigation.boothLibrary.title"),
                    I18N.Get("assetManager.navigation.boothLibrary.description"),
                    new[]
                    {
                        I18N.Get("assetManager.navigation.boothLibrary.row.purchases"),
                        I18N.Get("assetManager.navigation.boothLibrary.row.sync"),
                        I18N.Get("assetManager.navigation.boothLibrary.row.download")
                    },
                    IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: 12f)),
                new AssetManagerViewItemState(
                    "packages",
                    I18N.Get("assetManager.navigation.packages.label"),
                    I18N.Get("assetManager.navigation.packages.meta"),
                    I18N.Get("assetManager.navigation.packages.eyebrow"),
                    I18N.Get("assetManager.navigation.packages.title"),
                    I18N.Get("assetManager.navigation.packages.description"),
                    new[]
                    {
                        I18N.Get("assetManager.navigation.packages.row.installed"),
                        I18N.Get("assetManager.navigation.packages.row.dependencies"),
                        I18N.Get("assetManager.navigation.packages.row.updates")
                    })
            };
        }
    }
}
