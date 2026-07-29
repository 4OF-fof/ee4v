using Ee4v.Core.Internal.EditorAPI;
using Ee4v.Testing.Contracts;
using System.Linq;
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

        [Test]
        [FeatureTestCase(
            "Unity標準Favoritesのfolderを安全に読み取る",
            "SavedSearchFiltersが利用できるUnityでは、facadeが有効なfolder pathだけを返すことを確認します。",
            order: 261)]
        public void ProjectFavorites_ReturnsValidUniqueFolders()
        {
            Assert.That(
                ProjectFavorites.TryGetFolders(out var folders),
                Is.True);
            Assert.That(
                folders.Select(folder => folder.FolderPath),
                Is.Unique);
            Assert.That(
                folders.All(folder =>
                    AssetDatabase.IsValidFolder(
                        folder.FolderPath)),
                Is.True);
        }

        private sealed class TestEditorWindow : EditorWindow
        {
        }
    }
}
