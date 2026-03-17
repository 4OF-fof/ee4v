using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.AssetManager.Views.Components;
using _4OF.ee4v.AssetManager.Views.Components.TagListView;
using _4OF.ee4v.AssetManager.Views.Dialog;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views {
    public class TagListView : VisualElement {
        private readonly TagListContent _content;
        private readonly VisualElement _emptyStateContainer;
        private readonly Label _headerLabel;
        private readonly ScrollView _scrollView;

        private readonly Dictionary<string, int> _tagCounts = new();
        private readonly AssetToolbar _toolbar;
        private AssetListService _controller;
        private string _currentSearchText = string.Empty;
        private IAssetRepository _repository;
        private Func<VisualElement, VisualElement> _showDialogCallback;

        public TagListView() {
            style.flexGrow = 1;
            style.backgroundColor = ColorPreset.DefaultBackground;

            _toolbar = new AssetToolbar(0);
            var slider = _toolbar.Q<SliderInt>();
            if (slider != null) slider.style.display = DisplayStyle.None;
            _toolbar.SetSortVisible(false);
            _toolbar.OnBack += () => _controller?.GoBack();
            _toolbar.OnForward += () => _controller?.GoForward();
            _toolbar.OnSearchTextChanged += text =>
            {
                _currentSearchText = text;
                Refresh();
            };
            Add(_toolbar);

            _scrollView = new ScrollView {
                style = {
                    flexGrow = 1,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 10,
                    paddingBottom = 10
                }
            };
            Add(_scrollView);

            _headerLabel = new Label {
                style = {
                    fontSize = 24,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10,
                    marginTop = 5,
                    color = ColorPreset.TextColor
                }
            };
            _scrollView.Add(_headerLabel);

            _content = new TagListContent();
            _content.OnTagSelected += tag => OnTagSelected?.Invoke(tag);
            _content.OnTagRightClicked += ShowContextMenu;
            _scrollView.Add(_content);

            _emptyStateContainer = CreateEmptyState();
            Add(_emptyStateContainer);
        }

        public event Action<string> OnTagSelected;
        public event Action<string, string> OnTagRenamed;
        public event Action<string> OnTagDeleted;

        public void Initialize(IAssetRepository repository) {
            _repository = repository;
            Refresh();
        }

        public void SetController(AssetListService controller) {
            if (_controller != null) {
                _controller.OnHistoryChanged -= UpdateNavigationState;
                _controller.BreadcrumbsChanged -= UpdateBreadcrumbs;
            }

            _controller = controller;

            if (_controller == null) return;
            _controller.OnHistoryChanged += UpdateNavigationState;
            _controller.BreadcrumbsChanged += UpdateBreadcrumbs;
            UpdateNavigationState();
        }

        public void SetShowDialogCallback(Func<VisualElement, VisualElement> callback) {
            _showDialogCallback = callback;
        }

        public void Refresh() {
            if (_repository == null) return;

            CalculateTagCounts();

            var allTags = _tagCounts.Keys.ToList();
            allTags.Sort();

            if (allTags.Count == 0) {
                SetEmptyState(true);
                return;
            }

            SetEmptyState(false);

            var totalCount = _tagCounts.Count;
            _headerLabel.text = I18N.Get("UI.AssetManager.TagListView.AllTags", totalCount);

            if (!string.IsNullOrWhiteSpace(_currentSearchText)) {
                var filtered = allTags
                    .Where(t => t.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(GetTagCount)
                    .ThenBy(t => t)
                    .ToList();
                _content.DrawFlatList(filtered, GetTagCount);
            }
            else {
                var root = BuildTagTree(allTags);
                _content.DrawHierarchyList(root, GetTagCount, GetNodeTagCount);
            }
        }

        private void CalculateTagCounts() {
            _tagCounts.Clear();

            var assets = _repository.GetAllAssets();
            foreach (var asset in assets) {
                if (asset.IsDeleted || asset.Tags == null) continue;
                foreach (var tag in asset.Tags) {
                    _tagCounts.TryAdd(tag, 0);
                    _tagCounts[tag]++;
                }
            }

            var lib = _repository.GetLibraryMetadata();
            if (lib != null) CountFoldersRecursive(lib.FolderList);
        }

        private void CountFoldersRecursive(IEnumerable<BaseFolder> folders) {
            if (folders == null) return;
            foreach (var folder in folders) {
                if (folder.Tags != null)
                    foreach (var tag in folder.Tags) {
                        _tagCounts.TryAdd(tag, 0);
                        _tagCounts[tag]++;
                    }

                if (folder is Folder f) CountFoldersRecursive(f.Children);
            }
        }

        private int GetTagCount(string tag) {
            return _tagCounts.GetValueOrDefault(tag, 0);
        }

        private static int GetNodeTagCount(TagNode node) {
            var count = 0;
            foreach (var child in node.Children.Values) {
                if (!string.IsNullOrEmpty(child.FullPath)) count++;
                if (child.Children.Count > 0) count += GetNodeTagCount(child);
            }

            return count;
        }

        private static TagNode BuildTagTree(List<string> tags) {
            var root = new TagNode("root", null);
            foreach (var tag in tags) {
                var parts = tag.Split('/');
                var current = root;
                foreach (var part in parts) {
                    if (!current.Children.TryGetValue(part, out var nextNode)) {
                        nextNode = new TagNode(part, null);
                        current.Children[part] = nextNode;
                    }

                    current = nextNode;
                }

                current.FullPath = tag;
            }

            return root;
        }

        private void ShowContextMenu(string fullPath, VisualElement target) {
            var menu = new GenericDropdownMenu();
            menu.AddItem(I18N.Get("UI.AssetManager.TagListView.Rename"), false, () => ShowRenameDialog(fullPath));
            menu.AddItem(I18N.Get("UI.AssetManager.TagListView.Delete"), false, () =>
            {
                OnTagDeleted?.Invoke(fullPath);
                Refresh();
            });

            var rect = target.worldBound;
            var menuRect = new Rect(rect.x, rect.yMax, Mathf.Max(rect.width, 100), 0);
            menu.DropDown(menuRect, target);
        }

        private void ShowRenameDialog(string oldTag) {
            var dialog = new TagRenameDialog();
            dialog.OnTagRenamed += (oldName, newName) =>
            {
                OnTagRenamed?.Invoke(oldName, newName);
                Refresh();
            };
            _showDialogCallback?.Invoke(dialog.CreateContent(oldTag));
        }

        private void UpdateNavigationState() {
            if (_controller == null) {
                _toolbar.UpdateNavigationState(false, false);
                return;
            }

            _toolbar.UpdateNavigationState(_controller.CanGoBack, _controller.CanGoForward);
        }

        private void UpdateBreadcrumbs(List<(string Name, Ulid Id)> path) {
            _toolbar.UpdateBreadcrumbs(path);
        }

        private void SetEmptyState(bool isEmpty) {
            _scrollView.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            _emptyStateContainer.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static VisualElement CreateEmptyState() {
            var container = new VisualElement {
                style = {
                    position = Position.Absolute,
                    top = 0, left = 0, right = 0, bottom = 0,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    display = DisplayStyle.None
                }
            };

            var emptyContentRoot = new VisualElement {
                style = { alignItems = Align.Center, paddingTop = 40, paddingBottom = 40 }
            };

            var rawIcon = EditorGUIUtility.IconContent("Search Icon").image as Texture2D;
            var emptyIcon = new Image {
                image = rawIcon,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 64, height = 64, marginBottom = 16, opacity = 0.3f }
            };

            var emptyTitle = new Label(I18N.Get("UI.AssetManager.TagListView.EmptyTitle")) {
                style = {
                    fontSize = 16, unityTextAlign = TextAnchor.MiddleCenter, color = ColorPreset.TipsTitle,
                    marginBottom = 8
                }
            };

            var emptyHint = new Label(I18N.Get("UI.AssetManager.TagListView.EmptyHint")) {
                style = {
                    fontSize = 12, unityTextAlign = TextAnchor.MiddleCenter, color = ColorPreset.TipsText
                }
            };

            emptyContentRoot.Add(emptyIcon);
            emptyContentRoot.Add(emptyTitle);
            emptyContentRoot.Add(emptyHint);
            container.Add(emptyContentRoot);
            return container;
        }
    }
}