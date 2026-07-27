using Ee4v.Core.Internal.EditorAPI;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Tests
{
    public sealed class ProjectBrowserEditorApiTests
    {
        [Test]
        [FeatureTestCase(
            "ProjectBrowser facade は対象外 window へ fallback しない",
            "windowを明示した操作で別のProject windowを誤操作しないことを確認します。",
            order: 260)]
        public void ExplicitNonProjectWindow_ReturnsFalse()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            try
            {
                Assert.That(
                    ProjectBrowser.TryGetSnapshot(window, out _),
                    Is.False);
                Assert.That(
                    ProjectBrowser.TryShowFolder(
                        window,
                        AssetDatabase.AssetPathToGUID("Assets")),
                    Is.False);
                Assert.That(
                    ProjectBrowser.TrySetSearch(window, "test"),
                    Is.False);
                Assert.That(
                    ProjectBrowser.TryClearSearch(window),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private sealed class TestEditorWindow : EditorWindow
        {
        }
    }
}
