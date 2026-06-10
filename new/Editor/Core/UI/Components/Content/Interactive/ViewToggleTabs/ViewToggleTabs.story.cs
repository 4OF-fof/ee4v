using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ViewToggleTabsCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 11; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Content/Interactive/ViewToggleTabs/view-toggle-tabs.uss");
                registry.RegisterStory(new StoryRegistration(
                    "view-toggle-tabs",
                    "Content/Interactive",
                    "ViewToggleTabs",
                    "近接する表示内容を切り替えるための小型 toggle tab コンポーネントです。",
                    "TabCard のように panel slot を持たず、inspector や toolbar 周辺で file tree / details などの view mode だけを切り替える用途を想定しています。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildViewToggleTabsStory(parent)));
            }
        }

        private void BuildViewToggleTabsStory(VisualElement parent)
        {
            var selectedTabId = "detail";
            var treeEnabled = true;
            var detailEnabled = true;
            ViewToggleTabs tabs = null;

            var controls = CreatePlainControlsSection(parent, "近接する詳細 view の mode 切り替えを確認します。");

            var detailToggle = new Toggle("Detail Enabled")
            {
                value = detailEnabled
            };
            detailToggle.RegisterValueChangedCallback(evt =>
            {
                detailEnabled = evt.newValue;
                selectedTabId = RefreshTabs(tabs, selectedTabId, treeEnabled, detailEnabled);
            });
            controls.Content.Add(detailToggle);

            var treeToggle = new Toggle("Tree Enabled")
            {
                value = treeEnabled
            };
            treeToggle.RegisterValueChangedCallback(evt =>
            {
                treeEnabled = evt.newValue;
                selectedTabId = RefreshTabs(tabs, selectedTabId, treeEnabled, detailEnabled);
            });
            controls.Content.Add(treeToggle);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            tabs = new ViewToggleTabs();
            tabs.SelectionChanged += id =>
            {
                selectedTabId = id;
                selectedTabId = RefreshTabs(tabs, selectedTabId, treeEnabled, detailEnabled);
            };

            surface.Add(tabs);
            preview.Body.Add(surface);

            selectedTabId = RefreshTabs(tabs, selectedTabId, treeEnabled, detailEnabled);
            FinalizeControlsSection(parent, controls);
        }

        private static string RefreshTabs(
            ViewToggleTabs tabs,
            string selectedTabId,
            bool treeEnabled,
            bool detailEnabled)
        {
            if (tabs == null)
            {
                return selectedTabId;
            }

            tabs.SetState(new ViewToggleTabsState(
                new[]
                {
                    new ViewToggleTabState("detail", "Asset Info", detailEnabled),
                    new ViewToggleTabState("tree", "File Tree", treeEnabled)
                },
                selectedTabId));
            return tabs.SelectedTabId;
        }
    }
}
