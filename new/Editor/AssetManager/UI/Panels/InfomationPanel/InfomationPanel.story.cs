using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class AssetManagerInfomationPanelCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 102; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss");
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
            }
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
