using System;
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
                    "action-bar", "Containers", "ActionBar",
                    "主要内容と操作を左右へ配置する共通バーです。",
                    "左側の主要内容、中央内容、右側の操作を並べ、ToolbarやFooterの配置を整えます。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildActionBarStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Navigation.cs",
                        "Editor/AssetManager/UI/Components/AssetFilterEditor.cs",
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
            var leadingText = "3件を選択中";
            var centerText = "並び順: 名前";
            var showCenter = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "各領域の内容を変更し、左右と中央の配置を確認します。");
            var leadingField = AddTextField(
                controls.Content,
                "左側",
                leadingText,
                value =>
                {
                    leadingText = value;
                    refresh();
                });
            var centerField = AddTextField(
                controls.Content,
                "中央",
                centerText,
                value =>
                {
                    centerText = value;
                    refresh();
                });
            var centerToggle = AddToggle(
                controls.Content,
                "中央領域を表示",
                showCenter,
                value =>
                {
                    showCenter = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var bar = new ActionBar();
            var leading = UiTextFactory.Create();
            var center = UiTextFactory.Create();
            var result = UiTextFactory.Create(
                "操作を選択してください。",
                UiClassNames.SecondaryText);
            bar.Leading.Add(leading);
            bar.Center.Add(center);
            bar.Actions.Add(new UiButton(
                "解除",
                () => result.SetText("選択を解除しました。")));
            bar.Actions.Add(new UiButton(
                "適用",
                () => result.SetText("変更を適用しました。")));
            preview.Body.Add(bar);
            preview.Body.Add(result);

            refresh = () =>
            {
                leadingField.SetValueWithoutNotify(leadingText);
                centerField.SetValueWithoutNotify(centerText);
                centerToggle.SetValueWithoutNotify(showCenter);
                leading.SetText(leadingText);
                center.SetText(centerText);
                bar.Center.style.display = showCenter
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
