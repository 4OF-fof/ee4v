using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class MainToolbarCatalogRegistrarStory
    {
        private sealed class MainToolbarCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 103; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Toolbar/MainToolbar/main-toolbar.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Inputs/SearchField/search-field.uss");
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Inputs/NumericSlider/numeric-slider.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "main-toolbar",
                    "Domain/AssetManager",
                    "MainToolbar",
                    "AssetManager main view 上部に置く、横並びの toolbar です。",
                    "history navigation を先頭に持ち、呼び出し側が追加 action を Content slot に足せる構成です。",
                    new[] { "HistoryNavigation", "NumericSlider", "SearchField", "Icon" },
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildMainToolbarStory(window, parent)));
            }
        }

        private static void BuildMainToolbarStory(CatalogWindow window, VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            var toolbar = new MainToolbar();
            surface.Add(toolbar);
            preview.Body.Add(surface);
        }
    }
}
