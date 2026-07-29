using Ee4v.ProjectTabs;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class ProjectTabsCatalogStory
    {
        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 120;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/EditorEnhancements/ProjectTabs/UI/project-tabs.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Navigation/history-navigation-overlay.uss");
                Register(
                    registry,
                    "project-tabs-view",
                    "ProjectTabsView",
                    "Domain/ProjectTabs/Components",
                    false);
                Register(
                    registry,
                    "project-tabs-host",
                    "ProjectTabsHost",
                    "Domain/ProjectTabs/Components",
                    false);
                Register(
                    registry,
                    "project-tabs-screen",
                    "Project Tabs",
                    "Domain/ProjectTabs/Screens",
                    true);
            }

            private static void Register(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string title,
                string group,
                bool screen)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        group,
                        title,
                        screen
                            ? CatalogCoveragePreview.ScreenDescription(title)
                            : CatalogCoveragePreview.ComponentDescription(title),
                        screen
                            ? CatalogCoveragePreview.ScreenDetails(title)
                            : CatalogCoveragePreview.ComponentDetails(title),
                        new[] { "HistoryNavigationOverlay", "Icon" },
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        Build));
            }
        }

        private static void Build(
            CatalogWindow window,
            VisualElement parent)
        {
            var view = new ProjectTabsView();
            view.SetState(new ProjectTabsViewState(
                new[]
                {
                    new ProjectTabViewState(
                        "home",
                        string.Empty,
                        CatalogCoveragePreview.SampleFolder,
                        false,
                        true,
                        true),
                    new ProjectTabViewState(
                        "avatars",
                        CatalogCoveragePreview.SampleTagOne,
                        "Assets/Avatars",
                        true,
                        true),
                    new ProjectTabViewState(
                        "environment",
                        CatalogCoveragePreview.SampleTagTwo,
                        "Assets/Environment",
                        true)
                },
                "avatars",
                true,
                true,
                new[]
                {
                    new ProjectHistoryEntryViewState(
                        CatalogCoveragePreview.SampleFolder,
                        1)
                },
                new[]
                {
                    new ProjectHistoryEntryViewState(
                        CatalogCoveragePreview.SampleCollection,
                        1)
                }));
            view.style.flexGrow = 1f;
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                120f,
                true).Add(view);
        }
    }
}
