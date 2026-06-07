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
        private sealed class AssetManagerCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 100; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/panels.uss");
                registry.RegisterStyleSheet("Editor/AssetManager/UI/toolbar.uss");

                registry.RegisterStory(new StoryRegistration(
                    "asset-manager-navigation-panel",
                    "Domain/AssetManager",
                    "NavigationPanel",
                    "AssetManager 左ペイン用の navigation コンポーネントです。",
                    "カテゴリ、ソース、保存済みビューのような左側導線を単体で再利用できるようにした panel component です。ThreePaneLayout の左ペインにも、単体 window にも同じものを載せます。",
                    new[]
                    {
                        "InfoCard"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAssetManagerNavigationPanelStory(parent)));

                registry.RegisterStory(new StoryRegistration(
                    "asset-manager-main-view",
                    "Domain/AssetManager",
                    "MainView",
                    "AssetManager 中央領域の toolbar 以下だけを表す main view コンポーネントです。",
                    "layout 内では上部 toolbar の下に配置し、単体 window では toolbar と呼び出し側で合成する前提です。一覧、空状態、進行中タスク表示などを置くベース領域として扱います。",
                    new[]
                    {
                        "InfoCard"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAssetManagerMainViewStory(parent)));

                registry.RegisterStory(new StoryRegistration(
                    "asset-manager-infomation-panel",
                    "Domain/AssetManager",
                    "InfomationPanel",
                    "AssetManager 右ペイン用の情報パネルコンポーネントです。",
                    "選択中アセットの詳細、プレビュー、検証結果の文脈を単体でも layout 内でも同じ構成で再利用する右ペイン component です。",
                    new[]
                    {
                        "InfoCard"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAssetManagerInfomationPanelStory(parent)));

                registry.RegisterStory(new StoryRegistration(
                    "asset-manager-toolbar",
                    "Domain/AssetManager",
                    "AssetManagerToolbar",
                    "AssetManager main view 上部に置く、横並びの toolbar コンテナです。",
                    "現時点では中身を持たない container-only component です。呼び出し側が search、filter、action button などを Content slot に追加して使う前提です。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAssetManagerToolbarStory(parent)));

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

    }
}
