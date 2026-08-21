using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ActionBarCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 19;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "action-bar", "Content", "ActionBar",
                    "主要内容と操作を左右へ配置する共通バーです。",
                    "左側の主要内容、中央内容、右側の操作を並べ、ToolbarやFooterの配置を整えます。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildActionBarStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsFooter.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsToolbar.cs"
                    }));
            }
        }

        private void BuildActionBarStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var bar = new ActionBar();
            bar.Leading.Add(UiTextFactory.Create("3件を選択中"));
            bar.Actions.Add(UiTextFactory.CreateButton("解除", () => { }));
            bar.Actions.Add(UiTextFactory.CreateButton("適用", () => { }));
            preview.Body.Add(bar);
        }
    }
}
