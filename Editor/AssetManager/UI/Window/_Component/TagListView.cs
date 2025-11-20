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
        private readonly ListView _listView;
        private readonly AssetToolbar _toolbar;
        private List<string> _allTags = new();
        private AssetViewController _controller;
        private string _currentSearchText = string.Empty;
        private List<string> _filteredTags = new();
        private IAssetRepository _repository;

        public TagListView() {
            style.flexGrow = 1;
            style.backgroundColor = ColorPreset.DefaultBackground;

            _toolbar = new AssetToolbar(0);
            var slider = _toolbar.Q<SliderInt>();
            if (slider != null) slider.style.display = DisplayStyle.None;
            Add(_toolbar);

            _listView = new ListView {
                style = { flexGrow = 1 },
                makeItem = () => new Label {
                    style = {
                        height = 24,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        paddingLeft = 4,
                        borderBottomWidth = 1,
                        borderBottomColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.2f))
                    }
                },
                bindItem = (e, i) =>
                {
                    var label = (Label)e;
                    if (i >= 0 && i < _filteredTags.Count)
                        label.text = _filteredTags[i];
                },
                fixedItemHeight = 24,
                selectionType = SelectionType.Single
            };

            _listView.selectionChanged += OnSelectionChanged;
            Add(_listView);

            _toolbar.OnBack += () => _controller?.GoBack();
            _toolbar.OnForward += () => _controller?.GoForward();
            _toolbar.OnSearchTextChanged += text =>
            {
                _currentSearchText = text;
                ApplyFilter();
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
            if (_repository == null) return;

            _allTags = _repository.GetAllTags();
            _allTags?.Sort();

            ApplyFilter();
        }

        private void ApplyFilter() {
            if (string.IsNullOrWhiteSpace(_currentSearchText))
                _filteredTags = new List<string>(_allTags ?? new List<string>());
            else
                _filteredTags = _allTags?
                    .Where(t => t.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList() ?? new List<string>();

            _listView.itemsSource = _filteredTags;
            _listView.Rebuild();
        }

        private void OnSelectionChanged(IEnumerable<object> selection) {
            var selected = selection.FirstOrDefault() as string;
            if (string.IsNullOrEmpty(selected)) return;
            OnTagSelected?.Invoke(selected);
            _listView.ClearSelection();
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
    }
}