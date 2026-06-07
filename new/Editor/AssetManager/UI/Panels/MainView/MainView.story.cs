using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class AssetManagerMainViewCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 101; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Panels/MainView/main-view.uss");
                registry.RegisterStory(new StoryRegistration(
                    "asset-manager-main-view",
                    "Domain/AssetManager",
                    "MainView",
                    "AssetManager 中央領域の toolbar 以下だけを表す main view コンポーネントです。",
                    "layout 内では上部 toolbar の下に配置し、単体 window では toolbar と呼び出し側で合成する前提です。一覧、空状態、進行中タスク表示などを置くベース領域として扱います。",
                    new[] { "AssetItemGrid" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildAssetManagerMainViewStory(parent)));
            }
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
    }
}
