using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class ProjectFavoritesBackend
    {
        private const BindingFlags StaticMethodFlags =
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;
        private const BindingFlags InstancePropertyFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private static readonly Type SavedSearchFiltersType =
            typeof(Editor).Assembly.GetType(
                "UnityEditor.SavedSearchFilters");
        private static readonly Type SearchFilterType =
            typeof(Editor).Assembly.GetType("UnityEditor.SearchFilter");
        private static readonly MethodInfo ConvertToTreeViewMethod =
            SavedSearchFiltersType?.GetMethod(
                "ConvertToTreeView",
                StaticMethodFlags);
        private static readonly MethodInfo GetFilterMethod =
            SavedSearchFiltersType?.GetMethod(
                "GetFilter",
                StaticMethodFlags,
                null,
                new[] { typeof(int) },
                null);
        private static readonly MethodInfo AddSavedFilterMethod =
            SavedSearchFiltersType?.GetMethod(
                "AddSavedFilter",
                StaticMethodFlags);
        private static readonly MethodInfo RemoveSavedFilterMethod =
            SavedSearchFiltersType?.GetMethod(
                "RemoveSavedFilter",
                StaticMethodFlags,
                null,
                new[] { typeof(int) },
                null);
        private static readonly MethodInfo AddChangeListenerMethod =
            SavedSearchFiltersType?.GetMethod(
                "AddChangeListener",
                StaticMethodFlags,
                null,
                new[] { typeof(Action) },
                null);
        private static readonly PropertyInfo SearchFilterFoldersProperty =
            SearchFilterType?.GetProperty(
                "folders",
                InstancePropertyFlags);
        private static readonly Action SavedFiltersChanged =
            OnSavedFiltersChanged;
        private static Action _changed;
        private static bool _listenerRegistered;

        public static bool TryGetFolders(
            out IReadOnlyList<ProjectFavoriteFolder> folders)
        {
            folders = Array.Empty<ProjectFavoriteFolder>();
            if (!CanRead)
            {
                return false;
            }

            try
            {
                var root = ConvertToTreeViewMethod.Invoke(
                    null,
                    Array.Empty<object>());
                if (root == null)
                {
                    return false;
                }

                var result = new List<ProjectFavoriteFolder>();
                var seenPaths = new HashSet<string>(
                    StringComparer.Ordinal);
                foreach (var item in EnumerateTreeItems(root))
                {
                    if (!TryReadFolderPath(item, out var folderPath) ||
                        !seenPaths.Add(folderPath))
                    {
                        continue;
                    }

                    result.Add(new ProjectFavoriteFolder(
                        AssetDatabase.AssetPathToGUID(folderPath),
                        folderPath));
                }

                folders = result;
                return true;
            }
            catch (Exception)
            {
                folders = Array.Empty<ProjectFavoriteFolder>();
                return false;
            }
        }

        public static bool TryAddFolder(string folderPath)
        {
            var normalizedPath = NormalizeFolderPath(folderPath);
            if (!CanWrite ||
                string.IsNullOrEmpty(normalizedPath) ||
                !AssetDatabase.IsValidFolder(normalizedPath))
            {
                return false;
            }

            if (!TryGetFavoriteEntries(out var entries))
            {
                return false;
            }

            if (entries.Any(entry =>
                    string.Equals(
                        entry.FolderPath,
                        normalizedPath,
                        StringComparison.Ordinal)))
            {
                return true;
            }

            try
            {
                var filter = Activator.CreateInstance(SearchFilterType);
                SearchFilterFoldersProperty.SetValue(
                    filter,
                    new[] { normalizedPath });
                var displayName = GetDisplayName(normalizedPath);
                var result = AddSavedFilterMethod.Invoke(
                    null,
                    new object[]
                    {
                        displayName,
                        filter,
                        64f
                    });
                return result is int instanceId && instanceId != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryRemoveFolder(string folderPath)
        {
            var normalizedPath = NormalizeFolderPath(folderPath);
            if (!CanWrite || string.IsNullOrEmpty(normalizedPath))
            {
                return false;
            }

            if (!TryGetFavoriteEntries(out var entries))
            {
                return false;
            }

            var matchingIds = entries
                .Where(entry =>
                    string.Equals(
                        entry.FolderPath,
                        normalizedPath,
                        StringComparison.Ordinal))
                .Select(entry => entry.InstanceId)
                .ToArray();
            if (matchingIds.Length == 0)
            {
                return true;
            }

            try
            {
                foreach (var instanceId in matchingIds)
                {
                    RemoveSavedFilterMethod.Invoke(
                        null,
                        new object[] { instanceId });
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryAddChangedListener(Action callback)
        {
            if (callback == null || AddChangeListenerMethod == null)
            {
                return false;
            }

            _changed -= callback;
            _changed += callback;
            if (_listenerRegistered)
            {
                return true;
            }

            try
            {
                AddChangeListenerMethod.Invoke(
                    null,
                    new object[] { SavedFiltersChanged });
                _listenerRegistered = true;
                return true;
            }
            catch (Exception)
            {
                _changed -= callback;
                return false;
            }
        }

        public static void RemoveChangedListener(Action callback)
        {
            if (callback != null)
            {
                _changed -= callback;
            }
        }

        private static bool CanRead
        {
            get
            {
                return ConvertToTreeViewMethod != null &&
                    GetFilterMethod != null &&
                    SearchFilterFoldersProperty != null;
            }
        }

        private static bool CanWrite
        {
            get
            {
                return CanRead &&
                    AddSavedFilterMethod != null &&
                    RemoveSavedFilterMethod != null &&
                    SearchFilterType != null;
            }
        }

        private static bool TryGetFavoriteEntries(
            out IReadOnlyList<FavoriteEntry> entries)
        {
            entries = Array.Empty<FavoriteEntry>();
            if (!CanRead)
            {
                return false;
            }

            try
            {
                var root = ConvertToTreeViewMethod.Invoke(
                    null,
                    Array.Empty<object>());
                if (root == null)
                {
                    return false;
                }

                var result = new List<FavoriteEntry>();
                foreach (var item in EnumerateTreeItems(root))
                {
                    if (!TryReadFolderPath(item, out var folderPath) ||
                        !TryGetIntProperty(item, "id", out var instanceId))
                    {
                        continue;
                    }

                    result.Add(new FavoriteEntry(
                        instanceId,
                        folderPath));
                }

                entries = result;
                return true;
            }
            catch (Exception)
            {
                entries = Array.Empty<FavoriteEntry>();
                return false;
            }
        }

        private static IEnumerable<object> EnumerateTreeItems(object root)
        {
            var pending = new Stack<object>();
            PushChildren(root, pending);
            while (pending.Count > 0)
            {
                var item = pending.Pop();
                yield return item;
                PushChildren(item, pending);
            }
        }

        private static void PushChildren(
            object item,
            Stack<object> pending)
        {
            var childrenProperty = item
                .GetType()
                .GetProperty("children", InstancePropertyFlags);
            if (!(childrenProperty?.GetValue(item) is IEnumerable children))
            {
                return;
            }

            var childItems = children
                .Cast<object>()
                .Where(child => child != null)
                .ToArray();
            for (var index = childItems.Length - 1; index >= 0; index--)
            {
                pending.Push(childItems[index]);
            }
        }

        private static bool TryReadFolderPath(
            object treeItem,
            out string folderPath)
        {
            folderPath = null;
            var isFolderProperty = treeItem
                .GetType()
                .GetProperty("isFolder", InstancePropertyFlags);
            if (!(isFolderProperty?.GetValue(treeItem) is bool isFolder) ||
                !isFolder ||
                !TryGetIntProperty(treeItem, "id", out var instanceId))
            {
                return false;
            }

            var filter = GetFilterMethod.Invoke(
                null,
                new object[] { instanceId });
            var paths = SearchFilterFoldersProperty.GetValue(filter)
                as string[];
            if (paths == null || paths.Length != 1)
            {
                return false;
            }

            var normalizedPath = NormalizeFolderPath(paths[0]);
            if (string.IsNullOrEmpty(normalizedPath) ||
                !AssetDatabase.IsValidFolder(normalizedPath))
            {
                return false;
            }

            folderPath = normalizedPath;
            return true;
        }

        private static bool TryGetIntProperty(
            object instance,
            string propertyName,
            out int value)
        {
            value = 0;
            var property = instance
                .GetType()
                .GetProperty(propertyName, InstancePropertyFlags);
            if (!(property?.GetValue(instance) is int propertyValue))
            {
                return false;
            }

            value = propertyValue;
            return true;
        }

        private static string NormalizeFolderPath(string folderPath)
        {
            return string.IsNullOrWhiteSpace(folderPath)
                ? string.Empty
                : folderPath.Replace('\\', '/').TrimEnd('/');
        }

        private static string GetDisplayName(string folderPath)
        {
            var separatorIndex = folderPath.LastIndexOf('/');
            return separatorIndex < 0
                ? folderPath
                : folderPath.Substring(separatorIndex + 1);
        }

        private static void OnSavedFiltersChanged()
        {
            var listeners = _changed;
            if (listeners == null)
            {
                return;
            }

            foreach (Action listener in listeners.GetInvocationList())
            {
                try
                {
                    listener();
                }
                catch (Exception)
                {
                    // A package listener must not break Unity's Favorites UI.
                }
            }
        }

        private readonly struct FavoriteEntry
        {
            public FavoriteEntry(int instanceId, string folderPath)
            {
                InstanceId = instanceId;
                FolderPath = folderPath;
            }

            public int InstanceId { get; }

            public string FolderPath { get; }
        }
    }
}
