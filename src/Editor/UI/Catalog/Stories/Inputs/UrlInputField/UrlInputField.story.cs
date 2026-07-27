using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class UrlInputFieldCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 32; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Inputs/InputField/input-field.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Inputs/UrlInputField/url-input-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "url-input-field",
                    "Inputs",
                    "UrlInputField",
                    "ブラウザ起動ボタン付きの汎用URL入力フィールドです。",
                    "URL文字列を編集し、現在値がある場合だけ右端のボタンからブラウザで開けます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildUrlInputFieldStory(parent)));
            }
        }

        private void BuildUrlInputFieldStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.width = 320f;

            surface.Add(new UrlInputField(new UrlInputFieldState(
                "https://booth.pm",
                I18N.Get("ui.url.openTooltip"))));
            preview.Body.Add(surface);
        }
    }
}
