using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderContentOverlay
{
    internal sealed class FolderContentOverlayIconCache
    {
        private readonly Dictionary<string, IconSummary>
            _summariesByFolder =
                new Dictionary<string, IconSummary>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly AssetIconResolver _assetIconResolver =
            new AssetIconResolver();

        public Texture Get(string folderPath)
        {
            return GetSummary(
                    folderPath,
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase))
                ?.DisplayIcon;
        }

        public bool Invalidate(string folderPath)
        {
            folderPath = NormalizePath(folderPath);
            return !string.IsNullOrEmpty(folderPath) &&
                _summariesByFolder.Remove(folderPath);
        }

        internal static IconSummary SummarizeIcons(
            IEnumerable<Texture> icons)
        {
            var groups = icons
                .Where(icon => icon != null)
                .GroupBy(icon => icon)
                .Select(group => new
                {
                    Icon = group.Key,
                    Count = group.Count()
                })
                .OrderByDescending(group => group.Count)
                .ThenBy(
                    group => group.Icon.name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (groups.Length == 0)
            {
                return IconSummary.Empty;
            }

            var leading = groups[0];
            var isTied = groups.Length > 1 &&
                leading.Count == groups[1].Count;
            var total = groups.Sum(group => group.Count);
            return new IconSummary(
                isTied ? null : leading.Icon,
                leading.Count * 2 > total ? leading.Icon : null);
        }

        private IconSummary GetSummary(
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

        private IconSummary FindRepresentativeIcon(
            string folderPath,
            ISet<string> resolvingFolders)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return IconSummary.Empty;
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
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var assetPath in assetPaths)
            {
                var icon = _assetIconResolver.Resolve(assetPath);
                if (icon != null)
                {
                    candidates.Add(icon);
                }
            }

            var childFolders = AssetDatabase.GetSubFolders(folderPath)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase);
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

            return SummarizeIcons(candidates);
        }

        private static bool IsDirectChild(
            string folderPath,
            string assetPath)
        {
            var parentPath = NormalizePath(
                Path.GetDirectoryName(assetPath));
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

        internal sealed class IconSummary
        {
            public static readonly IconSummary Empty =
                new IconSummary(null, null);

            public IconSummary(
                Texture displayIcon,
                Texture propagatedIcon)
            {
                DisplayIcon = displayIcon;
                PropagatedIcon = propagatedIcon;
            }

            public Texture DisplayIcon { get; }

            public Texture PropagatedIcon { get; }
        }

        private sealed class AssetIconResolver
        {
            private static readonly Texture DefaultAssetIcon =
                EditorGUIUtility.IconContent("DefaultAsset Icon").image;

            public Texture Resolve(string assetPath)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return null;
                }

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(
                    assetPath);
                var stableTypeIcon = ResolveStableTypeIcon(
                    assetPath,
                    assetType);
                if (stableTypeIcon != null)
                {
                    return stableTypeIcon;
                }

                var cachedIcon = AssetDatabase.GetCachedIcon(assetPath);
                if (cachedIcon != null &&
                    !IsGenericFileIcon(cachedIcon))
                {
                    return cachedIcon;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var thumbnail = asset == null
                    ? null
                    : AssetPreview.GetMiniThumbnail(asset);
                if (thumbnail != null &&
                    !IsGenericFileIcon(thumbnail))
                {
                    return thumbnail;
                }

                if (cachedIcon != null)
                {
                    return cachedIcon;
                }

                return assetType == null
                    ? null
                    : AssetPreview.GetMiniTypeThumbnail(assetType);
            }

            private static Texture ResolveStableTypeIcon(
                string assetPath,
                Type assetType)
            {
                if (assetType == null)
                {
                    return null;
                }

                if (typeof(Texture).IsAssignableFrom(assetType))
                {
                    return GetBuiltinIcon("Texture Icon", assetType);
                }

                if (typeof(Material).IsAssignableFrom(assetType))
                {
                    return GetBuiltinIcon("Material Icon", assetType);
                }

                if (typeof(Mesh).IsAssignableFrom(assetType))
                {
                    return GetBuiltinIcon("Mesh Icon", assetType);
                }

                if (typeof(GameObject).IsAssignableFrom(assetType))
                {
                    var importer = AssetImporter.GetAtPath(assetPath);
                    if (importer is ModelImporter)
                    {
                        return GetBuiltinIcon(
                            "ModelImporter Icon",
                            assetType);
                    }

                    if (string.Equals(
                            Path.GetExtension(assetPath),
                            ".prefab",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return GetBuiltinIcon(
                            "Prefab Icon",
                            assetType);
                    }
                }

                return null;
            }

            private static Texture GetBuiltinIcon(
                string iconName,
                Type fallbackType)
            {
                return EditorGUIUtility.IconContent(iconName).image ??
                    AssetPreview.GetMiniTypeThumbnail(fallbackType);
            }

            private static bool IsGenericFileIcon(Texture icon)
            {
                if (icon == null)
                {
                    return false;
                }

                if (DefaultAssetIcon != null && icon == DefaultAssetIcon)
                {
                    return true;
                }

                return !string.IsNullOrEmpty(icon.name) &&
                    icon.name.IndexOf(
                        "DefaultAsset",
                        StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
