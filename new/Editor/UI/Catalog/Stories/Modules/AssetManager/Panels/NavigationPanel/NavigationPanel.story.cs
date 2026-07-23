using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerNavigationPanelCatalogRegistrarStory
    {
        private sealed class AssetManagerNavigationPanelCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 100; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Panels/NavigationPanel/navigation-panel.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "asset-manager-navigation-panel",
                    "Domain/AssetManager",
                    "NavigationPanel",
                    "AssetManager 左ペイン用の navigation コンポーネントです。",
                    "カテゴリ、ソース、保存済みビューのような左側導線を単体で再利用できるようにした panel component です。ThreePaneLayout の左ペインにも、単体 window にも同じものを載せます。",
                    new[] { "SingleSelectButtonGroup" },
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildAssetManagerNavigationPanelStory(window, parent)));
            }
        }

        private static void BuildAssetManagerNavigationPanelStory(CatalogWindow window, VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = UiSpacingTokens.None;
            surface.style.paddingRight = UiSpacingTokens.None;
            surface.style.paddingTop = UiSpacingTokens.None;
            surface.style.paddingBottom = UiSpacingTokens.None;
            surface.style.height = 360f;

            var panel = new NavigationPanel();
            panel.style.flexGrow = 1f;
            surface.Add(panel);
            preview.Body.Add(surface);
        }
    }
}
