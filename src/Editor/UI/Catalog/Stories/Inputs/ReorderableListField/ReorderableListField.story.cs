using Ee4v.Core.I18n;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ReorderableListFieldCatalogRegistrar
            : ICatalogRegistrar
        {
            public int Order
            {
                get { return 34; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/ReorderableListField/reorderable-list-field.uss");
                registry.RegisterStory(new StoryRegistration(
                    "reorderable-list-field",
                    "Inputs",
                    "ReorderableListField",
                    I18N.Get("catalog.reorderableList.description"),
                    I18N.Get("catalog.reorderableList.details"),
                    null,
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildReorderableListFieldStory(parent)));
            }
        }

        private void BuildReorderableListFieldStory(
            UnityEngine.UIElements.VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.width = 420f;

            var field = new ReorderableListField(
                new ReorderableListFieldState(
                    new[]
                    {
                        new ReorderableListItemState("ee4v", "ee4v"),
                        new ReorderableListItemState("eagle", "Eagle"),
                        new ReorderableListItemState(
                            "blm",
                            "Booth Library Manager")
                    },
                    reorderTooltip:
                        I18N.Get(
                            "catalog.reorderableList.reorderTooltip")));

            surface.Add(field);
            preview.Body.Add(surface);
        }
    }
}
