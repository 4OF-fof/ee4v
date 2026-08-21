using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class DisclosureSectionCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 21;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "disclosure-section", "Content", "DisclosureSection",
                    "見出しから本文の表示を切り替えるセクションです。",
                    "見出しの操作で本文を開閉し、展開状態の変更を通知します。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildDisclosureSectionStory(parent),
                    new[]
                    {
                        "Editor/Core/Presentation/Settings/SettingsUiRenderer.cs"
                    }));
            }
        }

        private void BuildDisclosureSectionStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var section = new DisclosureSection("表示設定");
            section.Add(UiTextFactory.Create("展開された本文です。"));
            preview.Body.Add(section);
        }
    }
}
