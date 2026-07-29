using Ee4v.HiddenObjects;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class HiddenObjectsComponentsCatalogStory
    {
        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 140;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/SearchField/search-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/EditorEnhancements/HiddenObjects/UI/hidden-objects-window.uss");
                Register(
                    registry,
                    "hidden-objects-toolbar",
                    "HiddenObjectsToolbar",
                    BuildToolbar);
                Register(
                    registry,
                    "hidden-object-tree-row",
                    "HiddenObjectTreeRow",
                    BuildTreeRow);
                Register(
                    registry,
                    "hidden-object-tree-view",
                    "HiddenObjectTreeView",
                    BuildTreeView);
                Register(
                    registry,
                    "hidden-objects-footer",
                    "HiddenObjectsFooter",
                    BuildFooter);
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        "hidden-objects-window-screen",
                        "Domain/HiddenObjects/Screens",
                        "Hidden Objects Window",
                        CatalogCoveragePreview.ScreenDescription(
                            "Hidden Objects Window"),
                        CatalogCoveragePreview.ScreenDetails(
                            "Hidden Objects Window"),
                        new[]
                        {
                            "HiddenObjectsToolbar",
                            "HiddenObjectTreeView",
                            "HiddenObjectsFooter"
                        },
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        BuildScreen));
            }

            private static void Register(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string title,
                System.Action<CatalogWindow, VisualElement> build)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        "Domain/HiddenObjects/Components",
                        title,
                        CatalogCoveragePreview.ComponentDescription(title),
                        CatalogCoveragePreview.ComponentDetails(title),
                        null,
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        build));
            }
        }

        private static void BuildToolbar(
            CatalogWindow window,
            VisualElement parent)
        {
            var toolbar = new HiddenObjectsToolbar(CreateText());
            toolbar.SetState(
                string.Empty,
                CreateSceneOptions(),
                0);
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                100f,
                true).Add(toolbar);
        }

        private static void BuildTreeRow(
            CatalogWindow window,
            VisualElement parent)
        {
            var row = new HiddenObjectTreeRow();
            row.SetState(new HiddenObjectTreeItemViewState(
                1,
                false,
                101,
                CatalogCoveragePreview.SampleTitle,
                CatalogCoveragePreview.SampleSubtitle,
                true,
                true,
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.GenericFile,
                    UiSizeTokens.Size16)));
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                80f,
                true).Add(row);
        }

        private static void BuildTreeView(
            CatalogWindow window,
            VisualElement parent)
        {
            var tree = new HiddenObjectTreeView();
            tree.SetState(CreateState());
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                290f).Add(tree);
        }

        private static void BuildFooter(
            CatalogWindow window,
            VisualElement parent)
        {
            var footer = new HiddenObjectsFooter(CreateText());
            footer.SetState(
                CatalogCoveragePreview.SampleStatus,
                3,
                1);
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                100f,
                true).Add(footer);
        }

        private static void BuildScreen(
            CatalogWindow window,
            VisualElement parent)
        {
            var view = new HiddenObjectsView(CreateText());
            view.SetState(CreateState());
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                420f).Add(view);
        }

        private static HiddenObjectsViewText CreateText()
        {
            return new HiddenObjectsViewText(
                CatalogCoveragePreview.SampleSearch,
                CatalogCoveragePreview.SampleSearch,
                CatalogCoveragePreview.SampleClearSelection,
                CatalogCoveragePreview.SampleSceneAll,
                CatalogCoveragePreview.SampleRefresh,
                CatalogCoveragePreview.SampleRefreshTooltip,
                CatalogCoveragePreview.SampleSelectAll,
                CatalogCoveragePreview.SampleClearSelection,
                CatalogCoveragePreview.SampleReveal);
        }

        private static HiddenObjectSceneOptionViewState[]
            CreateSceneOptions()
        {
            return new[]
            {
                new HiddenObjectSceneOptionViewState(
                    0,
                    CatalogCoveragePreview.SampleSceneAll),
                new HiddenObjectSceneOptionViewState(
                    10,
                    CatalogCoveragePreview.SampleSceneMain)
            };
        }

        private static HiddenObjectsViewState CreateState()
        {
            var icon = IconState.FromBuiltinIcon(
                UiBuiltinIcon.GenericFile,
                UiSizeTokens.Size16);
            return new HiddenObjectsViewState(
                new[]
                {
                    new HiddenObjectSceneGroupViewState(
                        10,
                        CatalogCoveragePreview.SampleSceneMain,
                        CatalogCoveragePreview.SampleStatus,
                        new[]
                        {
                            new HiddenObjectNodeViewState(
                                101,
                                CatalogCoveragePreview.SampleTitle,
                                true,
                                true,
                                icon,
                                null),
                            new HiddenObjectNodeViewState(
                                102,
                                CatalogCoveragePreview.SampleSubtitle,
                                true,
                                false,
                                icon,
                                null)
                        })
                },
                CreateSceneOptions(),
                0,
                string.Empty,
                CatalogCoveragePreview.SampleStatus,
                CatalogCoveragePreview.SampleEmpty,
                CatalogCoveragePreview.SampleDescription,
                2,
                1);
        }
    }
}
