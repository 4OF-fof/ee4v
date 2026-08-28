using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ItemRowCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 16;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "item-row",
                    "Containers/Items",
                    "ItemRow",
                    "先頭要素、アイコン、名称、補足、末尾操作からなる1件分の行です。",
                    "名称と補足を横または縦に配置し、行の先頭と末尾へ操作や状態を追加できます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildItemRowStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/SearchableFileTree.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupView.cs",
                        "Editor/Feature/Avatar/PhysBoneCollider/UI/PhysBoneColliderWindow.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/Components/HiddenObjectTreeRow.cs",
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs",
                        "Editor/Feature/Project/ProjectTabs/UI/ProjectTabsView.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs",
                        "Editor/UI/Components/Content/NavigationItem/NavigationItem.cs"
                    }));
            }
        }

        private void BuildItemRowStory(VisualElement parent)
        {
            var title = "Hierarchy";
            var description = "UnityEditor.SceneHierarchyWindow";
            var layout = ItemRowLayout.Stacked;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "名称、補足、文字配置を変更して一件分の行を確認します。");
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
            var layoutField = AddEnumField(
                controls.Content,
                "配置",
                layout,
                value =>
                {
                    layout = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var row = new ItemRow();
            row.Leading.Add(UiTextFactory.CreateToggle());
            var result = UiTextFactory.Create(
                "末尾操作を選択してください。",
                UiClassNames.SecondaryText);
            row.Trailing.Add(new UiButton(
                "開く",
                () => result.SetText("開く操作を実行しました。")));
            preview.Body.Add(row);
            preview.Body.Add(result);

            refresh = () =>
            {
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                layoutField.SetValueWithoutNotify((Enum)(object)layout);
                row.SetState(new ItemRowState(
                    title,
                    description,
                    IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Scene,
                        UiSizeTokens.Size16),
                    layout));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
