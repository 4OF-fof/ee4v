using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
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
                    "1行または複数行のテキストを編集する汎用入力コンポーネントです。",
                    "SearchField や UrlInputField と同じ境界線、背景、focus 表現を持つ text field です。Catalog の短文/長文編集 control でも使用します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildInputFieldStory(parent)));
            }
        }

        private void BuildInputFieldStory(VisualElement parent)
        {
            var placeholder = "Type text";
            var maxHeight = 120f;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "InputField の表示パラメータを変更して、1行/複数行と placeholder の見た目を確認します。");
            var placeholderField = AddTextField(controls.Content, "Placeholder", placeholder, nextValue =>
            {
                placeholder = nextValue;
                refresh();
            }, placeholder: "Placeholder text");

            var maxHeightField = new FloatField("Max Height")
            {
                value = maxHeight
            };
            maxHeightField.RegisterValueChangedCallback(evt =>
            {
                maxHeight = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(maxHeightField);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.width = 360f;

            var singleLineInput = new InputField(new InputFieldState(string.Empty, false, maxHeight, placeholder));
            singleLineInput.style.marginBottom = 12f;
            surface.Add(singleLineInput);

            var multilineInput = new InputField(new InputFieldState(string.Empty, true, maxHeight, placeholder));
            surface.Add(multilineInput);

            preview.Body.Add(surface);

            refresh = () =>
            {
                placeholderField.SetValueWithoutNotify(placeholder);
                maxHeightField.SetValueWithoutNotify(maxHeight);
                singleLineInput.SetPlaceholder(placeholder);
                multilineInput.SetPlaceholder(placeholder);
                singleLineInput.SetMaxHeight(maxHeight);
                multilineInput.SetMaxHeight(maxHeight);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
