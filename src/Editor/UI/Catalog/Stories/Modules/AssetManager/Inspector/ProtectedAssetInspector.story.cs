using Ee4v.AssetManager.UI;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class
        ProtectedAssetInspectorCatalogStory
    {
        private sealed class Registrar :
            CatalogWindow.ICatalogRegistrar
        {
            public int Order => 111;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/Button/ui-button.uss");
                registry.RegisterStyleSheet(
                    "Editor/AssetManager/UI/Inspector/protected-asset-inspector.uss");
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        "asset-manager-protected-inspector",
                        "Domain/AssetManager/Screens",
                        "Protected Asset Inspector Overlay",
                        "保護された AssetManager 原本の通常 Inspector に重ねる警告です。",
                        "Variant またはコピーを促す案内と、保護解除、警告解除を表示します。",
                        new[] { "UiButton" },
                        CatalogWindow
                            .ComponentImplementationKind
                            .UiToolkit,
                        Build));
            }
        }

        private static void Build(
            CatalogWindow window,
            VisualElement parent)
        {
            var preview =
                window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.height = 420f;
            surface.style.paddingLeft = 0f;
            surface.style.paddingRight = 0f;
            surface.style.paddingTop = 0f;
            surface.style.paddingBottom = 0f;
            var inspector = new VisualElement();
            inspector.style.paddingLeft = 16f;
            inspector.style.paddingRight = 16f;
            inspector.style.paddingTop = 16f;
            inspector.Add(UiTextFactory.Create(
                "BaseMaterial",
                UiClassNames.SectionTitle));
            inspector.Add(UiTextFactory.Create(
                "Shader: Standard",
                UiClassNames.SecondaryText));
            surface.Add(inspector);
            surface.Add(
                new ProtectedAssetInspectorView(
                    () => { },
                    () => { }));
            preview.Body.Add(surface);
        }
    }
}
