using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal static class AssetManagerToolbarCatalogRegistrarStory
    {
        private sealed class AssetManagerToolbarCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 103; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Toolbar/AssetManagerToolbar/asset-manager-toolbar.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "asset-manager-toolbar",
                    "Domain/AssetManager",
                    "AssetManagerToolbar",
                    "AssetManager main view 上部に置く、横並びの toolbar コンテナです。",
                    "現時点では中身を持たない container-only component です。呼び出し側が search、filter、action button などを Content slot に追加して使う前提です。",
                    new string[0],
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildAssetManagerToolbarStory(window, parent)));
            }
        }

        private static void BuildAssetManagerToolbarStory(CatalogWindow window, VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            var toolbar = new AssetManagerToolbar();
            surface.Add(toolbar);
            preview.Body.Add(surface);
        }
    }
}
