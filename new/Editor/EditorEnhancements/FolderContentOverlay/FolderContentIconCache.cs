using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderContentOverlay
{
    internal sealed class FolderContentIconCache
        : IFolderContentIconProvider
    {
        private readonly Dictionary<string, FolderIconSummary>
            _summariesByFolder =
                new Dictionary<string, FolderIconSummary>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly FolderAssetIconResolver _assetIconResolver;

        public FolderContentIconCache(
            FolderAssetIconResolver assetIconResolver)
        {
            _assetIconResolver = assetIconResolver ??
                throw new ArgumentNullException(nameof(assetIconResolver));
        }

        public Texture Get(string folderPath)
        {
            return GetSummary(
                    folderPath,
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase))
                ?.DisplayIcon;
        }

        private FolderIconSummary GetSummary(
            string folderPath,
            ISet<string> resolvingFolders)
        {
            folderPath = NormalizePath(folderPath);
            if (string.IsNullOrEmpty(folderPath))
            {
                return null;
            }

            if (_summariesByFolder.TryGetValue(
                    folderPath,
                    out var cachedSummary))
            {
                return cachedSummary;
            }

            if (!resolvingFolders.Add(folderPath))
            {
                return null;
            }

            var summary = FindRepresentativeIcon(
                folderPath,
                resolvingFolders);
            resolvingFolders.Remove(folderPath);
            _summariesByFolder[folderPath] = summary;
            return summary;
        }

        public bool Invalidate(string folderPath)
        {
            folderPath = NormalizePath(folderPath);
            return !string.IsNullOrEmpty(folderPath) &&
                _summariesByFolder.Remove(folderPath);
        }

        private FolderIconSummary FindRepresentativeIcon(
            string folderPath,
            ISet<string> resolvingFolders)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return FolderIconSummary.Empty;
            }

            var candidates = new List<Texture>();
            var assetPaths = AssetDatabase.FindAssets(
                    string.Empty,
                    new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    !string.IsNullOrEmpty(path) &&
                    !AssetDatabase.IsValidFolder(path) &&
                    IsDirectChild(folderPath, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var assetPath in assetPaths)
            {
                var icon = _assetIconResolver.Resolve(assetPath);
                if (icon != null)
                {
                    candidates.Add(icon);
                }
            }

            var childFolders = AssetDatabase.GetSubFolders(folderPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            foreach (var childFolder in childFolders)
            {
                var childSummary = GetSummary(
                    childFolder,
                    resolvingFolders);
                if (childSummary?.PropagatedIcon != null)
                {
                    candidates.Add(childSummary.PropagatedIcon);
                }
            }

            return new FolderIconSummary(
                SelectRepresentativeIcon(candidates),
                SelectMajorityRepresentativeIcon(candidates));
        }

        internal static Texture SelectRepresentativeIcon(
            IEnumerable<Texture> icons)
        {
            var groups = icons
                .Where(icon => icon != null)
                .GroupBy(icon => icon)
                .OrderByDescending(group => group.Count())
                .ThenBy(
                    group => group.Key.name,
                    StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();

            if (groups.Length == 0)
            {
                return null;
            }

            return groups.Length > 1 &&
                groups[0].Count() == groups[1].Count()
                    ? null
                    : groups[0].Key;
        }

        internal static Texture SelectMajorityRepresentativeIcon(
            IEnumerable<Texture> icons)
        {
            var candidates = icons
                .Where(icon => icon != null)
                .ToArray();
            var leadingGroup = candidates
                .GroupBy(icon => icon)
                .OrderByDescending(group => group.Count())
                .ThenBy(
                    group => group.Key.name,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return leadingGroup != null &&
                leadingGroup.Count() * 2 > candidates.Length
                    ? leadingGroup.Key
                    : null;
        }

        private static bool IsDirectChild(
            string folderPath,
            string assetPath)
        {
            var parentPath = NormalizePath(Path.GetDirectoryName(assetPath));
            return string.Equals(
                parentPath,
                folderPath,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? null
                : path.Replace('\\', '/').TrimEnd('/');
        }

        private sealed class FolderIconSummary
        {
            public static readonly FolderIconSummary Empty =
                new FolderIconSummary(null, null);

            public FolderIconSummary(
                Texture displayIcon,
                Texture propagatedIcon)
            {
                DisplayIcon = displayIcon;
                PropagatedIcon = propagatedIcon;
            }

            public Texture DisplayIcon { get; }

            public Texture PropagatedIcon { get; }
        }
    }
}
