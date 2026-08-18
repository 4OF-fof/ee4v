using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using UnityEditor;

[assembly: InternalsVisibleTo(
    "Ee4v.FolderContentOverlay.Tests.Editor")]

namespace Ee4v.FolderContentOverlay
{
    [InitializeOnLoad]
    internal static class FolderContentOverlay
    {
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

        private static readonly FolderContentOverlayIconCache Cache =
            new FolderContentOverlayIconCache();

        static FolderContentOverlay()
        {
            var settings = CoreSettings.Current;
            settings.Register(Enabled);
            InjectorApi.Register(
                new ItemInjectionRegistration(
                    "folder-content-overlay.renderer",
                    InjectionChannel.ProjectItem,
                    context => FolderContentOverlayRenderer.Draw(
                        context,
                        Cache),
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
            CollectParentFolders(
                movedFromAssetPaths,
                affectedFolders);

            if (affectedFolders.Count > 0)
            {
                FoldersChanged?.Invoke(affectedFolders
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray());
            }
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
                    CollectFolderAndAncestors(parentPath, output);
                }
            }
        }
    }
}
