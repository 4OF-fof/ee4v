using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class SelectableItemGridCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 11; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Collections/ItemGrid/item-grid.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Inputs/Selection/SelectableItemGrid/selectable-item-grid.uss");
                registry.RegisterStory(new StoryRegistration(
                    "selectable-item-grid",
                    "Inputs/Selection",
                    "SelectableItemGrid",
                    "ItemGrid にクリック選択、Ctrl/Cmd 複数選択、Shift 範囲選択、Ctrl/Cmd ドラッグ連続選択を追加した selectable grid component です。",
                    "表示専用の ItemGrid から選択責務を分離し、通常クリックは単一選択、Ctrl/Cmd クリックは複数トグル、Shift クリックは範囲選択、Ctrl/Cmd ドラッグは連続選択として SelectionChanged に通知します。",
                    new[]
                    {
                        "ItemGrid",
                        "ItemCard"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSelectableItemGridStory(parent)));
            }
        }

        private void BuildSelectableItemGridStory(VisualElement parent)
        {
            var itemCount = 80;
            var itemsPerRow = 6;
            Action refresh = null;
            var controls = CreatePlainControlsSection(parent, "通常クリック、Ctrl/Cmd クリック、Shift 範囲選択、Ctrl/Cmd ドラッグ連続選択の状態通知を確認します。");

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

            var selectedCard = new InfoCard();
            selectedCard.SetState(CreateSelectableItemGridSelectionState(null));
            controls.Content.Add(selectedCard);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;
            surface.style.height = 420f;

            var thumbnail = CreateItemCardSampleThumbnail(132, 132);
            var thumbnailBytes = thumbnail.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(thumbnail);

            var grid = new SelectableItemGrid();
            grid.style.flexGrow = 1f;
            grid.SelectionChanged += items =>
            {
                selectedCard.SetState(CreateSelectableItemGridSelectionState(items));
            };
            surface.Add(grid);
            preview.Body.Add(surface);

            refresh = () =>
            {
                grid.SetState(new ItemGridState(CreateSelectableItemGridItems(itemCount, thumbnailBytes), itemsPerRow));
                selectedCard.SetState(CreateSelectableItemGridSelectionState(grid.SelectedItems));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private static List<ItemCardState> CreateSelectableItemGridItems(int itemCount, byte[] thumbnailBytes)
        {
            var items = new List<ItemCardState>();
            for (var i = 0; i < itemCount; i++)
            {
                items.Add(new ItemCardState(
                    "sample-" + (i + 1),
                    string.Format("Sample Item {0:00}", i + 1),
                    i % 4 == 0 ? null : new ItemImageState(thumbnailBytes)));
            }

            return items;
        }

        private static InfoCardState CreateSelectableItemGridSelectionState(IReadOnlyList<ItemCardState> items)
        {
            if (items == null || items.Count == 0)
            {
                return new InfoCardState("Selection", "未選択", null, "0");
            }

            var builder = new StringBuilder();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(items[i].ItemName);
            }

            return new InfoCardState("Selection", builder.ToString(), null, items.Count.ToString());
        }
    }
}
