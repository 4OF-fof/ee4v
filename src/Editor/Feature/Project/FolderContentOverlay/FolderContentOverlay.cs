using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo(
    "Ee4v.FolderContentOverlay.Tests.Editor")]

namespace Ee4v.FolderContentOverlay
{
    [InitializeOnLoad]
    internal static class FolderContentOverlay
    {
        private const float OverlayScale = 0.5f;
        private const float IconPadding = 1f;
        private const float OneColumnLeftInset = 2f;
        private const float IconHeightScale = 0.95f;
        private const string RegistrationId =
            "folder-content-overlay.renderer";

        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f)
        };

        private static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "folderContentOverlay.enabled",
                SettingScope.User,
                "FolderContentOverlay",
                "settings.section.project",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "project",
                    "folder",
                    "content",
                    "overlay"
                });

        private static readonly IconCache Cache = new IconCache();

        static FolderContentOverlay()
        {
            var settings = CoreSettings.Current;
            settings.Register(Enabled);
            InjectorApi.Register(
                new ItemInjectionRegistration(
                    RegistrationId,
                    InjectionChannel.ProjectItem,
                    Draw,
                    priority: 10,
                    isEnabled: () => settings.Get(Enabled)));
            settings.Changed += OnSettingChanged;
            FolderContentOverlayAssetPostprocessor.FoldersChanged +=
                OnFoldersChanged;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(args.Definition, Enabled))
            {
                InjectorApi.Repaint(InjectionChannel.ProjectItem);
            }
        }

        private static void OnFoldersChanged(
            IReadOnlyCollection<string> folderPaths)
        {
            if (folderPaths == null)
            {
                return;
            }

            var invalidated = false;
            foreach (var folderPath in folderPaths)
            {
                invalidated |= Cache.Invalidate(folderPath);
            }

            if (invalidated)
            {
                InjectorApi.Repaint(InjectionChannel.ProjectItem);
            }
        }

        private static void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                Event.current == null ||
                Event.current.type != EventType.Repaint ||
                string.IsNullOrEmpty(context.Guid) ||
                context.SuppressProjectItemIconOverlay)
            {
                return;
            }

            var folderPath = AssetDatabase.GUIDToAssetPath(context.Guid);
            if (string.IsNullOrEmpty(folderPath) ||
                !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var contentIcon = Cache.Get(folderPath);
            if (contentIcon == null)
            {
                return;
            }

            var folderIconRect = GetFolderIconRect(
                context.SelectionRect,
                context.ProjectViewMode,
                context.ProjectOrientation);
            DrawOutlinedIcon(
                GetOverlayRect(folderIconRect),
                contentIcon);
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

        internal static void CollectFolderAndAncestors(
            string folderPath,
            ISet<string> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            var current = folderPath?.Replace('\\', '/').TrimEnd('/');
            while (!string.IsNullOrEmpty(current))
            {
                output.Add(current);

                var parent = Path.GetDirectoryName(current)
                    ?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parent) ||
                    string.Equals(
                        parent,
                        current,
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }
        }

        private static Rect GetFolderIconRect(
            Rect itemRect,
            ProjectItemViewMode viewMode,
            ProjectItemOrientation orientation)
        {
            Rect iconRect;
            if (orientation == ProjectItemOrientation.Vertical ||
                itemRect.height >
                EditorGUIUtility.singleLineHeight * 1.5f)
            {
                iconRect = new Rect(
                    itemRect.x - IconPadding,
                    itemRect.y - IconPadding,
                    itemRect.width + IconPadding * 2f,
                    itemRect.width + IconPadding * 2f);
            }
            else
            {
                var x = viewMode == ProjectItemViewMode.OneColumn
                    ? itemRect.x + OneColumnLeftInset
                    : itemRect.x - IconPadding;
                var size = itemRect.height + IconPadding * 2f;
                iconRect = new Rect(
                    x,
                    itemRect.y - IconPadding,
                    size,
                    size);
            }

            iconRect.height *= IconHeightScale;
            return iconRect;
        }

        private static Rect GetOverlayRect(Rect folderIconRect)
        {
            var width = folderIconRect.width * OverlayScale;
            var height = folderIconRect.height * OverlayScale;
            return new Rect(
                folderIconRect.xMax - width,
                folderIconRect.yMax - height,
                width,
                height);
        }

        private static void DrawOutlinedIcon(Rect rect, Texture icon)
        {
            var previousColor = GUI.color;
            GUI.color = Color.black;
            for (var i = 0; i < OutlineOffsets.Length; i++)
            {
                var offset = OutlineOffsets[i];
                GUI.DrawTexture(
                    new Rect(
                        rect.x + offset.x,
                        rect.y + offset.y,
                        rect.width,
                        rect.height),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
            }

            GUI.color = previousColor;
            GUI.DrawTexture(
                rect,
                icon,
                ScaleMode.ScaleToFit,
                true);
        }

        private sealed class IconCache
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
                if (cachedIcon != null && !IsGenericFileIcon(cachedIcon))
                {
                    return cachedIcon;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var thumbnail = asset == null
                    ? null
                    : AssetPreview.GetMiniThumbnail(asset);
                if (thumbnail != null && !IsGenericFileIcon(thumbnail))
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
                        return GetBuiltinIcon("Prefab Icon", assetType);
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

    internal sealed class FolderContentOverlayAssetPostprocessor
        : AssetPostprocessor
    {
        internal static event Action<IReadOnlyCollection<string>>
            FoldersChanged;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var affectedFolders = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            CollectParentFolders(importedAssets, affectedFolders);
            CollectParentFolders(deletedAssets, affectedFolders);
            CollectParentFolders(movedAssets, affectedFolders);
            CollectParentFolders(movedFromAssetPaths, affectedFolders);

            if (affectedFolders.Count > 0)
            {
                FoldersChanged?.Invoke(affectedFolders
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
            }
        }

        private static void CollectParentFolders(
            IEnumerable<string> assetPaths,
            ISet<string> output)
        {
            if (assetPaths == null)
            {
                return;
            }

            foreach (var assetPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath) ||
                    assetPath.EndsWith(
                        ".meta",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parentPath = Path.GetDirectoryName(assetPath)
                    ?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parentPath))
                {
                    FolderContentOverlay.CollectFolderAndAncestors(
                        parentPath,
                        output);
                }
            }
        }
    }
}
