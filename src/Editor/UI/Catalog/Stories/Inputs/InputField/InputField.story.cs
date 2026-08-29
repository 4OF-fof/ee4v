using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private enum InputFieldStoryInputKind
        {
            SingleLineText,
            MultilineText,
            Toggle,
            Object,
            Dropdown
        }

        private sealed class InputFieldCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 32; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Inputs/InputField/input-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "input-field",
                    "Inputs",
                    "InputField",
                    "テキスト、トグル、Object選択、ドロップダウンの標準スタイルをまとめて確認する単一値入力のStoryです。",
                    "Catalog上の単一値入力はInputField Storyへ統一します。文字列にはInputFieldを使用し、その他の値にはUiTextFactoryが生成する型付き入力を使用します。単行テキストとドロップダウンは透明背景の下線型、複数行テキストは全周枠のテキストエリア型です。トグルは標準入力と同じ境界線と操作色を使う小型チェックボックスとし、選択時は青い面と白いチェックマークで状態を示します。フォーカスの有無では外観を変えません。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildInputFieldStory(parent),
                    new[]
                    {
                        "Editor/UI/Components/Inputs/ListField/ListField.cs",
                        "Editor/UI/Catalog/helper/Controls.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetManagerControls.cs"
                    }));
            }
        }

        private void BuildInputFieldStory(VisualElement parent)
        {
            var inputKind = InputFieldStoryInputKind.SingleLineText;
            var placeholder = "Type text";
            var maxHeight = 120f;
            var readOnly = false;
            var toggleValue = false;
            UnityEngine.Object objectValue = null;
            var dropdownOptions = new List<string>
            {
                "Compact",
                "Standard",
                "Comfortable"
            };
            var dropdownValue = dropdownOptions[1];
            Action refresh = null;

            var controls = CreatePlainControlsSection(
                parent,
                "入力種別を切り替えて、テキスト、トグル、Object選択、ドロップダウンの標準スタイルを確認します。");
            var inputKindField = AddEnumField(
                controls.Content,
                "入力種別",
                inputKind,
                value =>
                {
                    inputKind = value;
                    refresh();
                });
            var placeholderField = AddTextField(controls.Content, "Placeholder", placeholder, nextValue =>
            {
                placeholder = nextValue;
                refresh();
            }, placeholder: "Placeholder text");

            var maxHeightField = AddFloatField(
                controls.Content,
                "Max Height",
                maxHeight,
                value =>
                {
                    maxHeight = Mathf.Max(0f, value);
                    refresh();
                });
            var readOnlyField = AddToggle(
                controls.Content,
                "読み取り専用",
                readOnly,
                value =>
                {
                    readOnly = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewArea();
            surface.style.width = 360f;
            var fieldHost = new VisualElement();
            surface.Add(fieldHost);

            preview.Body.Add(surface);

            refresh = () =>
            {
                inputKindField.SetValueWithoutNotify(inputKind);
                placeholderField.SetValueWithoutNotify(placeholder);
                maxHeightField.SetValueWithoutNotify(maxHeight);
                readOnlyField.SetValueWithoutNotify(readOnly);

                var isText =
                    inputKind == InputFieldStoryInputKind.SingleLineText ||
                    inputKind == InputFieldStoryInputKind.MultilineText;
                SetControlVisible(placeholderField, isText);
                SetControlVisible(
                    maxHeightField,
                    inputKind == InputFieldStoryInputKind.MultilineText);
                SetControlVisible(readOnlyField, isText);

                fieldHost.Clear();
                switch (inputKind)
                {
                    case InputFieldStoryInputKind.MultilineText:
                    {
                        var value = readOnly
                            ? "読み取り専用の複数行入力です。\n内容は編集できません。"
                            : string.Empty;
                        var field = new InputField(new InputFieldState(
                            value,
                            true,
                            maxHeight,
                            placeholder))
                        {
                            IsReadOnly = readOnly
                        };
                        fieldHost.Add(field);
                        break;
                    }
                    case InputFieldStoryInputKind.Toggle:
                    {
                        var field = UiTextFactory.CreateToggle();
                        field.SetValueWithoutNotify(toggleValue);
                        field.RegisterValueChangedCallback(evt =>
                        {
                            toggleValue = evt.newValue;
                        });
                        fieldHost.Add(field);
                        break;
                    }
                    case InputFieldStoryInputKind.Object:
                    {
                        var field = UiTextFactory.CreateObjectField();
                        field.objectType = typeof(GameObject);
                        field.allowSceneObjects = true;
                        field.SetValueWithoutNotify(objectValue);
                        field.RegisterValueChangedCallback(evt =>
                        {
                            objectValue = evt.newValue;
                        });
                        fieldHost.Add(field);
                        break;
                    }
                    case InputFieldStoryInputKind.Dropdown:
                    {
                        var selectedIndex = Mathf.Max(
                            0,
                            dropdownOptions.IndexOf(dropdownValue));
                        var field = UiTextFactory.CreatePopupField(
                            string.Empty,
                            dropdownOptions,
                            selectedIndex);
                        field.RegisterValueChangedCallback(evt =>
                        {
                            dropdownValue = evt.newValue;
                        });
                        fieldHost.Add(field);
                        break;
                    }
                    default:
                    {
                        var value = readOnly
                            ? "読み取り専用の単行入力"
                            : string.Empty;
                        var field = new InputField(new InputFieldState(
                            value,
                            false,
                            maxHeight,
                            placeholder))
                        {
                            IsReadOnly = readOnly
                        };
                        fieldHost.Add(field);
                        break;
                    }
                }
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
