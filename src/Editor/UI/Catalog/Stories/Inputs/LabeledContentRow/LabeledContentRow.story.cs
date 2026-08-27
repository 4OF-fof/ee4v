using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class LabeledContentRowCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 35;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "labeled-content-row",
                    "Containers",
                    "LabeledContentRow",
                    "ラベル、入力内容、補助操作を横に並べるフォーム行です。",
                    "ラベル、入力内容、補助操作を一行へ整理して配置します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildLabeledContentRowStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetDetailComponents.cs",
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/Core/Presentation/Settings/SettingsUiRenderer.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildLabeledContentRowStory(VisualElement parent)
        {
            var label = "名前";
            var showAction = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "ラベルと補助操作の有無を変更し、フォーム行の配置を確認します。");
            var labelField = AddTextField(
                controls.Content,
                "ラベル",
                label,
                value =>
                {
                    label = value;
                    refresh();
                });
            var actionToggle = AddToggle(
                controls.Content,
                "補助操作を表示",
                showAction,
                value =>
                {
                    showAction = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var row = new LabeledContentRow();
            var input = UiTextFactory.CreateTextField();
            input.SetValueWithoutNotify("Main Window");
            row.Content.Add(input);
            var result = UiTextFactory.Create(
                "入力内容はまだ保存されていません。",
                UiClassNames.SecondaryText);
            var save = UiTextFactory.CreateButton(
                "保存",
                () => result.SetText("「" + input.value + "」を保存しました。"));
            row.Actions.Add(save);
            preview.Body.Add(row);
            preview.Body.Add(result);

            refresh = () =>
            {
                labelField.SetValueWithoutNotify(label);
                actionToggle.SetValueWithoutNotify(showAction);
                row.SetLabel(label);
                save.style.display = showAction
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
