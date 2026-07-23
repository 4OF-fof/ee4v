using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ee4v.AssetManager.Tests
{
    public sealed class AssetItemGridLayoutTests
    {
        [UnityTest]
        public IEnumerator AttachedHostsWithIndependentControllersFollowTheSameGridSetting()
        {
            var settingController = new MainViewController();
            var originalSize = settingController.ItemsPerRow;
            var window = ScriptableObject.CreateInstance<GridTestWindow>();
            MainViewHost first = null;
            MainViewHost second = null;
            window.position = new Rect(0f, 0f, 1000f, 540f);
            window.Show();

            try
            {
                first = AddView(window);
                second = AddView(window);
                yield return null;
                yield return null;

                first.MainView.SetGridSize(2);
                yield return null;

                Assert.That(first.MainView.DisplayedGridSize, Is.EqualTo(2));
                Assert.That(second.MainView.DisplayedGridSize, Is.EqualTo(2));
                Assert.That(first.Toolbar.GridSizeValue, Is.EqualTo(2));
                Assert.That(second.Toolbar.GridSizeValue, Is.EqualTo(2));

                second.MainView.SetGridSize(12);
                yield return null;

                Assert.That(first.MainView.DisplayedGridSize, Is.EqualTo(12));
                Assert.That(second.MainView.DisplayedGridSize, Is.EqualTo(12));
                Assert.That(first.Toolbar.GridSizeValue, Is.EqualTo(12));
                Assert.That(second.Toolbar.GridSizeValue, Is.EqualTo(12));
            }
            finally
            {
                settingController.SetItemsPerRow(originalSize);
                first?.Dispose();
                second?.Dispose();
                settingController.Dispose();
                window.Close();
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
