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
            var preview = CreatePreviewSection(parent);
            var surface = new PreviewSurface();
            surface.style.width = 240f;
            surface.style.height = 140f;
            surface.Placeholder.Add(new EmptyState(new EmptyStateState(
                string.Empty, "プレビューがありません")));
            surface.Content.Add(UiTextFactory.Create("Preview content"));
            surface.Overlay.Add(UiTextFactory.CreateButton("更新", () => { }));
            surface.SetHasContent(true);
            preview.Body.Add(surface);
        }
    }
}
