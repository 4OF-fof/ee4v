using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class CopyableTextAreaCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Display/CopyableTextArea/copyable-text-area.uss");
                registry.RegisterStory(new StoryRegistration(
                    "copyable-text-area",
                    "Display",
                    "CopyableTextArea",
                    "長文の確認結果を選択・コピーできる、読み取り専用のテキスト領域コンポーネントです。",
                    "右上に copy button を持つ readonly multiline text field です。テスト詳細や監査ログのような長文を表示し、そのまま clipboard へ渡す用途を想定しています。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildCopyableTextAreaStory(parent)));
            }
        }

        private void BuildCopyableTextAreaStory(VisualElement parent)
        {
            var text = "ja-JP/Core: testing.window.failureDetailsTitle (Editor/Core/Localization/ja-JP/core.jsonc)\n" +
                       "en-US/Core: testing.window.copy (Editor/Core/Localization/en-US/core.jsonc)";
            var buttonText = "Copy";
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "表示する長文と button text を変えながら、詳細結果表示用 text area を確認します。");
            var textField = AddTextField(controls.Content, "Text", text, nextValue =>
            {
                text = nextValue;
                refresh();
            }, true, 140f);
            var buttonField = AddTextField(controls.Content, "Button", buttonText, nextValue =>
            {
                buttonText = nextValue;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var textArea = new CopyableTextArea();
            preview.Body.Add(textArea);

            refresh = () =>
            {
                textField.SetValueWithoutNotify(text);
                buttonField.SetValueWithoutNotify(buttonText);
                textArea.SetState(new CopyableTextAreaState(text, buttonText));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
