using System;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class CommaSeparatedListFieldCatalogRegistrar
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
                    "Editor/UI/Components/Inputs/CommaSeparatedListField/comma-separated-list-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "comma-separated-list-field",
                    "Inputs",
                    "CommaSeparatedListField",
                    I18N.Get("catalog.listInput.description"),
                    I18N.Get("catalog.listInput.details"),
                    new[] { "InputField" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildCommaSeparatedListFieldStory(parent),
                    new[]
                    {
                        "Editor/Core/Presentation/Settings/CommaSeparatedListSettingDrawer.cs"
                    }));
            }
        }

        private void BuildCommaSeparatedListFieldStory(
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

            var field = new CommaSeparatedListField(
                new CommaSeparatedListFieldState(
                    new[] { "Airi", "Manuka", "Moe" },
                    tooltip,
                    placeholder));
            var serializedValue = UiTextFactory.Create(
                I18N.Get(
                    "catalog.listInput.savedValue",
                    new object[] { string.Join(",", field.Values) }),
                UiClassNames.SecondaryText);
            serializedValue.style.marginTop = UiSpacingTokens.Medium;
            field.ValuesChanged += values =>
                serializedValue.SetText(
                    I18N.Get(
                        "catalog.listInput.savedValue",
                        new object[] { string.Join(",", values) }));

            surface.Add(field);
            surface.Add(serializedValue);
            preview.Body.Add(surface);

            refresh = () =>
            {
                tooltipField.SetValueWithoutNotify(tooltip);
                placeholderField.SetValueWithoutNotify(placeholder);
                field.SetState(new CommaSeparatedListFieldState(
                    field.Values,
                    tooltip,
                    placeholder));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
