using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.UI.Dialog;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Component {
    public class TagListView : VisualElement {
        private readonly VisualElement _emptyStateContainer;
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private readonly ScrollView _scrollView;

        private readonly Dictionary<string, int> _tagCounts = new();
        private readonly AssetToolbar _toolbar;
        private AssetViewController _controller;
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

            _emptyStateContainer = new VisualElement {
                style = {
                    position = Position.Absolute,
                    top = 0,
                    left = 0,
                    right = 0,
                    bottom = 0,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    display = DisplayStyle.None
                }
            };

            var emptyContentRoot = new VisualElement {
                style = {
                    alignItems = Align.Center,
                    paddingTop = 40,
                    paddingBottom = 40
                }
            };

            var rawIcon = EditorGUIUtility.IconContent("Search Icon").image as Texture2D;
            if (rawIcon != null) rawIcon.filterMode = FilterMode.Bilinear;

            var emptyIcon = new Image {
                image = rawIcon,
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    width = 64,
                    height = 64,
                    marginBottom = 16,
                    opacity = 0.3f
                }
            };

            var emptyTitle = new Label(I18N.Get("UI.AssetManager.TagListView.EmptyTitle")) {
                style = {
                    fontSize = 16,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ColorPreset.TransparentWhite50,
                    marginBottom = 8
                }
            };

            var emptyHint = new Label(I18N.Get("UI.AssetManager.TagListView.EmptyHint")) {
                style = {
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ColorPreset.TransparentWhite30
                }
            };

            emptyContentRoot.Add(emptyIcon);
            emptyContentRoot.Add(emptyTitle);
            emptyContentRoot.Add(emptyHint);
            _emptyStateContainer.Add(emptyContentRoot);
            Add(_emptyStateContainer);

            _toolbar.OnBack += () => _controller?.GoBack();
            _toolbar.OnForward += () => _controller?.GoForward();
            _toolbar.OnSearchTextChanged += text =>
            {
                _currentSearchText = text;
                Refresh();
            };
        }

        public void Initialize(IAssetRepository repository) {
            _repository = repository;
            Refresh();
        }

        public void SetController(AssetViewController controller) {
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

        public event Action<string> OnTagSelected;
        public event Action<string, string> OnTagRenamed;
        public event Action<string> OnTagDeleted;

        public void Refresh() {
            _scrollView.Clear();
            if (_repository == null) return;

            CalculateTagCounts();

            var allTags = _tagCounts.Keys.ToList();
            allTags.Sort();

            if (allTags.Count == 0) {
                UpdateEmptyState(true);
                return;
            }

            UpdateEmptyState(false);

            var totalCount = _tagCounts.Values.Sum();
            var header = new Label(I18N.Get("UI.AssetManager.TagListView.AllTags", totalCount)) {
                style = {
                    fontSize = 24,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10,
                    marginTop = 5,
                    color = ColorPreset.TextColor
                }
            };
            _scrollView.Add(header);

            if (!string.IsNullOrWhiteSpace(_currentSearchText))
                DrawFlatList(allTags);
            else
                DrawHierarchyList(allTags);
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

        private void DrawFlatList(List<string> allTags) {
            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };
            _scrollView.Add(container);

            var filtered = allTags
                .Where(t => t.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(GetTagCount)
                .ThenBy(t => t);

            foreach (var tag in filtered) {
                var count = GetTagCount(tag);
                container.Add(CreateTagButton(tag, tag, count));
            }
        }

        private void DrawHierarchyList(List<string> allTags) {
            var root = BuildTagTree(allTags);
            RenderTreeNodes(root, _scrollView);
        }

        private void RenderTreeNodes(TagNode node, VisualElement container) {
            var leafContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 4
                }
            };
            container.Add(leafContainer);

            var sortedChildren = node.Children.Values
                .OrderByDescending(n => string.IsNullOrEmpty(n.FullPath) ? 0 : GetTagCount(n.FullPath))
                .ThenBy(n => n.Name)
                .ToList();

            foreach (var child in sortedChildren) {
                var isFolder = child.Children.Count > 0;

                if (isFolder) {
                    if (!string.IsNullOrEmpty(child.FullPath)) {
                        var count = GetTagCount(child.FullPath);
                        var selfBtn = CreateTagButton(child.Name, child.FullPath, count);
                        leafContainer.Add(selfBtn);
                    }

                    var groupTagCount = GetNodeTagCount(child);
                    var groupName = I18N.Get("UI.AssetManager.TagListView.GroupNameFmt", child.Name, groupTagCount);

                    var foldoutKey = child.FullPath ?? child.Name;
                    var isOpen = _foldoutStates.GetValueOrDefault(foldoutKey, false);

                    var foldout = new Foldout {
                        text = groupName,
                        value = isOpen,
                        style = {
                            marginLeft = 0,
                            marginTop = 8,
                            marginBottom = 4,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            fontSize = 24
                        }
                    };
                    foldout.Q<Toggle>().style.fontSize = 24;

                    foldout.RegisterValueChangedCallback(evt => { _foldoutStates[foldoutKey] = evt.newValue; });

                    container.Add(foldout);
                    RenderTreeNodes(child, foldout.contentContainer);
                }
                else {
                    var count = GetTagCount(child.FullPath);
                    var btn = CreateTagButton(child.Name, child.FullPath, count);
                    leafContainer.Add(btn);
                }
            }
        }

        private Button CreateTagButton(string displayName, string fullPath, int count) {
            var btn = new Button(() => OnTagSelected?.Invoke(fullPath)) {
                text = I18N.Get("UI.AssetManager.TagListView.TagWithCountFmt", displayName, count),
                tooltip = fullPath,
                style = {
                    height = 24,
                    borderTopLeftRadius = 12,
                    borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12,
                    borderBottomRightRadius = 12,
                    paddingLeft = 10,
                    paddingRight = 10,
                    marginRight = 4,
                    marginBottom = 4,
                    backgroundColor = ColorPreset.TagPillBackgroundStyle,
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                    color = ColorPreset.TextColor,
                    fontSize = 11
                }
            };

            btn.RegisterCallback<MouseEnterEvent>(_ =>
                btn.style.backgroundColor = ColorPreset.TagPillHoverStyle);
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
                btn.style.backgroundColor = ColorPreset.TagPillBackgroundStyle);

            btn.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                evt.StopPropagation();
                ShowContextMenu(btn, fullPath);
            });

            return btn;
        }

        private void ShowContextMenu(VisualElement target, string fullPath) {
            var menu = new GenericDropdownMenu();
            menu.AddItem(I18N.Get("UI.AssetManager.TagListView.Rename"), false, () => ShowRenameDialog(fullPath));
            menu.AddItem(I18N.Get("UI.AssetManager.TagListView.Delete"), false, () => DeleteTag(fullPath));

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

        private void DeleteTag(string tag) {
            OnTagDeleted?.Invoke(tag);
            Refresh();
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

        private void UpdateEmptyState(bool isEmpty) {
            if (isEmpty) {
                _scrollView.style.display = DisplayStyle.None;
                _emptyStateContainer.style.display = DisplayStyle.Flex;
            }
            else {
                _scrollView.style.display = DisplayStyle.Flex;
                _emptyStateContainer.style.display = DisplayStyle.None;
            }
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

        private class TagNode {
            public TagNode(string name, string fullPath) {
                Name = name;
                FullPath = fullPath;
            }

            public string Name { get; }
            public string FullPath { get; set; }
            public Dictionary<string, TagNode> Children { get; } = new();
        }
    }
}