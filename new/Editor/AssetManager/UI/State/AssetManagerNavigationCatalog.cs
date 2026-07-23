using System;
using Ee4v.Core.I18n;
using Ee4v.UI;

namespace Ee4v.AssetManager
{
    internal static class AssetManagerNavigationCatalog
    {
        private static readonly string[] ItemIds =
        {
            "all-assets",
            "favorites",
            "booth-library",
            "packages"
        };

        public static string DefaultItemId
        {
            get { return ItemIds[0]; }
        }

        public static AssetManagerViewItemState[] Items
        {
            get { return CreateItems(); }
        }

        public static string NormalizeItemId(string itemId)
        {
            for (var i = 0; i < ItemIds.Length; i++)
            {
                if (string.Equals(ItemIds[i], itemId, StringComparison.Ordinal))
                {
                    return ItemIds[i];
                }
            }

            return DefaultItemId;
        }

        public static AssetManagerViewItemState GetItem(string itemId)
        {
            var resolvedId = NormalizeItemId(itemId);
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
