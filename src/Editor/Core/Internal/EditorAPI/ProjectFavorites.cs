using System;
using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI.Backends;

namespace Ee4v.Core.Internal.EditorAPI
{
    internal sealed class ProjectFavoriteFolder
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

    internal static class ProjectFavorites
    {
        public static bool TryGetFolders(
            out IReadOnlyList<ProjectFavoriteFolder> folders)
        {
            return ProjectFavoritesBackend.TryGetFolders(out folders);
        }

        public static bool TryAddFolder(string folderPath)
        {
            return ProjectFavoritesBackend.TryAddFolder(folderPath);
        }

        public static bool TryRemoveFolder(string folderPath)
        {
            return ProjectFavoritesBackend.TryRemoveFolder(folderPath);
        }

        public static bool TryAddChangedListener(Action callback)
        {
            return ProjectFavoritesBackend.TryAddChangedListener(callback);
        }

        public static void RemoveChangedListener(Action callback)
        {
            ProjectFavoritesBackend.RemoveChangedListener(callback);
        }
    }
}
