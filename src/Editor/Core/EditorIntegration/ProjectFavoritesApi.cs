using System;
using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI;

namespace Ee4v.Core.EditorIntegration
{
    public sealed class ProjectFavoriteFolder
    {
        internal ProjectFavoriteFolder(
            string folderGuid,
            string folderPath)
        {
            FolderGuid = folderGuid ?? string.Empty;
            FolderPath = folderPath ?? string.Empty;
        }

        public string FolderGuid { get; }
        public string FolderPath { get; }
    }

    public static class ProjectFavoritesApi
    {
        public static bool TryGetFolders(
            out IReadOnlyList<ProjectFavoriteFolder> folders)
        {
            if (!ProjectFavorites.TryGetFolders(out var source))
            {
                folders = Array.Empty<ProjectFavoriteFolder>();
                return false;
            }

            var result = new ProjectFavoriteFolder[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = new ProjectFavoriteFolder(
                    source[i].FolderGuid,
                    source[i].FolderPath);
            }

            folders = result;
            return true;
        }

        public static bool TryAddFolder(string folderPath)
        {
            return ProjectFavorites.TryAddFolder(folderPath);
        }

        public static bool TryRemoveFolder(string folderPath)
        {
            return ProjectFavorites.TryRemoveFolder(folderPath);
        }

        public static bool TryAddChangedListener(Action callback)
        {
            return ProjectFavorites.TryAddChangedListener(callback);
        }

        public static void RemoveChangedListener(Action callback)
        {
            ProjectFavorites.RemoveChangedListener(callback);
        }
    }
}
