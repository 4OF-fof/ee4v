using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class TagSelectorWindow : BaseWindow {
        private List<string> _allTags;
        private IAssetRepository _repository;
        private ToolbarSearchField _searchField;
        private VisualElement _tagContainer;
        private Dictionary<string, int> _tagUsageCount;

        protected override void OnLostFocus() {
            base.OnLostFocus();
            Close();
        }

        public event Action<string> OnTagSelected;

        public static void Show(Vector2 screenPosition, IAssetRepository repository, Action<string> onTagSelected) {
            var window = OpenSetup<TagSelectorWindow>(screenPosition);
            window._repository = repository;
            window.OnTagSelected = onTagSelected;
            window.position = new Rect(window.position.x, window.position.y, 300, 400);
            window.ShowPopup();
        }

        protected override VisualElement HeaderContent() {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 24,
                    flexGrow = 1
                }
            };

            var titleLabel = new Label("Select Tag") {
                style = {
                    flexGrow = 1,
                    marginLeft = 8,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            root.Add(titleLabel);

            return root;
        }

        protected override VisualElement Content() {
            var root = base.Content();

            var container = new VisualElement {
                style = {
                    flexGrow = 1,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8
                }
            };

            var searchRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            _searchField = new ToolbarSearchField {
                style = {
                    height = 20,
                    width = Length.Percent(100)
                }
            };
            _searchField.RegisterValueChangedCallback(evt => FilterTags(evt.newValue));
            searchRow.Add(_searchField);
            container.Add(searchRow);

            _searchField.schedule.Execute(() => _searchField?.Q<TextField>()?.Focus()).ExecuteLater(50);

            _tagContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    flexGrow = 1
                }
            };
            container.Add(_tagContainer);

            root.Add(container);

            RefreshTagList();

            return root;
        }

        private void RefreshTagList() {
            if (_repository == null) return;

            _tagUsageCount = new Dictionary<string, int>();
            var allAssets = _repository.GetAllAssets().Where(a => !a.IsDeleted).ToList();

            foreach (var asset in allAssets) {
                if (asset.Tags == null) continue;
                foreach (var tag in asset.Tags) {
                    _tagUsageCount.TryAdd(tag, 0);
                    _tagUsageCount[tag]++;
                }
            }

            var metadata = _repository.GetLibraryMetadata();
            if (metadata?.FolderList != null) CountFolderTags(metadata.FolderList);

            _allTags = _tagUsageCount
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            FilterTags(string.Empty);
        }

        private void CountFolderTags(IEnumerable<BaseFolder> folders) {
            foreach (var folder in folders) {
                if (folder.Tags != null)
                    foreach (var tag in folder.Tags) {
                        _tagUsageCount.TryAdd(tag, 0);
                        _tagUsageCount[tag]++;
                    }

                if (folder is Folder f && f.Children != null) CountFolderTags(f.Children);
            }
        }

        private void FilterTags(string searchText) {
            _tagContainer.Clear();

            var filteredTags = string.IsNullOrEmpty(searchText)
                ? _allTags
                : _allTags.Where(t => t.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var hasExactMatch = filteredTags.Any(t => t.Equals(searchText, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(searchText) && !hasExactMatch) {
                var createButton = CreateNewTagButton(searchText);
                _tagContainer.Add(createButton);
            }

            foreach (var tagButton in filteredTags.Select(CreateTagButton)) _tagContainer.Add(tagButton);

            if (filteredTags.Count != 0 || (!string.IsNullOrWhiteSpace(searchText) && !hasExactMatch)) return;
            var noResultLabel = new Label(string.IsNullOrEmpty(searchText) ? "No tags available" : "No matching tags") {
                style = {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = Color.gray,
                    marginTop = 20,
                    width = Length.Percent(100)
                }
            };
            _tagContainer.Add(noResultLabel);
        }

        private Button CreateNewTagButton(string tagName) {
            var button = new Button(() =>
            {
                OnTagSelected?.Invoke(tagName);
                _searchField.value = string.Empty;
                RefreshTagList();
            }) {
                text = $"+ Create '{tagName}'",
                style = {
                    height = 24,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.2f)),
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopLeftRadius = 12,
                    borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12,
                    borderBottomRightRadius = 12,
                    color = Color.white
                }
            };

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.3f, 0.6f, 0.3f));
            });
            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.2f));
            });

            return button;
        }

        private Button CreateTagButton(string tag) {
            var count = _tagUsageCount.GetValueOrDefault(tag, 0);

            var button = new Button(() =>
            {
                OnTagSelected?.Invoke(tag);
                _searchField.value = string.Empty;
            }) {
                text = $"{tag} ({count})",
                tooltip = tag,
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
                    backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)),
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    color = ColorPreset.TextColor
                }
            };

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            });
            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            });

            return button;
        }
    }
}