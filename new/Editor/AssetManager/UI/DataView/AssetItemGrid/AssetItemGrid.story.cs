using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class AssetItemGridCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 104; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss");
                registry.RegisterStory(new StoryRegistration(
                    "asset-item-grid",
                    "Domain/AssetManager",
                    "AssetItemGrid",
                    "AssetManager item list を受け取り、汎用 ItemGrid に表示状態として流し込む domain component です。",
                    "AssetManagerItemList から Texture2D 付き ItemGridState への変換と cache 利用を内包し、MainView 側が ItemGridCache を直接意識しないための adapter として扱います。",
                    new[]
                    {
                        "ItemGrid",
                        "ItemCard"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAssetItemGridStory(parent)));
            }
        }

        private void BuildAssetItemGridStory(VisualElement parent)
        {
            var itemCount = 80;
            var itemsPerRow = 6;
            Action refresh = null;
            var controls = CreatePlainControlsSection(parent, "AssetManagerItemList を流し込み、AssetItemGrid 側の変換と cache 利用を確認します。");

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

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
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
                var request = new AssetManagerItemListRequest("catalog-asset-item-grid");
                string ignoredStatusText;
                grid.SetAssetItems(request, CreateCatalogAssetItemList(itemCount, itemsPerRow), out ignoredStatusText);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private AssetManagerItemList CreateCatalogAssetItemList(int itemCount, int itemsPerRow)
        {
            var thumbnail = CreateItemCardSampleThumbnail(132, 132);
            var thumbnailBytes = thumbnail.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(thumbnail);

            var items = new List<AssetManagerItemListItem>();
            for (var i = 0; i < itemCount; i++)
            {
                items.Add(new AssetManagerItemListItem(
                    string.Format("Asset Item {0:00}", i + 1),
                    i % 4 == 0 ? null : thumbnailBytes));
            }

            return new AssetManagerItemList(items, "No asset items.", itemsPerRow);
        }
    }
}
