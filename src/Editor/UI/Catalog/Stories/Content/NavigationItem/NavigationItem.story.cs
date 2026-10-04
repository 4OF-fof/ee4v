using System;
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
                    "navigation-item", "Inputs", "NavigationItem",
                    "アイコン、名称、補足、選択状態を持つ移動項目です。",
                    "選択状態を切り替え、先頭と末尾へ補助表示や操作を追加できます。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildNavigationItemStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Navigation.cs",
                        "Editor/UI/Components/Inputs/PrefabSelector/PrefabSelector.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildNavigationItemStory(VisualElement parent)
        {
            var title = "Animations";
            var description = "12 items";
            var selected = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "名称、補足、選択状態を変更します。Previewの項目を押しても選択状態を切り替えられます。");
            var titleField = AddTextField(
                controls.Content,
                "名称",
                title,
                value =>
                {
                    title = value;
                    refresh();
                });
            var descriptionField = AddTextField(
                controls.Content,
                "補足",
                description,
                value =>
                {
                    description = value;
                    refresh();
                });
            Toggle selectedToggle = null;
            selectedToggle = AddToggle(
                controls.Content,
                "選択",
                selected,
                value =>
                {
                    selected = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            NavigationItem item = null;
            item = new NavigationItem(onClick: () =>
            {
                selected = !selected;
                selectedToggle.SetValueWithoutNotify(selected);
                item.SetSelected(selected);
            });
            item.Trailing.Add(new Badge("12"));
            preview.Body.Add(item);

            refresh = () =>
            {
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                selectedToggle.SetValueWithoutNotify(selected);
                item.SetState(new NavigationItemState(
                    title,
                    description,
                    FluentUiIcons.CreateState("folder.png"),
                    selected));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
