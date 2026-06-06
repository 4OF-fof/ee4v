using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private void BuildSearchableTreeViewStory(VisualElement parent)
        {
            var searchableTreeViewMeta = "Tree";
            Action refresh = null;

            var controls = CreatePlainControlsSection(
                parent,
                "行右側の短い文字列は SearchableTreeView 固有の列ではなく、Catalog story では bindItem が SampleTreeNode.Meta を描画しています。");
            var searchableTreeViewMetaField = AddTextField(controls.Content, "SampleTreeNode.Meta (SearchableTreeView)", searchableTreeViewMeta, value =>
            {
                searchableTreeViewMeta = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var treeView = new SearchableTreeView<SampleTreeNode>(
                CreateSampleTreeItem,
                BindSampleTreeItem,
                null,
                "一致する項目がありません。");
            treeView.SetViewDataKey("ee4v-ui-catalog-searchable-tree-view-story");
            preview.Body.Add(treeView);

            refresh = () =>
            {
                searchableTreeViewMetaField.SetValueWithoutNotify(searchableTreeViewMeta);
                treeView.SetItems(BuildSampleTreeItems("Input", searchableTreeViewMeta));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private void BuildAssetManagerItemGridStory(VisualElement parent)
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
