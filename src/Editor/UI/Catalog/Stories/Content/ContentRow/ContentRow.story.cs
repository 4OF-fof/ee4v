using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ContentRowCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 16;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "content-row",
                    "Content",
                    "ContentRow",
                    "先頭要素、アイコン、名称、補足、末尾操作からなる1件分の行です。",
                    "名称と補足を横または縦に配置し、行の先頭と末尾へ操作や状態を追加できます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildContentRowStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/SearchableFileTree.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectTreeView.cs",
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildContentRowStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var row = new ContentRow(new ContentRowState(
                "Hierarchy",
                "UnityEditor.SceneHierarchyWindow",
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Scene,
                    UiSizeTokens.Size16),
                ContentRowLayout.Stacked));
            row.Leading.Add(UiTextFactory.CreateToggle());
            row.Trailing.Add(UiTextFactory.CreateButton(
                "開く",
                () => { }));
            preview.Body.Add(row);
        }
    }
}
