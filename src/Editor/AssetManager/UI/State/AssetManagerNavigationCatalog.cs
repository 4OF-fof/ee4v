using System;
using Ee4v.Core.I18n;
using Ee4v.UI;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerNavigationCatalog
    {
        private static readonly string[] ItemIds =
        {
            "all-assets",
            "booth-items",
            "uncategorized",
            "tags"
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
                    I18N.Get("assetManager.navigation.all.label"),
                    string.Empty,
                    string.Empty,
                    I18N.Get("assetManager.navigation.all.label"),
                    string.Empty,
                    Array.Empty<string>(),
                    IconState.FromBuiltinIcon(UiBuiltinIcon.Package, size: UiSizeTokens.Size12)),
                new AssetManagerViewItemState(
                    "booth-items",
                    I18N.Get("assetManager.navigation.boothItems.label"),
                    string.Empty,
                    string.Empty,
                    I18N.Get("assetManager.navigation.boothItems.label"),
                    string.Empty,
                    Array.Empty<string>(),
                    IconState.FromBuiltinIcon(UiBuiltinIcon.Store, size: UiSizeTokens.Size12)),
                new AssetManagerViewItemState(
                    "uncategorized",
                    I18N.Get("assetManager.navigation.uncategorized.label"),
                    string.Empty,
                    string.Empty,
                    I18N.Get("assetManager.navigation.uncategorized.label"),
                    string.Empty,
                    Array.Empty<string>(),
                    IconState.FromBuiltinIcon(UiBuiltinIcon.Uncategorized, size: UiSizeTokens.Size12)),
                new AssetManagerViewItemState(
                    "tags",
                    I18N.Get("assetManager.navigation.tags.label"),
                    string.Empty,
                    string.Empty,
                    I18N.Get("assetManager.navigation.tags.label"),
                    string.Empty,
                    Array.Empty<string>(),
                    IconState.FromBuiltinIcon(UiBuiltinIcon.Tag, size: UiSizeTokens.Size12))
            };
        }
    }
}
