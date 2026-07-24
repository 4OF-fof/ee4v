using System.Collections;
using NUnit.Framework;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class AssetItemGridLayoutTests
    {
        [UnityTest]
        public IEnumerator AttachedHostsKeepIndependentGridSizesAndUseSettingAsInitialValue()
        {
            var window = ScriptableObject.CreateInstance<GridTestWindow>();
            MainViewHost first = null;
            MainViewHost second = null;
            MainViewHost third = null;
            window.position = new Rect(0f, 0f, 1000f, 540f);
            window.Show();

            try
            {
                first = AddView(window);
                second = AddView(window);
                yield return null;
                yield return null;

                var initialSize = first.MainView.GridSize;
                var firstSize = initialSize == 2 ? 3 : 2;
                Assert.That(second.MainView.GridSize, Is.EqualTo(initialSize));

                first.MainView.SetGridSize(firstSize);
                yield return null;

                Assert.That(first.MainView.DisplayedGridSize, Is.EqualTo(firstSize));
                Assert.That(second.MainView.DisplayedGridSize, Is.EqualTo(initialSize));
                Assert.That(first.Toolbar.GridSizeValue, Is.EqualTo(firstSize));
                Assert.That(second.Toolbar.GridSizeValue, Is.EqualTo(initialSize));

                third = AddView(window);
                yield return null;

                Assert.That(third.MainView.GridSize, Is.EqualTo(initialSize));
                Assert.That(third.Toolbar.GridSizeValue, Is.EqualTo(initialSize));

                second.MainView.SetGridSize(12);
                yield return null;

                Assert.That(first.MainView.DisplayedGridSize, Is.EqualTo(firstSize));
                Assert.That(second.MainView.DisplayedGridSize, Is.EqualTo(12));
                Assert.That(third.MainView.DisplayedGridSize, Is.EqualTo(initialSize));
                Assert.That(first.Toolbar.GridSizeValue, Is.EqualTo(firstSize));
                Assert.That(second.Toolbar.GridSizeValue, Is.EqualTo(12));
                Assert.That(third.Toolbar.GridSizeValue, Is.EqualTo(initialSize));
            }
            finally
            {
                first?.Dispose();
                second?.Dispose();
                third?.Dispose();
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator StandaloneInfomationWindowRestoresAndFollowsSelectionSession()
        {
            var session = AssetManagerUiDependencies.StandaloneViewSession;
            var selectedItem = new ItemCardState(
                "item-1",
                "Selected Item",
                new ItemImageState());
            InfomationWindow window = null;
            session.SetSelection(
                new[] { selectedItem },
                AssetSelectionContentKind.AssetItem);
            session.RequestDetailTab("asset-info");

            try
            {
                window = ScriptableObject.CreateInstance<InfomationWindow>();
                window.Show();
                yield return null;
                yield return null;

                var panel = window.rootVisualElement.Q<InfomationPanel>();
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.SelectedItems.Count, Is.EqualTo(1));
                Assert.That(panel.SelectedItems[0].ItemId, Is.EqualTo("item-1"));
                Assert.That(
                    panel.SelectionContentKind,
                    Is.EqualTo(AssetSelectionContentKind.AssetItem));
                Assert.That(panel.SelectedDetailTabId, Is.EqualTo("asset-info"));

                session.SetSelection(
                    new[]
                    {
                        new ItemCardState(
                            "file-1",
                            "Selected File",
                            new ItemImageState(),
                            null,
                            "item-1")
                    },
                    AssetSelectionContentKind.AssetFile);
                yield return null;

                Assert.That(panel.SelectedItems.Count, Is.EqualTo(1));
                Assert.That(panel.SelectedItems[0].ItemId, Is.EqualTo("file-1"));
                Assert.That(
                    panel.SelectionContentKind,
                    Is.EqualTo(AssetSelectionContentKind.AssetFile));
            }
            finally
            {
                session.SetSelection(
                    null,
                    AssetSelectionContentKind.AssetItem);
                window?.Close();
            }
        }

        private static MainViewHost AddView(EditorWindow window)
        {
            var host = new MainViewHost();
            host.MainView.style.flexGrow = 1f;
            window.rootVisualElement.Add(host.Toolbar);
            window.rootVisualElement.Add(host.MainView);
            return host;
        }

        private sealed class GridTestWindow : EditorWindow
        {
        }
    }
}
