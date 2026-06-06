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
        private void BuildAssetManagerToolbarStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            var toolbar = new AssetManagerToolbar();
            surface.Add(toolbar);
            preview.Body.Add(surface);
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

        private void BuildAssetManagerNavigationPanelStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = 0f;
            surface.style.paddingRight = 0f;
            surface.style.paddingTop = 0f;
            surface.style.paddingBottom = 0f;
            surface.style.height = 360f;

            var panel = new NavigationPanel();
            panel.style.flexGrow = 1f;
            surface.Add(panel);
            preview.Body.Add(surface);
        }

        private void BuildAssetManagerMainViewStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = 0f;
            surface.style.paddingRight = 0f;
            surface.style.paddingTop = 0f;
            surface.style.paddingBottom = 0f;
            surface.style.height = 360f;

            var panel = new MainView();
            panel.style.flexGrow = 1f;
            surface.Add(panel);
            preview.Body.Add(surface);
        }

        private void BuildAssetManagerInfomationPanelStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = 0f;
            surface.style.paddingRight = 0f;
            surface.style.paddingTop = 0f;
            surface.style.paddingBottom = 0f;
            surface.style.height = 360f;

            var panel = new InfomationPanel();
            panel.style.flexGrow = 1f;
            surface.Add(panel);
            preview.Body.Add(surface);
        }

        private void BuildAssetManagerWindowLayoutStory(VisualElement parent)
        {
            var navigationWidth = 240f;
            var inspectorWidth = 280f;
            var navigationMinWidth = 180f;
            var navigationMaxWidth = 320f;
            var contentMinWidth = 360f;
            var inspectorMinWidth = 220f;
            var inspectorMaxWidth = 360f;
            var navigationCollapsed = false;
            var inspectorCollapsed = false;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "左右ペインは split bar の drag で幅を変えられ、button で完全に折りたためます。min/max を変えると drag 範囲も更新されます。");

            var navigationWidthField = new FloatField("Navigation Width")
            {
                value = navigationWidth
            };
            navigationWidthField.RegisterValueChangedCallback(evt =>
            {
                navigationWidth = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(navigationWidthField);

            var navigationMinWidthField = new FloatField("Navigation Min")
            {
                value = navigationMinWidth
            };
            navigationMinWidthField.RegisterValueChangedCallback(evt =>
            {
                navigationMinWidth = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(navigationMinWidthField);

            var navigationMaxWidthField = new FloatField("Navigation Max")
            {
                value = navigationMaxWidth
            };
            navigationMaxWidthField.RegisterValueChangedCallback(evt =>
            {
                navigationMaxWidth = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(navigationMaxWidthField);

            var inspectorWidthField = new FloatField("Inspector Width")
            {
                value = inspectorWidth
            };
            inspectorWidthField.RegisterValueChangedCallback(evt =>
            {
                inspectorWidth = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(inspectorWidthField);

            var inspectorMinWidthField = new FloatField("Inspector Min")
            {
                value = inspectorMinWidth
            };
            inspectorMinWidthField.RegisterValueChangedCallback(evt =>
            {
                inspectorMinWidth = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(inspectorMinWidthField);

            var inspectorMaxWidthField = new FloatField("Inspector Max")
            {
                value = inspectorMaxWidth
            };
            inspectorMaxWidthField.RegisterValueChangedCallback(evt =>
            {
                inspectorMaxWidth = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(inspectorMaxWidthField);

            var contentMinWidthField = new FloatField("Content Min")
            {
                value = contentMinWidth
            };
            contentMinWidthField.RegisterValueChangedCallback(evt =>
            {
                contentMinWidth = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(contentMinWidthField);

            var navigationCollapsedToggle = new Toggle("Navigation Collapsed")
            {
                value = navigationCollapsed
            };
            navigationCollapsedToggle.RegisterValueChangedCallback(evt =>
            {
                navigationCollapsed = evt.newValue;
                refresh();
            });
            controls.Content.Add(navigationCollapsedToggle);

            var inspectorCollapsedToggle = new Toggle("Inspector Collapsed")
            {
                value = inspectorCollapsed
            };
            inspectorCollapsedToggle.RegisterValueChangedCallback(evt =>
            {
                inspectorCollapsed = evt.newValue;
                refresh();
            });
            controls.Content.Add(inspectorCollapsedToggle);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = 0f;
            surface.style.paddingRight = 0f;
            surface.style.paddingTop = 0f;
            surface.style.paddingBottom = 0f;
            surface.style.height = 360f;

            var layout = new AssetManagerWindowLayout();
            layout.style.flexGrow = 1f;
            layout.NavigationPaneContent.Add(new NavigationPanel());
            layout.ContentPaneContent.Add(new MainView());
            layout.InspectorPaneContent.Add(new InfomationPanel());
            layout.NavigationPaneWidthChanged += value =>
            {
                navigationWidth = value;
                navigationWidthField.SetValueWithoutNotify(value);
            };
            layout.InspectorPaneWidthChanged += value =>
            {
                inspectorWidth = value;
                inspectorWidthField.SetValueWithoutNotify(value);
            };
            layout.NavigationCollapsedChanged += value =>
            {
                navigationCollapsed = value;
                navigationCollapsedToggle.SetValueWithoutNotify(value);
            };
            layout.InspectorCollapsedChanged += value =>
            {
                inspectorCollapsed = value;
                inspectorCollapsedToggle.SetValueWithoutNotify(value);
            };

            surface.Add(layout);
            preview.Body.Add(surface);

            refresh = () =>
            {
                navigationWidthField.SetValueWithoutNotify(navigationWidth);
                navigationMinWidthField.SetValueWithoutNotify(navigationMinWidth);
                navigationMaxWidthField.SetValueWithoutNotify(navigationMaxWidth);
                inspectorWidthField.SetValueWithoutNotify(inspectorWidth);
                inspectorMinWidthField.SetValueWithoutNotify(inspectorMinWidth);
                inspectorMaxWidthField.SetValueWithoutNotify(inspectorMaxWidth);
                contentMinWidthField.SetValueWithoutNotify(contentMinWidth);
                navigationCollapsedToggle.SetValueWithoutNotify(navigationCollapsed);
                inspectorCollapsedToggle.SetValueWithoutNotify(inspectorCollapsed);

                layout.SetState(new AssetManagerWindowLayoutState(
                    navigationWidth,
                    inspectorWidth,
                    navigationMinWidth,
                    navigationMaxWidth,
                    contentMinWidth,
                    inspectorMinWidth,
                    inspectorMaxWidth,
                    navigationCollapsed,
                    inspectorCollapsed));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
