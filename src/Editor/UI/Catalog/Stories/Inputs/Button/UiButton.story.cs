using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class UiButtonCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 5; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/Button/ui-button.uss");
                registry.RegisterStory(new StoryRegistration(
                    "ui-button",
                    "Inputs",
                    "UiButton",
                    "共通の文字描画、状態、余白を持つbuttonコンポーネントです。",
                    "文字はUiTextFactoryのIMGUI fallbackを使い、solid / ghost、icon、meta、selected、compactを同じ操作表現で確認できます。",
                    new[] { "Icon" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildUiButtonStory(parent)));
            }
        }

        private void BuildUiButtonStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            surface.style.width = 280f;

            var column = new VisualElement();
            column.style.flexGrow = 1f;

            column.Add(new UiButton(
                new UiButtonState(
                    "Primary action",
                    iconState: IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Package,
                        UiSizeTokens.Size12))));
            column.Add(new UiButton(
                new UiButtonState(
                    "Selected navigation",
                    "12",
                    iconState: IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Store,
                        UiSizeTokens.Size12),
                    selected: true,
                    variant: UiButtonVariant.Ghost)));
            column.Add(new UiButton(
                new UiButtonState(
                    tooltip: "Icon action",
                    iconState: IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Refresh,
                        UiSizeTokens.Size12),
                    variant: UiButtonVariant.Ghost,
                    size: UiButtonSize.Compact)));

            surface.Add(column);
            preview.Body.Add(surface);
        }
    }
}
