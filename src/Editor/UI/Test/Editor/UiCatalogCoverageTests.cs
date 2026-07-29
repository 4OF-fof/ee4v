using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.UI.Tests
{
    public sealed class UiCatalogCoverageTests
    {
        private static readonly string[] RequiredScreenStoryIds =
        {
            "asset-manager-window",
            "asset-manager-navigation-window",
            "asset-manager-main-view-window",
            "asset-manager-infomation-window",
            "asset-manager-collection-creation-window",
            "context-menu-window",
            "image-tooltip",
            "folder-style-window-screen",
            "hierarchy-style-window-screen",
            "hidden-objects-window-screen",
            "project-tabs-screen",
            "scene-switcher-screen",
            "feature-test-manager-screen",
            "user-settings-screen",
            "project-settings-screen"
        };

        private static readonly string[] RequiredComponentStoryIds =
        {
            "alerts",
            "asset-collection-icon-selector",
            "asset-item-grid",
            "asset-manager-infomation-panel",
            "asset-manager-main-view",
            "asset-manager-navigation-panel",
            "collection-navigation-list",
            "comma-separated-list-field",
            "copyable-text-area",
            "decoration-style-editor",
            "decoration-style-window-layout",
            "diff-confirmation-overlay",
            "draggable-toggle-group",
            "file-icon-catalog",
            "file-tree-detail-view",
            "hidden-object-tree-row",
            "hidden-object-tree-view",
            "hidden-objects-footer",
            "hidden-objects-toolbar",
            "hidden-objects-view",
            "history-navigation",
            "history-navigation-overlay",
            "icon",
            "image-stack",
            "info-card",
            "input-field",
            "item-card",
            "item-grid",
            "item-image",
            "main-toolbar",
            "numeric-slider",
            "project-tabs-host",
            "project-tabs-view",
            "reorderable-list-field",
            "scene-switcher-row",
            "scene-switcher-setting-field",
            "scene-switcher-view",
            "search-field",
            "searchable-file-tree",
            "searchable-tree-view",
            "selectable-item-grid",
            "single-select-button-group",
            "source-priority-setting",
            "status-badge",
            "status-overlay",
            "tab-card",
            "tag-list-page",
            "test-result-group",
            "three-pane-layout",
            "ui-button",
            "url-input-field",
            "view-toggle-tabs",
            "window-toast"
        };

        private static readonly string[] RequiredImguiStoryIds =
        {
            "depth-indicator-renderer",
            "folder-content-overlay-renderer",
            "folder-style-renderer",
            "hierarchy-decoration-renderer",
            "hierarchy-style-renderer",
            "hidden-object-hierarchy-button",
            "scene-switcher-hierarchy-trigger"
        };

        private static readonly Type[] RequiredGenericUiTypes =
        {
            typeof(Alerts),
            typeof(CommaSeparatedListField),
            typeof(ContextMenu),
            typeof(ContextMenuWindow),
            typeof(CopyableTextArea),
            typeof(DecorationStyleEditor),
            typeof(DecorationStyleWindowLayout),
            typeof(DiffConfirmationOverlay),
            typeof(DraggableToggleGroup),
            typeof(HistoryNavigationOverlay),
            typeof(Icon),
            typeof(ImageStack),
            typeof(ImageTooltip),
            typeof(ImageTooltipWindow),
            typeof(InfoCard),
            typeof(InputField),
            typeof(ItemCard),
            typeof(ItemGrid),
            typeof(ItemImage),
            typeof(NumericSlider),
            typeof(ReorderableListField),
            typeof(SearchField),
            typeof(SearchableTreeView<>),
            typeof(SelectableItemGrid),
            typeof(SingleSelectButtonGroup),
            typeof(StatusBadge),
            typeof(StatusOverlay),
            typeof(TabCard),
            typeof(ThreePaneLayout),
            typeof(UiButton),
            typeof(UrlInputField),
            typeof(ViewToggleTabs),
            typeof(WindowToast),
            typeof(WindowToastHost)
        };

        [Test]
        [FeatureTestCase(
            "すべての画面がCatalogへ登録される",
            "独立window、Settings画面、Project/Hierarchy注入UIに対応するstoryが存在することを確認します。",
            order: 220,
            category: FeatureTestCategory.Ui)]
        public void Catalog_RegistersAllScreensAndInjectedUi()
        {
            var stories = CatalogWindow
                .GetRegisteredStoriesForTests()
                .ToDictionary(
                    story => story.Id,
                    StringComparer.Ordinal);

            Assert.That(
                RequiredScreenStoryIds
                    .Where(id => !stories.ContainsKey(id)),
                Is.Empty);
            Assert.That(
                RequiredComponentStoryIds
                    .Where(id => !stories.ContainsKey(id)),
                Is.Empty);
            Assert.That(
                RequiredImguiStoryIds
                    .Where(id =>
                        !stories.TryGetValue(id, out var story) ||
                        story.Implementation !=
                        CatalogWindow.ComponentImplementationKind.Imgui),
                Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "UI用USSがCatalogへ登録される",
            "Foundation token以外の全USSを走査し、Catalog registrarのstylesheet登録漏れを検出します。",
            order: 221,
            category: FeatureTestCategory.Ui)]
        public void Catalog_RegistersAllUiStyleSheets()
        {
            var editorRoot = GetEditorRootFullPath();
            Assert.That(editorRoot, Is.Not.Null.And.Not.Empty);

            var registered = new HashSet<string>(
                CatalogWindow.GetRegisteredStyleSheetPathsForTests(),
                StringComparer.Ordinal);
            var missing = Directory
                .EnumerateFiles(
                    editorRoot,
                    "*.uss",
                    SearchOption.AllDirectories)
                .Select(path =>
                    "Editor/" + GetRelativePath(editorRoot, path))
                .Where(path =>
                    !path.StartsWith(
                        "Editor/UI/Foundation/",
                        StringComparison.Ordinal) &&
                    !registered.Contains(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }

        [Test]
        [FeatureTestCase(
            "汎用Catalog UIはUI assemblyが所有する",
            "Domain外へ登録する汎用componentとlayoutがEe4v.UI namespaceおよびEe4v.UI.Editor assemblyに存在することを確認します。",
            order: 222,
            category: FeatureTestCategory.Ui)]
        public void Catalog_GenericUiIsOwnedByUiAssembly()
        {
            var misplaced = RequiredGenericUiTypes
                .Where(type =>
                    !string.Equals(
                        type.Namespace,
                        "Ee4v.UI",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        type.Assembly.GetName().Name,
                        "Ee4v.UI.Editor",
                        StringComparison.Ordinal))
                .Select(type =>
                    type.FullName + " (" +
                    type.Assembly.GetName().Name + ")")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(misplaced, Is.Empty, string.Join("\n", misplaced));
        }

        private static string GetEditorRootFullPath()
        {
            var anchorAssetPath =
                AssetDatabase.FindAssets("Ee4vPackageAnchor")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => path.EndsWith(
                        "Editor/Core/Internal/Ee4vPackageAnchor.cs",
                        StringComparison.Ordinal));
            if (string.IsNullOrEmpty(anchorAssetPath))
            {
                return null;
            }

            var packageRootAssetPath = Path.GetDirectoryName(
                Path.GetDirectoryName(
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(anchorAssetPath))));
            return string.IsNullOrEmpty(packageRootAssetPath)
                ? null
                : Path.Combine(
                    Path.GetFullPath(packageRootAssetPath),
                    "Editor");
        }

        private static string GetRelativePath(
            string rootPath,
            string path)
        {
            var normalizedRoot = rootPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return path.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase)
                ? path.Substring(normalizedRoot.Length)
                    .Replace('\\', '/')
                : path.Replace('\\', '/');
        }
    }
}
