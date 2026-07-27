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
                        window.BuildCommaSeparatedListFieldStory(parent)));
            }
        }

        private void BuildCommaSeparatedListFieldStory(
            VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.width = 520f;

            var field = new CommaSeparatedListField(
                new CommaSeparatedListFieldState(
                    new[] { "Airi", "Manuka", "Moe" },
                    I18N.Get("catalog.listInput.sampleTooltip"),
                    I18N.Get("catalog.listInput.itemPlaceholder")));
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
        }
    }
}
