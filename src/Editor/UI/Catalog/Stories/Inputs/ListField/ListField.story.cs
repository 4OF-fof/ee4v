using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ListFieldCatalogRegistrar
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
                    "Editor/UI/Components/Inputs/ListField/list-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "list-field",
                    "Inputs",
                    "ListField",
                    I18N.Get("catalog.listInput.description"),
                    I18N.Get("catalog.listInput.details"),
                    new[] { "InputField" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildListFieldStory(parent),
                    new[]
                    {
                        "Editor/Core/Presentation/Settings/CommaSeparatedListSettingDrawer.cs",
                        "Editor/Feature/Avatar/FaceExpression/BlendShapeNamePresetSetting.cs"
                    }));
            }
        }

        private void BuildListFieldStory(
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

            ListFieldState<string> CreateState(
                IReadOnlyList<string> values)
            {
                return new ListFieldState<string>(
                    values,
                    (value, notifyValueChanged) =>
                    {
                        var item = new InputField(
                            new InputFieldState(
                                value,
                                placeholder: placeholder));
                        item.ValueChanged += notifyValueChanged;
                        return item;
                    },
                    () => string.Empty,
                    string.IsNullOrEmpty,
                    item => ((InputField)item).FocusInput(),
                    tooltip,
                    I18N.Get("catalog.listInput.addItem"),
                    I18N.Get("catalog.listInput.removeItem"));
            }

            var field = new ListField<string>(
                CreateState(
                    new[] { "Airi", "Manuka", "Moe" }));
            surface.Add(field);
            preview.Body.Add(surface);

            refresh = () =>
            {
                tooltipField.SetValueWithoutNotify(tooltip);
                placeholderField.SetValueWithoutNotify(placeholder);
                field.SetState(CreateState(field.Values));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
