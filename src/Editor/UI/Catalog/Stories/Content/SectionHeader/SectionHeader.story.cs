using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class SectionHeaderCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 15;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "section-header",
                    "Content",
                    "SectionHeader",
                    "見出し、補足説明、右側の操作をまとめるコンポーネントです。",
                    "見出しと説明を左側に、セクションの操作を右側に配置します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSectionHeaderStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetDetailComponents.cs",
                        "Editor/AssetManager/UI/SearchableFileTree.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildSectionHeaderStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var header = new SectionHeader(
                "表示設定",
                "この領域に適用する表示方法を選択します。");
            header.Actions.Add(UiTextFactory.CreateButton(
                "リセット",
                () => { }));
            preview.Body.Add(header);
        }
    }
}
