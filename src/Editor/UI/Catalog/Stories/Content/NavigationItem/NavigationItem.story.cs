using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class NavigationItemCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 20;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "navigation-item", "Content", "NavigationItem",
                    "アイコン、名称、補足、選択状態を持つ移動項目です。",
                    "選択状態を切り替え、先頭と末尾へ補助表示や操作を追加できます。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildNavigationItemStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildNavigationItemStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var item = new NavigationItem(new NavigationItemState(
                "Animations", "12 items",
                IconState.FromBuiltinIcon(UiBuiltinIcon.Folder), true));
            item.Trailing.Add(new Badge(new BadgeState("12")));
            preview.Body.Add(item);
        }
    }
}
