using System;
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
                    "Containers/Sections",
                    "SectionHeader",
                    "見出し、補足説明、右側の操作をまとめるコンポーネントです。",
                    "見出しと説明を左側に、セクションの操作を右側に配置します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSectionHeaderStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetDetailComponents.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/SearchableFileTree.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs",
                        "Editor/Feature/Avatar/PhysBoneCollider/UI/PhysBoneColliderWindow.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildSectionHeaderStory(VisualElement parent)
        {
            const string DefaultTitle = "表示設定";
            const string DefaultDescription =
                "この領域に適用する表示方法を選択します。";
            var title = DefaultTitle;
            var description = DefaultDescription;
            var showAction = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "見出し、説明、右側の操作領域を変更します。");
            var titleField = AddTextField(
                controls.Content,
                "見出し",
                title,
                value =>
                {
                    title = value;
                    refresh();
                });
            var descriptionField = AddTextField(
                controls.Content,
                "説明",
                description,
                value =>
                {
                    description = value;
                    refresh();
                });
            var actionToggle = AddToggle(
                controls.Content,
                "操作を表示",
                showAction,
                value =>
                {
                    showAction = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var header = new SectionHeader();
            var reset = UiTextFactory.CreateButton(
                "リセット",
                () =>
                {
                    title = DefaultTitle;
                    description = DefaultDescription;
                    refresh();
                });
            header.Actions.Add(reset);
            preview.Body.Add(header);

            refresh = () =>
            {
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                actionToggle.SetValueWithoutNotify(showAction);
                header.SetTitle(title);
                header.SetDescription(description);
                reset.style.display = showAction
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
