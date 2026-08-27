using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private enum FormFieldStoryInputKind
        {
            Text,
            Toggle,
            Object,
            StringList
        }

        private sealed class FormFieldCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 35;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "form-field",
                    "Inputs",
                    "FormField",
                    "外枠へ重ねたラベルと、枠内の各種入力コンポーネント・任意ボタンを一つにまとめるフォーム項目です。",
                    "fieldset風の外枠へラベルを重ね、InputField、Toggle、ObjectField、StringListFieldと任意のボタンを同じ枠内に配置します。文字入力にはInputFieldを使用し、ToggleとObjectFieldにはUiTextFactoryの標準入力スタイルを使用します。各入力の外観はFormFieldの内外で共通です。複数の入力要素が必要な場合は、一つの入力コンポーネントとして渡します。値の保持と検証は入力コンポーネントが担当します。",
                    new[] { "InputField", "StringListField" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildFormFieldStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/Core/Presentation/Settings/SettingsUiRenderer.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/GestureAssignmentView.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildFormFieldStory(VisualElement parent)
        {
            var label = "名前";
            var showAction = true;
            var inputKind = FormFieldStoryInputKind.Text;
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
                    case FormFieldStoryInputKind.Toggle:
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
                    case FormFieldStoryInputKind.Object:
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
                    case FormFieldStoryInputKind.StringList:
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
                    ? UiTextFactory.CreateButton(
                        "保存",
                        () => result.SetText(formatResult()))
                    : null;
                fieldHost.Add(new FormField(label, input, save));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
