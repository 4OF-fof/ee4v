using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class PreviewContainerCatalogRegistrar :
            ICatalogRegistrar
        {
            public int Order => 18;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "preview-container", "Containers", "PreviewContainer",
                    "内容、空表示、重ねる操作を配置するプレビュー用コンテナです。",
                    "描画は行わず、Content、Placeholder、Overlayの3層と表示切り替えだけを担当します。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildPreviewContainerStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/Components/AssetItemGridCard.cs",
                        "Editor/AssetManager/UI/Components/AssetThumbnailStack.cs",
                        "Editor/AssetManager/UI/DerivedAssetPrefabPickerWindow.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/Components/GestureAssignmentCell.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Avatar/PhysBoneCollider/UI/PhysBoneColliderWindow.cs"
                    }));
            }
        }

        private void BuildPreviewContainerStory(VisualElement parent)
        {
            var hasContent = true;
            var controls = CreatePlainControlsSection(
                parent,
                "内容の有無を切り替え、ContentとPlaceholderの表示を確認します。");
            Toggle contentToggle = null;

            var preview = CreatePreviewSection(parent);
            var surface = new PreviewContainer();
            surface.AddToClassList(
                "ee4v-ui-catalog-preview-container");
            surface.Placeholder.Add(new EmptyState(new EmptyStateState(
                string.Empty, "プレビューがありません")));
            surface.Content.Add(UiTextFactory.Create(
                "Preview content",
                UiClassNames.SecondaryText,
                "ee4v-ui-catalog-preview-container__content"));
            var toggleButton = UiTextFactory.CreateButton(
                "切り替え",
                () =>
                {
                    hasContent = !hasContent;
                    contentToggle.SetValueWithoutNotify(hasContent);
                    surface.SetHasContent(hasContent);
                });
            toggleButton.AddToClassList(
                "ee4v-ui-catalog-preview-container__action");
            surface.Overlay.Add(toggleButton);
            preview.Body.Add(CreatePreviewArea(surface, true));

            contentToggle = AddToggle(
                controls.Content,
                "内容を表示",
                hasContent,
                value =>
                {
                    hasContent = value;
                    surface.SetHasContent(hasContent);
                });
            surface.SetHasContent(hasContent);
            FinalizeControlsSection(parent, controls);
        }
    }
}
