using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class TagListView : VisualElement {
        private readonly ScrollView _scrollView;

        private readonly Dictionary<string, int> _tagCounts = new();
        private readonly AssetToolbar _toolbar;
        private AssetViewController _controller;
        private string _currentSearchText = string.Empty;
        private IAssetRepository _repository;

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

        public event Action<string> OnTagSelected;

        public void Refresh() {
            _scrollView.Clear();
            if (_repository == null) return;

            CalculateTagCounts();

            var allTags = _tagCounts.Keys.ToList();
            allTags.Sort();

            if (allTags.Count == 0) {
                var label = new Label("No tags found.") {
                    style = {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        opacity = 0.5f,
                        marginTop = 20
                    }
                };
                _scrollView.Add(label);
                return;
            }

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
                .OrderBy(t => t);

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

            var sortedChildren = node.Children.Values.OrderBy(n => n.Name).ToList();

            foreach (var child in sortedChildren) {
                var isFolder = child.Children.Count > 0;

                if (isFolder) {
                    var foldout = new Foldout {
                        text = child.Name,
                        value = false,
                        style = {
                            marginLeft = 0,
                            marginTop = 4,
                            unityFontStyleAndWeight = FontStyle.Bold
                        }
                    };

                    if (!string.IsNullOrEmpty(child.FullPath)) {
                        var count = GetTagCount(child.FullPath);
                        var selfBtn = CreateTagButton(child.Name, child.FullPath, count);
                        selfBtn.style.marginBottom = 4;
                        foldout.contentContainer.Add(selfBtn);
                    }

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
                text = $"{displayName} ({count})",
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
                    backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                    color = ColorPreset.TextColor,
                    fontSize = 11
                }
            };

            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = ColorPreset.MouseOverBackground);
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
                btn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)));

            return btn;
        }

        private int GetTagCount(string tag) {
            return _tagCounts.GetValueOrDefault(tag, 0);
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