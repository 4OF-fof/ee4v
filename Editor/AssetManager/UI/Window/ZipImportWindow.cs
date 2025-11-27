using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.Core.Utility;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class ZipImportWindow : BaseWindow {
        private readonly List<FileNode> _nodes = new();
        private readonly HashSet<string> _selectedPaths = new();

        private AssetService _assetService;
        private IAssetRepository _repository;
        private string _rootPath;
        private Ulid _targetAssetId;
        private string _tempExtractPath;
        private VisualElement _treeContainer;

        protected override void OnDestroy() {
            base.OnDestroy();
            CleanupTempFiles();
        }

        public static void Open(Vector2 screenPosition, Ulid assetId, IAssetRepository repository,
            AssetService assetService) {
            var window = OpenSetup<ZipImportWindow>(screenPosition);
            window._targetAssetId = assetId;
            window._repository = repository;
            window._assetService = assetService;
            window.position = new Rect(screenPosition.x, screenPosition.y, 400, 500);
            window.titleContent = new GUIContent(I18N.Get("UI.AssetManager.ZipImport.Title"));
            window.ExtractZipAndBuildTree();
            window.ShowPopup();
        }

        private void CleanupTempFiles() {
            if (string.IsNullOrEmpty(_tempExtractPath) || !Directory.Exists(_tempExtractPath)) return;
            try {
                Directory.Delete(_tempExtractPath, true);
            }
            catch {
                // ignored
            }

            _tempExtractPath = null;
        }

        private void ExtractZipAndBuildTree() {
            var asset = _repository.GetAsset(_targetAssetId);
            if (asset == null) {
                Close();
                return;
            }

            _tempExtractPath = Path.Combine(Path.GetTempPath(), "ee4v_zip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempExtractPath);

            var zipFiles = _repository.GetAssetFiles(_targetAssetId, "*.zip");
            if (zipFiles == null || zipFiles.Count == 0) {
                EditorUtility.DisplayDialog(I18N.Get("UI.Core.ErrorTitle"), I18N.Get("UI.AssetManager.ZipImport.Error.ZipNotFound"), I18N.Get("UI.Core.OK"));
                Close();
                return;
            }

            var zipPath = zipFiles[0];

            try {
                EditorUtility.DisplayProgressBar(I18N.Get("UI.Common.PleaseWait"), I18N.Get("UI.AssetManager.ZipImport.Progress.Extracting"), 0.5f);
                ZipFile.ExtractToDirectory(zipPath, _tempExtractPath);
            }
            catch (Exception e) {
                EditorUtility.DisplayDialog(I18N.Get("UI.Core.ErrorTitle"), I18N.Get("UI.AssetManager.ZipImport.Error.FailedToExtract", e.Message), I18N.Get("UI.Core.OK"));
                Close();
                return;
            }
            finally {
                EditorUtility.ClearProgressBar();
            }

            _rootPath = _tempExtractPath;
            var zipName = Path.GetFileNameWithoutExtension(zipPath);
            var entries = Directory.GetFileSystemEntries(_tempExtractPath);
            if (entries.Length == 1 && Directory.Exists(entries[0])) {
                var dirName = Path.GetFileName(entries[0]);
                if (string.Equals(dirName, zipName, StringComparison.OrdinalIgnoreCase)) _rootPath = entries[0];
            }

            BuildTreeNodes(_rootPath);
        }

        private void BuildTreeNodes(string rootPath) {
            _nodes.Clear();
            _selectedPaths.Clear();

            var contentFolder = EditorPrefsManager.ContentFolderPath;
            var importDir = Path.Combine(contentFolder, "AssetManager", "Assets", _targetAssetId.ToString(), "Import");

            var existingRelativePaths = new HashSet<string>();
            var hasExistingImports = false;

            if (Directory.Exists(importDir)) {
                var files = Directory.GetFiles(importDir, "*", SearchOption.AllDirectories);
                if (files.Length > 0) {
                    hasExistingImports = true;
                    foreach (var f in files) {
                        var rel = Path.GetRelativePath(importDir, f).Replace('\\', '/');
                        existingRelativePaths.Add(rel);
                    }
                }
            }

            var rootDirInfo = new DirectoryInfo(rootPath);
            var rootNode = new FileNode {
                Name = I18N.Get("UI.AssetManager.ZipImport.Root"),
                IsDirectory = true,
                RelativePath = "",
                IsExpanded = true
            };

            AddDirectoryNodes(rootDirInfo, rootNode, rootPath, hasExistingImports, existingRelativePaths);
            _nodes.Add(rootNode);
        }

        private void AddDirectoryNodes(DirectoryInfo dirInfo, FileNode parentNode, string rootPath,
            bool hasExistingImports, HashSet<string> existingRelativePaths) {
            foreach (var dir in dirInfo.GetDirectories()) {
                var node = new FileNode {
                    Name = dir.Name,
                    IsDirectory = true,
                    RelativePath = Path.GetRelativePath(rootPath, dir.FullName).Replace('\\', '/'),
                    IsExpanded = true
                };
                parentNode.Children.Add(node);
                AddDirectoryNodes(dir, node, rootPath, hasExistingImports, existingRelativePaths);
            }

            foreach (var file in dirInfo.GetFiles()) {
                if (file.Name.StartsWith(".")) continue;

                var relPath = Path.GetRelativePath(rootPath, file.FullName).Replace('\\', '/');
                var node = new FileNode {
                    Name = file.Name,
                    IsDirectory = false,
                    RelativePath = relPath
                };

                var shouldSelect = false;
                if (hasExistingImports) {
                    if (existingRelativePaths.Contains(relPath)) shouldSelect = true;
                }
                else {
                    if (file.Extension.Equals(".unitypackage", StringComparison.OrdinalIgnoreCase)) shouldSelect = true;
                }

                if (shouldSelect) _selectedPaths.Add(relPath);

                parentNode.Children.Add(node);
            }
        }

        private void SelectAllRecursive(FileNode node) {
            if (!node.IsDirectory) _selectedPaths.Add(node.RelativePath);
            foreach (var child in node.Children) SelectAllRecursive(child);
        }

        protected override VisualElement HeaderContent() {
            var label = new Label(I18N.Get("UI.AssetManager.ZipImport.Header")) {
                style = {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    color = ColorPreset.TextColor,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            return label;
        }

        protected override VisualElement Content() {
            var root = base.Content();
            root.style.flexGrow = 1;

            var toolbar = new VisualElement {
                style = { flexDirection = FlexDirection.Row, marginBottom = 5, flexShrink = 0 }
            };

            var selectAllBtn = new Button(() =>
            {
                _selectedPaths.Clear();
                if (_nodes.Count > 0) SelectAllRecursive(_nodes[0]);
                RenderTree();
            }) { text = I18N.Get("UI.AssetManager.ZipImport.SelectAll"), style = { flexGrow = 1 } };

            var deselectAllBtn = new Button(() =>
            {
                _selectedPaths.Clear();
                RenderTree();
            }) { text = I18N.Get("UI.AssetManager.ZipImport.DeselectAll"), style = { flexGrow = 1 } };

            toolbar.Add(selectAllBtn);
            toolbar.Add(deselectAllBtn);
            root.Add(toolbar);

            var scrollArea = new ScrollView {
                style = {
                    flexGrow = 1, backgroundColor = ColorPreset.TransparentBlack10Style, borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderTopColor = ColorPreset.WindowBorder, borderBottomColor = ColorPreset.WindowBorder,
                    borderRightColor = ColorPreset.WindowBorder, borderLeftColor = ColorPreset.WindowBorder
                }
            };

            _treeContainer = new VisualElement();
            scrollArea.Add(_treeContainer);
            root.Add(scrollArea);

            var footer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row, marginTop = 8, justifyContent = Justify.FlexEnd, flexShrink = 0
                }
            };

            var cancelBtn = new Button(Close) { text = I18N.Get("UI.AssetManager.Dialog.Button.Cancel"), style = { width = 80 } };
            var importBtn = new Button(DoImport) {
                text = I18N.Get("UI.AssetManager.ZipImport.Select"),
                style = { width = 80, backgroundColor = ColorPreset.SuccessButtonStyle, color = ColorPreset.TextColor }
            };

            footer.Add(cancelBtn);
            footer.Add(importBtn);
            root.Add(footer);

            RenderTree();
            return root;
        }

        private void RenderTree() {
            _treeContainer.Clear();
            if (_nodes.Count <= 0) return;
            foreach (var child in _nodes[0].Children)
                RenderNodeRecursive(child, _treeContainer);
        }

        private void RenderNodeRecursive(FileNode node, VisualElement parentContainer) {
            var itemContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column
                }
            };

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 20,
                    paddingLeft = 0
                }
            };

            var arrow = new Label(node.IsExpanded ? "▼" : "▶") {
                style = {
                    width = 16,
                    marginLeft = 0,
                    marginRight = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    color = ColorPreset.InActiveItem,
                    visibility = node.IsDirectory ? Visibility.Visible : Visibility.Hidden
                }
            };

            var childrenContainer = new VisualElement {
                style = {
                    paddingLeft = 16,
                    display = node.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None
                }
            };

            var isChecked = IsNodeChecked(node);
            var toggle = new Toggle { value = isChecked, style = { marginLeft = 0, marginRight = 0 } };

            if (node.IsDirectory) {
                arrow.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    node.IsExpanded = !node.IsExpanded;
                    arrow.text = node.IsExpanded ? "▼" : "▶";
                    childrenContainer.style.display = node.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                    evt.StopPropagation();
                });
                row.Add(arrow);
            }
            else {
                toggle.RegisterValueChangedCallback(evt =>
                {
                    SetNodeChecked(node, evt.newValue);
                    RenderTree();
                });
                row.Add(toggle);
            }

            var icon = EditorGUIUtility.IconContent(node.IsDirectory ? "Folder Icon" : "TextAsset Icon").image;
            var iconImg = new Image
                { image = icon, style = { width = 16, height = 16, marginLeft = 4, marginRight = 4 } };
            row.Add(iconImg);

            var label = new Label(node.Name) { style = { fontSize = 12 } };
            row.Add(label);

            itemContainer.Add(row);
            itemContainer.Add(childrenContainer);
            parentContainer.Add(itemContainer);

            if (!node.IsDirectory) return;
            foreach (var child in node.Children)
                RenderNodeRecursive(child, childrenContainer);
        }

        private bool IsNodeChecked(FileNode node) {
            return !node.IsDirectory ? _selectedPaths.Contains(node.RelativePath) : node.Children.All(IsNodeChecked);
        }

        private void SetNodeChecked(FileNode node, bool check) {
            if (!node.IsDirectory) {
                if (check) _selectedPaths.Add(node.RelativePath);
                else _selectedPaths.Remove(node.RelativePath);
            }
            else {
                foreach (var child in node.Children) SetNodeChecked(child, check);
            }
        }

        private void DoImport() {
            if (_selectedPaths.Count == 0) {
                Close();
                return;
            }

            var paths = _selectedPaths.ToList();
            _assetService.ImportFilesFromZip(_targetAssetId, _rootPath, paths);
            Close();
        }

        private class FileNode {
            public readonly List<FileNode> Children = new();
            public bool IsDirectory;
            public bool IsExpanded;
            public string Name;
            public string RelativePath;
        }
    }
}