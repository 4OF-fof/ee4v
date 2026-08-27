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
                    "共通の境界線、背景、focus 表現を持つ text field です。読み取り専用を含む短文・長文編集で使用します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildInputFieldStory(parent),
                    new[]
                    {
                        "Editor/UI/Components/Inputs/CommaSeparatedListField/CommaSeparatedListField.cs",
                        "Editor/UI/Catalog/helper/Controls.cs",
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetManagerControls.cs"
                    }));
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

            var maxHeightField = UiTextFactory.CreateFloatField("Max Height");
            maxHeightField.value = maxHeight;
            maxHeightField.RegisterValueChangedCallback(evt =>
            {
                maxHeight = Mathf.Max(0f, evt.newValue);
                refresh();
            });
            controls.Content.Add(maxHeightField);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewArea();
            surface.style.width = 360f;

            var singleLineInput = new InputField(new InputFieldState(string.Empty, false, maxHeight, placeholder));
            singleLineInput.style.marginBottom = UiSpacingTokens.Xl;
            surface.Add(singleLineInput);

            var multilineInput = new InputField(new InputFieldState(string.Empty, true, maxHeight, placeholder));
            surface.Add(multilineInput);

            var readonlyInput = new InputField(new InputFieldState(
                "読み取り専用の複数行入力です。\n" +
                "この文章は表示領域より長くしてあります。\n" +
                "右側のスクロールバーをドラッグできます。\n" +
                "マウスホイールでも上下へ移動できます。\n" +
                "内容は編集できません。\n" +
                "長い説明文の途中も確認できます。\n" +
                "さらに下の行まで続きます。\n" +
                "スクロール位置が変わることを確認してください。\n" +
                "これが最後の行です。",
                true,
                maxHeight,
                placeholder))
            {
                IsReadOnly = true
            };
            readonlyInput.style.marginTop = UiSpacingTokens.Xl;
            surface.Add(readonlyInput);

            preview.Body.Add(surface);

            refresh = () =>
            {
                placeholderField.SetValueWithoutNotify(placeholder);
                maxHeightField.SetValueWithoutNotify(maxHeight);
                singleLineInput.SetPlaceholder(placeholder);
                multilineInput.SetPlaceholder(placeholder);
                singleLineInput.SetMaxHeight(maxHeight);
                multilineInput.SetMaxHeight(maxHeight);
                readonlyInput.SetMaxHeight(maxHeight);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
