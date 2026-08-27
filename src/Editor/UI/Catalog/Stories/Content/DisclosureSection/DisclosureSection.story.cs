using System;
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
                    "disclosure-section", "Containers", "DisclosureSection",
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
            var title = "表示設定";
            var expanded = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "見出しと展開状態を変更します。Previewの見出しからも開閉できます。");
            var titleField = AddTextField(
                controls.Content,
                "見出し",
                title,
                value =>
                {
                    title = value;
                    refresh();
                });
            Toggle expandedToggle = null;
            expandedToggle = AddToggle(
                controls.Content,
                "展開",
                expanded,
                value =>
                {
                    expanded = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var section = new DisclosureSection();
            section.Add(UiTextFactory.Create("展開された本文です。"));
            section.ExpandedChanged += value =>
            {
                expanded = value;
                expandedToggle.SetValueWithoutNotify(value);
            };
            preview.Body.Add(section);

            refresh = () =>
            {
                titleField.SetValueWithoutNotify(title);
                expandedToggle.SetValueWithoutNotify(expanded);
                section.SetTitle(title);
                section.SetExpanded(expanded);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
