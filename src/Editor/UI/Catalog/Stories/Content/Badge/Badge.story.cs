using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class BadgeCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 22;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "badge", "Content", "Badge",
                    "件数や短い補足を表示する中立的なバッジです。",
                    "処理状態に限定せず、一覧項目の件数などに使用します。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildBadgeStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupView.cs"
                    }));
            }
        }

        private void BuildBadgeStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var row = new ActionBar();
            row.Leading.Add(new Badge(new BadgeState("12")));
            row.Leading.Add(new Badge(new BadgeState(
                "保存済み", UiStatusTone.Passed)));
            preview.Body.Add(row);
        }
    }
}
