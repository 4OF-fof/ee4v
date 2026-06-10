using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
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
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/ItemImage/item-image.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/ImageStack/image-stack.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Inputs/InputField/input-field.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Inputs/SearchField/search-field.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/Interactive/ViewToggleTabs/view-toggle-tabs.uss");
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
            }
        }

        private static void BuildAssetManagerInfomationPanelStory(CatalogWindow window, VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
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
