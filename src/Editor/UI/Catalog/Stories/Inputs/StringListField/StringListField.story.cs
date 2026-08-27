using System;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class StringListFieldCatalogRegistrar
            : ICatalogRegistrar
        {
            public int Order
            {
                get { return 33; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/InputField/input-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/StringListField/string-list-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "string-list-field",
                    "Inputs",
                    "StringListField",
                    I18N.Get("catalog.listInput.description"),
                    I18N.Get("catalog.listInput.details"),
                    new[] { "InputField" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildStringListFieldStory(parent),
                    new[]
                    {
                        "Editor/Core/Presentation/Settings/CommaSeparatedListSettingDrawer.cs"
                    }));
            }
        }

        private void BuildStringListFieldStory(
            VisualElement parent)
        {
            var tooltip = I18N.Get(
                "catalog.listInput.sampleTooltip");
            var placeholder = I18N.Get(
                "catalog.listInput.itemPlaceholder");
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "補足表示と項目のプレースホルダーを変更します。値はPreviewで直接編集できます。");
            var tooltipField = AddTextField(
                controls.Content,
                "ツールチップ",
                tooltip,
                value =>
                {
                    tooltip = value;
                    refresh();
                });
            var placeholderField = AddTextField(
                controls.Content,
                "プレースホルダー",
                placeholder,
                value =>
                {
                    placeholder = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewArea();
            surface.style.width = 520f;

            var field = new StringListField(
                new StringListFieldState(
                    new[] { "Airi", "Manuka", "Moe" },
                    tooltip,
                    placeholder,
                    I18N.Get("catalog.listInput.addItem"),
                    I18N.Get("catalog.listInput.removeItem")));
            surface.Add(field);
            preview.Body.Add(surface);

            refresh = () =>
            {
                tooltipField.SetValueWithoutNotify(tooltip);
                placeholderField.SetValueWithoutNotify(placeholder);
                field.SetState(new StringListFieldState(
                    field.Values,
                    tooltip,
                    placeholder,
                    I18N.Get("catalog.listInput.addItem"),
                    I18N.Get("catalog.listInput.removeItem")));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
