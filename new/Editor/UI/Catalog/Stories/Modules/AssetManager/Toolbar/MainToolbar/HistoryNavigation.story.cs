using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
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
                    "通常時は末尾の breadcrumb だけを表示し、breadcrumb と戻る・進むボタンの hover 時に全階層または履歴を overlay 表示します。",
                    new string[0],
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildHistoryNavigationStory(window, parent)));
            }
        }

        private static void BuildHistoryNavigationStory(CatalogWindow window, VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = UiSpacingTokens.Xl;
            surface.style.paddingRight = UiSpacingTokens.Xl;
            surface.style.paddingTop = UiSpacingTokens.Xl;
            surface.style.paddingBottom = UiSpacingTokens.Xl;

            var historyNavigation = new HistoryNavigation();
            historyNavigation.SetState(new AssetItemGridHistoryState(
                new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.FileList,
                    "booth-library",
                    "Booth Library",
                    "sample-item",
                    "Sample Avatar Pack"),
                canGoBack: true,
                canGoForward: true,
                backEntries: new[]
                {
                    new AssetItemGridHistoryEntry(
                        AssetItemGridHistoryEntryKind.View,
                        "all-assets",
                        "All Assets")
                },
                forwardEntries: new[]
                {
                    new AssetItemGridHistoryEntry(
                        AssetItemGridHistoryEntryKind.FileDetail,
                        "booth-library",
                        "Booth Library",
                        "sample-item",
                        "Sample Avatar Pack",
                        detailId: "sample-file",
                        detailName: "avatar.unitypackage")
                }));

            surface.Add(historyNavigation);
            preview.Body.Add(surface);
        }
    }
}
