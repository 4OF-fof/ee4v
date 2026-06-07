using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class AssetManagerNavigationPanelCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 100; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Panels/NavigationPanel/navigation-panel.uss");
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
            }
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
    }
}
