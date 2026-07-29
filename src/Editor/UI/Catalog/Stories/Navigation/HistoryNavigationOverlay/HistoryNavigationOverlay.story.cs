using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class HistoryNavigationOverlayCatalogRegistrar
            : ICatalogRegistrar
        {
            public int Order => 60;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Navigation/history-navigation-overlay.uss");
                registry.RegisterStory(new StoryRegistration(
                    "history-navigation-overlay",
                    "Navigation",
                    "HistoryNavigationOverlay",
                    CatalogCoveragePreview.ComponentDescription(
                        "HistoryNavigationOverlay"),
                    CatalogCoveragePreview.ComponentDetails(
                        "HistoryNavigationOverlay"),
                    null,
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildHistoryNavigationOverlayStory(parent)));
            }
        }

        private void BuildHistoryNavigationOverlayStory(
            VisualElement parent)
        {
            var surface = CatalogCoveragePreview.CreateSurface(
                this,
                parent,
                210f);
            surface.style.position = Position.Relative;

            var overlay = new HistoryNavigationOverlay(5);
            overlay.SetRows(new[]
            {
                new HistoryNavigationOverlayRowState(
                    CatalogCoveragePreview.SampleFolder),
                new HistoryNavigationOverlayRowState(
                    CatalogCoveragePreview.SampleCollection,
                    () => { }),
                new HistoryNavigationOverlayRowState(
                    CatalogCoveragePreview.SampleFile,
                    () => { })
            });
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.position = Position.Relative;
            overlay.style.left = StyleKeyword.Auto;
            overlay.style.top = StyleKeyword.Auto;
            surface.Add(overlay);
        }
    }
}
