using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal static class HistoryNavigationCatalogRegistrarStory
    {
        private sealed class HistoryNavigationCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 102; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/AssetManager/UI/Toolbar/MainToolbar/main-toolbar.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "history-navigation",
                    "Domain/AssetManager",
                    "HistoryNavigation",
                    "AssetManager の history stack を操作する navigation component です。",
                    "戻る、進む、クリック可能な breadcrumb を横並びで表示し、MainToolbar などの toolbar 先頭に配置して使います。",
                    new string[0],
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildHistoryNavigationStory(window, parent)));
            }
        }

        private static void BuildHistoryNavigationStory(CatalogWindow window, VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            var historyNavigation = new HistoryNavigation();
            historyNavigation.SetState(new AssetItemGridHistoryState(
                new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.FileList,
                    "booth-library",
                    "Booth Library",
                    "sample-item",
                    "Sample Avatar Pack"),
                canGoBack: true,
                canGoForward: true));

            surface.Add(historyNavigation);
            preview.Body.Add(surface);
        }
    }
}
