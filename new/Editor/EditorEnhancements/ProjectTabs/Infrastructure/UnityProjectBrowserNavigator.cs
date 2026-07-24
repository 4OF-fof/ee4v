using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;

namespace Ee4v.ProjectTabs
{
    internal sealed class UnityProjectBrowserNavigator
    {
        private readonly EditorWindow _window;

        public UnityProjectBrowserNavigator(EditorWindow window)
        {
            _window = window;
        }

        public bool TryGetCurrentLocation(out ProjectTabLocation location)
        {
            location = null;
            if (!ProjectBrowser.TryGetSnapshot(_window, out var snapshot) ||
                string.IsNullOrWhiteSpace(snapshot.FolderGuid) ||
                string.IsNullOrWhiteSpace(snapshot.FolderPath))
            {
                return false;
            }

            location = new ProjectTabLocation(
                snapshot.FolderGuid,
                snapshot.FolderPath,
                snapshot.SearchText);
            return true;
        }

        public bool TryOpen(ProjectTabLocation location)
        {
            if (location == null ||
                !ProjectBrowser.TryShowFolder(
                    _window,
                    location.FolderGuid))
            {
                return false;
            }

            return string.IsNullOrEmpty(location.SearchText)
                ? ProjectBrowser.TryClearSearch(_window)
                : ProjectBrowser.TrySetSearch(
                    _window,
                    location.SearchText);
        }

        public static ProjectTabLocation CreateDefaultLocation()
        {
            return new ProjectTabLocation(
                AssetDatabase.AssetPathToGUID("Assets"),
                "Assets");
        }
    }
}
