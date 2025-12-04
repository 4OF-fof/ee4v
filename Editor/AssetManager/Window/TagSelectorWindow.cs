using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Window {
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

            var titleLabel = new Label(I18N.Get("UI.AssetManager.TagSelector.Title")) {
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
            var noResultLabel = new Label(string.IsNullOrEmpty(searchText)
                ? I18N.Get("UI.AssetManager.TagSelector.NoTags")
                : I18N.Get("UI.AssetManager.TagSelector.NoMatchingTags")) {
                style = {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ColorPreset.InactiveItem,
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
                text = I18N.Get("UI.AssetManager.TagSelector.CreateTagFmt", tagName),
                style = {
                    height = 24,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = ColorPreset.Success,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopLeftRadius = 12,
                    borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12,
                    borderBottomRightRadius = 12,
                    color = ColorPreset.TextColor
                }
            };

            button.RegisterCallback<MouseEnterEvent>(_ => { button.style.backgroundColor = ColorPreset.SuccessHover; });
            button.RegisterCallback<MouseLeaveEvent>(_ => { button.style.backgroundColor = ColorPreset.Success; });

            return button;
        }

        private Button CreateTagButton(string tag) {
            var count = _tagUsageCount.GetValueOrDefault(tag, 0);

            var button = new Button(() =>
            {
                OnTagSelected?.Invoke(tag);
                _searchField.value = string.Empty;
            }) {
                text = I18N.Get("UI.AssetManager.TagSelector.TagWithCount", tag, count),
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
                    backgroundColor = ColorPreset.TagPillBackground,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    color = ColorPreset.TextColor
                }
            };

            button.RegisterCallback<MouseEnterEvent>(_ => { button.style.backgroundColor = ColorPreset.TagPillHover; });
            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = ColorPreset.TagPillBackground;
            });

            return button;
        }
    }
}