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
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Interactive/InputField/input-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "input-field",
                    "Interactive",
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
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.width = 360f;

            var singleLine = new InputField(new InputFieldState("Sample text", "Single line"));
            singleLine.style.marginBottom = 12f;
            surface.Add(singleLine);

            var multiline = new InputField(new InputFieldState(
                "1 行目のテキスト\n2 行目のテキスト\n3 行目のテキスト\n4 行目のテキスト\n5 行目のテキスト\n6 行目のテキスト\n7 行目のテキスト\n8 行目のテキスト",
                "Multiline",
                true,
                120f));
            surface.Add(multiline);

            preview.Body.Add(surface);
        }
    }
}
