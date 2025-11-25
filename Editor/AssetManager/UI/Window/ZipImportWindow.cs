using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class ZipImportWindow : BaseWindow {
        private readonly List<FileNode> _nodes = new();

        private readonly HashSet<string> _selectedPaths = new();
        private AssetService _assetService;
        private IAssetRepository _repository;
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
            window.titleContent = new GUIContent("Import from ZIP");
            window.ExtractZipAndBuildTree();
            window.ShowPopup();
        }

        private void CleanupTempFiles() {
            if (!string.IsNullOrEmpty(_tempExtractPath) && Directory.Exists(_tempExtractPath)) {
                try {
                    Directory.Delete(_tempExtractPath, true);
                }
                catch (Exception e) {
                    Debug.LogWarning($"[ee4v] Failed to cleanup temp zip folder: {e.Message}");
                }

                _tempExtractPath = null;
            }
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
                EditorUtility.DisplayDialog("Error", "ZIP file not found in asset directory.", "OK");
                Close();
                return;
            }

            var zipPath = zipFiles[0];

            try {
                ZipFile.ExtractToDirectory(zipPath, _tempExtractPath);
            }
            catch (Exception e) {
                EditorUtility.DisplayDialog("Error", $"Failed to extract ZIP: {e.Message}", "OK");
                Close();
                return;
            }

            BuildTreeNodes(_tempExtractPath);
        }

        private void BuildTreeNodes(string rootPath) {
            _nodes.Clear();
            _selectedPaths.Clear();

            var rootDirInfo = new DirectoryInfo(rootPath);
            var rootNode = new FileNode {
                Name = "Root",
                IsDirectory = true,
                RelativePath = ""
            };

            AddDirectoryNodes(rootDirInfo, rootNode, rootPath);
            _nodes.Add(rootNode);

            SelectAllRecursive(rootNode);
        }

        private void SelectAllRecursive(FileNode node) {
            if (!node.IsDirectory) _selectedPaths.Add(node.RelativePath);
            foreach (var child in node.Children) SelectAllRecursive(child);
        }

        private void AddDirectoryNodes(DirectoryInfo dirInfo, FileNode parentNode, string rootPath) {
            foreach (var dir in dirInfo.GetDirectories()) {
                var node = new FileNode {
                    Name = dir.Name,
                    IsDirectory = true,
                    RelativePath = Path.GetRelativePath(rootPath, dir.FullName)
                };
                parentNode.Children.Add(node);
                AddDirectoryNodes(dir, node, rootPath);
            }

            foreach (var file in dirInfo.GetFiles()) {
                if (file.Name.StartsWith(".")) continue;

                var node = new FileNode {
                    Name = file.Name,
                    IsDirectory = false,
                    RelativePath = Path.GetRelativePath(rootPath, file.FullName)
                };
                parentNode.Children.Add(node);
            }
        }

        protected override VisualElement HeaderContent() {
            var label = new Label("Import Content Selection") {
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

            var toolbar = new VisualElement {
                style = { flexDirection = FlexDirection.Row, marginBottom = 5, flexShrink = 0 }
            };

            var selectAllBtn = new Button(() =>
            {
                _selectedPaths.Clear();
                if (_nodes.Count > 0) SelectAllRecursive(_nodes[0]);
                RenderTree();
            }) { text = "Select All", style = { flexGrow = 1 } };

            var deselectAllBtn = new Button(() =>
            {
                _selectedPaths.Clear();
                RenderTree();
            }) { text = "Deselect All", style = { flexGrow = 1 } };

            toolbar.Add(selectAllBtn);
            toolbar.Add(deselectAllBtn);
            root.Add(toolbar);

            var scrollArea = new ScrollView {
                style = {
                    flexGrow = 1, backgroundColor = new Color(0, 0, 0, 0.1f), borderTopWidth = 1, borderBottomWidth = 1,
                    borderTopColor = ColorPreset.WindowBorder, borderBottomColor = ColorPreset.WindowBorder,
                    borderRightColor = ColorPreset.WindowBorder, borderLeftColor = ColorPreset.WindowBorder
                }
            };

            _treeContainer = new VisualElement();
            scrollArea.Add(_treeContainer);
            root.Add(scrollArea);

            var footer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, marginTop = 8, justifyContent = Justify.FlexEnd, flexShrink = 0 }
            };

            var cancelBtn = new Button(Close) { text = "Cancel", style = { width = 80 } };
            var importBtn = new Button(DoImport) {
                text = "Import",
                style = { width = 80, backgroundColor = new Color(0.2f, 0.5f, 0.2f), color = Color.white }
            };

            footer.Add(cancelBtn);
            footer.Add(importBtn);
            root.Add(footer);

            RenderTree();
            return root;
        }

        private void RenderTree() {
            _treeContainer.Clear();
            if (_nodes.Count > 0)
                foreach (var child in _nodes[0].Children)
                    RenderNode(child, 0, _treeContainer);
        }

        private void RenderNode(FileNode node, int depth, VisualElement container) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = depth * 16 + 4,
                    height = 20
                }
            };

            var isChecked = IsNodeChecked(node);
            var toggle = new Toggle { value = isChecked };
            toggle.RegisterValueChangedCallback(evt =>
            {
                SetNodeChecked(node, evt.newValue);
                RenderTree();
            });
            row.Add(toggle);

            var icon = EditorGUIUtility.IconContent(node.IsDirectory ? "Folder Icon" : "TextAsset Icon").image;
            var iconImg = new Image { image = icon, style = { width = 16, height = 16, marginRight = 4 } };
            row.Add(iconImg);

            var label = new Label(node.Name) { style = { fontSize = 12 } };
            row.Add(label);

            container.Add(row);

            if (!node.IsDirectory) return;
            foreach (var child in node.Children) RenderNode(child, depth + 1, container);
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
            _assetService.ImportFilesFromZip(_targetAssetId, _tempExtractPath, paths);

            AssetManagerWindow.ShowToastMessage($"{paths.Count} items imported.", 3f, ToastType.Success);
            Close();
        }

        private class FileNode {
            public readonly List<FileNode> Children = new();
            public bool IsDirectory;
            public string Name;
            public string RelativePath;
        }
    }
}