using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerInfomationPanelCatalogRegistrarStory
    {
        private sealed class AssetManagerInfomationPanelCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 102; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ItemImage/item-image.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ImageStack/image-stack.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Inputs/Button/ui-button.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Inputs/InputField/input-field.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Inputs/SearchField/search-field.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Content/Interactive/ViewToggleTabs/view-toggle-tabs.uss");
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "asset-manager-infomation-panel",
                    "Domain/AssetManager",
                    "InfomationPanel",
                    "AssetManager 右ペイン用の情報パネルコンポーネントです。",
                    "選択中アセットの詳細、プレビュー、検証結果の文脈を単体でも layout 内でも同じ構成で再利用する右ペイン component です。",
                    new string[0],
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildAssetManagerInfomationPanelStory(window, parent)));
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "asset-manager-asset-info",
                    "Domain/AssetManager",
                    "AssetInfo",
                    "AssetManager のアセット情報を表示して編集するコンポーネントです。",
                    "名前・説明・タグの自動保存と、データソースや登録ファイル数の確認状態を再現します。",
                    new string[0],
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildAssetInfoStory(window, parent)));
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "asset-manager-group-info",
                    "Domain/AssetManager",
                    "GroupInfo",
                    "Variant／Version Group自身の情報を表示して編集する状態です。",
                    "グループ名・説明と配下ファイルの情報を表示し、Item専用のタグとファイル追加を表示しない状態を再現します。",
                    new string[0],
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildGroupInfoStory(window, parent)));
            }
        }

        private static void BuildAssetManagerInfomationPanelStory(CatalogWindow window, VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = UiSpacingTokens.None;
            surface.style.paddingRight = UiSpacingTokens.None;
            surface.style.paddingTop = UiSpacingTokens.None;
            surface.style.paddingBottom = UiSpacingTokens.None;
            surface.style.height = 360f;

            var panel = new InfomationPanel();
            panel.style.flexGrow = 1f;
            surface.Add(panel);
            preview.Body.Add(surface);
        }

        private static void BuildAssetInfoStory(
            CatalogWindow window,
            VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.height = 460f;
            var view = new AssetInfoView();
            view.SetState(new AssetInfoState(
                "sample-item",
                "Sample Avatar",
                "Asset Info の編集状態を確認するサンプルです。",
                new[] { "Avatar", "Sample" },
                3,
                "ee4v, Eagle",
                "2026/07/01 10:00",
                "2026/07/31 18:30",
                availableTagNames: new[]
                {
                    "Avatar",
                    "BoothMeta",
                    "FREE",
                    "Sample",
                    "Swimwear",
                    "VRCAsset",
                    "ギプフェル対応"
                }));
            surface.Add(view);
            preview.Body.Add(surface);
        }

        private static void BuildGroupInfoStory(
            CatalogWindow window,
            VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.height = 460f;
            var view = new AssetInfoView();
            view.SetState(new AssetInfoState(
                "sample-variant-group",
                "PC",
                "PC向けのVariant Groupです。",
                new string[0],
                2,
                "ee4v",
                "2026/07/01 10:00",
                "2026/07/31 18:30",
                showTags: false,
                canAddFile: false));
            surface.Add(view);
            preview.Body.Add(surface);
        }
    }
}
