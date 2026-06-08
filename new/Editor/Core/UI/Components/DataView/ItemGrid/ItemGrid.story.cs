using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ItemGridCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/DataView/ItemGrid/item-grid.uss");
                registry.RegisterStory(new StoryRegistration(
                    "item-grid",
                    "DataView",
                    "ItemGrid",
                    "ItemCard を仮想スクロールで並べる汎用グリッドコンポーネントです。",
                    "UI Toolkit の ListView を行単位で使い、表示領域に必要な行だけを生成します。列数は available width から再計算し、各セルには ItemCard を配置します。",
                    new[]
                    {
                        "ItemCard"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildItemGridStory(parent)));
            }
        }

        private void BuildItemGridStory(VisualElement parent)
        {
            var itemCount = 80;
            var itemsPerRow = 6;
            Action refresh = null;
            var controls = CreatePlainControlsSection(parent, "表示 item 数と 1 行あたりの個数を変えながら、仮想スクロールと可変 card 幅を確認します。");

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

            var thumbnail = CreateItemCardSampleThumbnail(132, 132);
            var grid = new ItemGrid();
            grid.style.flexGrow = 1f;
            surface.Add(grid);
            preview.Body.Add(surface);

            refresh = () =>
            {
                var items = new List<ItemCardState>();
                for (var i = 0; i < itemCount; i++)
                {
                    items.Add(new ItemCardState(
                        string.Format("Sample Item {0:00}", i + 1),
                        i % 4 == 0 ? null : thumbnail));
                }

                grid.SetState(new ItemGridState(items, itemsPerRow));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
