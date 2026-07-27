using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ee4v.SceneSwitcher
{
    internal sealed class SceneSwitcherRecord
    {
        public SceneSwitcherRecord(
            string path,
            bool isIgnored = false,
            bool isFavorite = false)
        {
            Path = path ?? string.Empty;
            IsIgnored = isIgnored;
            IsFavorite = isFavorite;
        }

        public string Path { get; }

        public bool IsIgnored { get; set; }

        public bool IsFavorite { get; set; }
    }

    internal sealed class SceneSwitcherItem
    {
        public SceneSwitcherItem(
            string path,
            bool isOpen,
            bool isFavorite)
        {
            Path = path ?? string.Empty;
            Name = System.IO.Path.GetFileNameWithoutExtension(Path);
            Folder = SceneSwitcherPolicy.GetDisplayFolder(Path);
            IsOpen = isOpen;
            IsFavorite = isFavorite;
        }

        public string Path { get; }

        public string Name { get; }

        public string Folder { get; }

        public bool IsOpen { get; }

        public bool IsFavorite { get; }
    }

    internal sealed class SceneSwitcherViewState
    {
        public SceneSwitcherViewState(
            string query,
            IReadOnlyList<SceneSwitcherItem> items,
            bool canCreate)
        {
            Query = query ?? string.Empty;
            Items = items ?? Array.Empty<SceneSwitcherItem>();
            CanCreate = canCreate;
        }

        public string Query { get; }

        public IReadOnlyList<SceneSwitcherItem> Items { get; }

        public bool CanCreate { get; }

        public bool IsFiltered =>
            !string.IsNullOrWhiteSpace(Query);
    }

    internal static class SceneSwitcherPolicy
    {
        public static List<SceneSwitcherRecord> Synchronize(
            IEnumerable<SceneSwitcherRecord> current,
            IEnumerable<string> discoveredPaths)
        {
            var discovered = (discoveredPaths ?? Array.Empty<string>())
                .Where(IsScenePath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var discoveredSet =
                new HashSet<string>(discovered, StringComparer.Ordinal);
            var result = new List<SceneSwitcherRecord>();
            var knownPaths =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (var record in current ??
                     Array.Empty<SceneSwitcherRecord>())
            {
                if (record == null ||
                    !discoveredSet.Contains(record.Path) ||
                    !knownPaths.Add(record.Path))
                {
                    continue;
                }

                result.Add(new SceneSwitcherRecord(
                    record.Path,
                    record.IsIgnored,
                    record.IsFavorite));
            }

            foreach (var path in discovered)
            {
                if (knownPaths.Add(path))
                {
                    result.Add(new SceneSwitcherRecord(path));
                }
            }

            return result;
        }

        public static SceneSwitcherViewState BuildView(
            IEnumerable<SceneSwitcherRecord> records,
            IEnumerable<string> openScenePaths,
            string query)
        {
            var orderedRecords = (records ??
                                  Array.Empty<SceneSwitcherRecord>())
                .Where(record =>
                    record != null &&
                    !record.IsIgnored &&
                    IsScenePath(record.Path))
                .ToArray();
            var openPaths = new HashSet<string>(
                openScenePaths ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var normalizedQuery = (query ?? string.Empty).Trim();
            var filtered = orderedRecords
                .Where(record => Matches(record.Path, normalizedQuery))
                .Select((record, index) => new
                {
                    Record = record,
                    Index = index,
                    Group = openPaths.Contains(record.Path)
                        ? 0
                        : record.IsFavorite
                            ? 1
                            : 2
                })
                .OrderBy(item => item.Group)
                .ThenBy(item => item.Index)
                .Select(item => new SceneSwitcherItem(
                    item.Record.Path,
                    openPaths.Contains(item.Record.Path),
                    item.Record.IsFavorite))
                .ToArray();

            var hasExactSceneName = orderedRecords.Any(record =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(record.Path),
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase));

            return new SceneSwitcherViewState(
                normalizedQuery,
                filtered,
                IsValidSceneName(normalizedQuery) &&
                !hasExactSceneName);
        }

        public static List<SceneSwitcherRecord> Reorder(
            IEnumerable<SceneSwitcherRecord> records,
            IEnumerable<string> orderedVisiblePaths)
        {
            var source = (records ?? Array.Empty<SceneSwitcherRecord>())
                .Where(record => record != null)
                .ToList();
            var byPath = source
                .GroupBy(record => record.Path, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
            var result = new List<SceneSwitcherRecord>();
            var added = new HashSet<string>(StringComparer.Ordinal);

            foreach (var path in orderedVisiblePaths ??
                     Array.Empty<string>())
            {
                if (path != null &&
                    byPath.TryGetValue(path, out var record) &&
                    added.Add(path))
                {
                    result.Add(record);
                }
            }

            foreach (var record in source)
            {
                if (added.Add(record.Path))
                {
                    result.Add(record);
                }
            }

            return result;
        }

        public static bool IsValidSceneName(string sceneName)
        {
            var value = (sceneName ?? string.Empty).Trim();
            return value.Length > 0 &&
                   value != "." &&
                   value != ".." &&
                   value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   value.IndexOf('/') < 0 &&
                   value.IndexOf('\\') < 0;
        }

        public static string NormalizeAssetFolder(string folder)
        {
            var normalized = (folder ?? string.Empty)
                .Trim()
                .Replace('\\', '/')
                .TrimEnd('/');
            if (string.IsNullOrEmpty(normalized))
            {
                return "Assets";
            }

            var segments = normalized.Split('/');
            if (segments.Length == 0 ||
                !string.Equals(
                    segments[0],
                    "Assets",
                    StringComparison.Ordinal))
            {
                return string.Empty;
            }

            for (var i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment) ||
                    segment == "." ||
                    segment == ".." ||
                    segment.IndexOfAny(
                        Path.GetInvalidFileNameChars()) >= 0)
                {
                    return string.Empty;
                }
            }

            return string.Join("/", segments);
        }

        public static string GetDisplayFolder(string scenePath)
        {
            var folder = Path.GetDirectoryName(scenePath ?? string.Empty)
                ?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) ||
                string.Equals(folder, "Assets", StringComparison.Ordinal))
            {
                return "Assets";
            }

            return folder.StartsWith(
                "Assets/",
                StringComparison.Ordinal)
                ? folder.Substring("Assets/".Length)
                : folder;
        }

        private static bool Matches(string path, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return Path.GetFileNameWithoutExtension(path)
                       .IndexOf(
                           query,
                           StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetDisplayFolder(path).IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsScenePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.EndsWith(
                       ".unity",
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
