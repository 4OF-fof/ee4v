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
                        "Protected Asset Inspector",
                        "保護された AssetManager 原本を選択したときの専用 Inspector です。",
                        "Material / Prefab Variant と編集可能コピーへの非破壊導線を表示します。",
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
            surface.Add(
                new ProtectedAssetInspectorView(
                    new ProtectedAssetInspectorViewState
                    {
                        AssetName = "BaseMaterial.mat",
                        AssetPath =
                            "Assets/AssetManager/Materials/BaseMaterial.mat",
                        CanCreateMaterialVariant = true,
                        CanCreateEditableCopy = true
                    },
                    () => { },
                    () => { },
                    () => { },
                    () => { }));
            preview.Body.Add(surface);
        }
    }
}
