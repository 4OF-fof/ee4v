using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class AssetManagerToolbarCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 103; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Toolbar/AssetManagerToolbar/asset-manager-toolbar.uss");
                registry.RegisterStory(new StoryRegistration(
                    "asset-manager-toolbar",
                    "Domain/AssetManager",
                    "AssetManagerToolbar",
                    "AssetManager main view 上部に置く、横並びの toolbar コンテナです。",
                    "現時点では中身を持たない container-only component です。呼び出し側が search、filter、action button などを Content slot に追加して使う前提です。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAssetManagerToolbarStory(parent)));
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
    }
}
