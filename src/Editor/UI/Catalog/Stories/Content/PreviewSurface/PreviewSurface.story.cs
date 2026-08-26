using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class PreviewSurfaceCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 18;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "preview-surface", "Content", "PreviewSurface",
                    "内容、空表示、重ねる操作を持つプレビュー面です。",
                    "Content、Placeholder、Overlayを重ね、内容の有無に応じて表示を切り替えます。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildPreviewSurfaceStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/Components/AssetItemGridCard.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/Components/GestureAssignmentCell.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs"
                    }));
            }
        }

        private void BuildPreviewSurfaceStory(VisualElement parent)
        {
            var hasContent = true;
            var controls = CreatePlainControlsSection(
                parent,
                "内容の有無を切り替え、ContentとPlaceholderの表示を確認します。");
            Toggle contentToggle = null;

            var preview = CreatePreviewSection(parent);
            var surface = new PreviewSurface();
            surface.style.width = 240f;
            surface.style.height = 140f;
            surface.Placeholder.Add(new EmptyState(new EmptyStateState(
                string.Empty, "プレビューがありません")));
            surface.Content.Add(UiTextFactory.Create("Preview content"));
            surface.Overlay.Add(UiTextFactory.CreateButton(
                "切り替え",
                () =>
                {
                    hasContent = !hasContent;
                    contentToggle.SetValueWithoutNotify(hasContent);
                    surface.SetHasContent(hasContent);
                }));
            preview.Body.Add(surface);

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
