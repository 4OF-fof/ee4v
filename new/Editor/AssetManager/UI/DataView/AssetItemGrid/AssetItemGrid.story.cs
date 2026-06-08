using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class AssetItemGridCatalogRegistrarStory
    {
        private sealed class AssetItemGridCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 104; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "asset-item-grid",
                    "Domain/AssetManager",
                    "AssetItemGrid",
                    "AssetManager item grid list を受け取り、汎用 ItemGrid に表示状態として流し込む domain component です。",
                    "AssetItemGridList から ItemImageState 付き ItemGridState への変換と grid state cache 利用を内包し、呼び出し側が ItemGridState を直接意識しないための adapter として扱います。",
                    new[]
                    {
                        "ItemGrid",
                        "ItemCard",
                        "ItemImage"
                    },
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildAssetItemGridStory(window, parent)));
            }
        }

        private static void BuildAssetItemGridStory(CatalogWindow window, VisualElement parent)
        {
            var itemCount = 80;
            var itemsPerRow = 6;
            Action refresh = null;
            var controls = window.CreatePlainControlsSection(parent, "AssetItemGridList を流し込み、AssetItemGrid 側の変換と cache 利用を確認します。");

            var itemCountField = new IntegerField("Item Count")
            {
                value = itemCount
            };
            itemCountField.RegisterValueChangedCallback(evt =>
            {
                itemCount = Mathf.Clamp(evt.newValue, 0, 500);
                itemCountField.SetValueWithoutNotify(itemCount);
                if (refresh != null)
                {
                    refresh();
                }
            });
            controls.Content.Add(itemCountField);

            var itemsPerRowField = new IntegerField("Items Per Row")
            {
                value = itemsPerRow
            };
            itemsPerRowField.RegisterValueChangedCallback(evt =>
            {
                itemsPerRow = Mathf.Clamp(evt.newValue, 1, 12);
                itemsPerRowField.SetValueWithoutNotify(itemsPerRow);
                if (refresh != null)
                {
                    refresh();
                }
            });
            controls.Content.Add(itemsPerRowField);

            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;
            surface.style.height = 420f;

            var grid = new AssetItemGrid();
            grid.style.flexGrow = 1f;
            surface.Add(grid);
            preview.Body.Add(surface);

            refresh = () =>
            {
                string ignoredStatusText;
                grid.SetAssetItems("catalog-asset-item-grid", CreateCatalogAssetItemList(itemCount, itemsPerRow), out ignoredStatusText);
            };

            refresh();
            CatalogWindow.FinalizeControlsSection(parent, controls);
        }

        private static AssetItemGridList CreateCatalogAssetItemList(int itemCount, int itemsPerRow)
        {
            var thumbnail = CatalogWindow.CreateItemCardSampleThumbnail(132, 132);
            var thumbnailBytes = thumbnail.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(thumbnail);

            var items = new List<AssetItemGridListItem>();
            for (var i = 0; i < itemCount; i++)
            {
                items.Add(new AssetItemGridListItem(
                    string.Format("Asset Item {0:00}", i + 1),
                    i % 4 == 0 ? null : thumbnailBytes));
            }

            return new AssetItemGridList(items, "No asset items.", itemsPerRow);
        }
    }
}
