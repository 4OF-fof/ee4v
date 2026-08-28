using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private enum FormInputStoryInputKind
        {
            Text,
            Toggle,
            Object,
            StringList
        }

        private sealed class FormInputCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 35;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "form-input",
                    "Inputs",
                    "FormInput",
                    "ラベル、入力コンポーネント、任意ボタンを横一列に配置するフォーム入力です。",
                    "囲み枠を持たず、InputField、Toggle、ObjectField、StringListFieldのいずれか一つと任意ボタンをラベルの右側へ配置します。値の保持と検証は入力コンポーネントが担当します。複数の入力を一つの枠へまとめる場合はInputGroupを使用します。",
                    new[] { "InputField", "StringListField" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildFormInputStory(parent),
                    new[]
                    {
                        "Editor/UI/Catalog/helper/Controls.cs",
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/Core/Presentation/Settings/SettingsUiRenderer.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildFormInputStory(VisualElement parent)
        {
            var label = "名前";
            var showAction = true;
            var inputKind = FormInputStoryInputKind.Text;
            var textValue = "Main Window";
            var toggleValue = true;
            UnityEngine.Object objectValue = null;
            IReadOnlyList<string> listValues =
                new[] { "Airi", "Manuka", "Moe" };
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "入力種別、ラベル、ボタンの有無を変更し、フォーム項目全体の外観を確認します。");
            var inputKindField = AddEnumField(
                controls.Content,
                "入力種別",
                inputKind,
                value =>
                {
                    inputKind = value;
                    refresh();
                });
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
                "ボタンを表示",
                showAction,
                value =>
                {
                    showAction = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var fieldHost = new VisualElement();
            var result = UiTextFactory.Create(
                "入力内容はまだ保存されていません。",
                UiClassNames.SecondaryText);
            preview.Body.Add(fieldHost);
            preview.Body.Add(result);

            refresh = () =>
            {
                inputKindField.SetValueWithoutNotify(inputKind);
                labelField.SetValueWithoutNotify(label);
                actionToggle.SetValueWithoutNotify(showAction);
                fieldHost.Clear();

                VisualElement input;
                Func<string> formatResult;
                switch (inputKind)
                {
                    case FormInputStoryInputKind.Toggle:
                    {
                        var toggle = UiTextFactory.CreateToggle();
                        toggle.SetValueWithoutNotify(toggleValue);
                        toggle.RegisterValueChangedCallback(
                            evt => toggleValue = evt.newValue);
                        input = toggle;
                        formatResult = () => toggleValue
                            ? "有効を保存しました。"
                            : "無効を保存しました。";
                        break;
                    }
                    case FormInputStoryInputKind.Object:
                    {
                        var objectField = UiTextFactory.CreateObjectField();
                        objectField.objectType = typeof(GameObject);
                        objectField.allowSceneObjects = true;
                        objectField.SetValueWithoutNotify(objectValue);
                        objectField.RegisterValueChangedCallback(
                            evt => objectValue = evt.newValue);
                        input = objectField;
                        formatResult = () => objectValue == null
                            ? "未選択を保存しました。"
                            : "「" + objectValue.name + "」を保存しました。";
                        break;
                    }
                    case FormInputStoryInputKind.StringList:
                    {
                        var listField = new StringListField(
                            new StringListFieldState(
                                listValues,
                                itemPlaceholder: "項目を入力",
                                addItemLabel: "項目を追加",
                                removeItemTooltip: "項目を削除"));
                        listField.ValuesChanged += values =>
                            listValues = values;
                        input = listField;
                        formatResult = () =>
                            "「" +
                            string.Join(", ", listValues) +
                            "」を保存しました。";
                        break;
                    }
                    default:
                    {
                        var textField = new InputField();
                        textField.SetValueWithoutNotify(textValue);
                        textField.ValueChanged +=
                            value => textValue = value ?? string.Empty;
                        input = textField;
                        formatResult = () =>
                            "「" + textValue + "」を保存しました。";
                        break;
                    }
                }

                var save = showAction
                    ? new UiButton(
                        "保存",
                        () => result.SetText(formatResult()))
                    : null;
                fieldHost.Add(new FormInput(label, input, save));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
