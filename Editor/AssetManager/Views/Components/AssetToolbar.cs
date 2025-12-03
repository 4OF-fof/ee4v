using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.State;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components {
    public sealed class AssetToolbar : VisualElement {
        private readonly Label _backLabel;
        private readonly ScrollView _breadcrumbContainer;
        private readonly Label _forwardLabel;
        private readonly Label _sortLabel;

        public AssetToolbar(int initialItemsPerRow) {
            style.alignItems = Align.Center;
            style.flexDirection = FlexDirection.Row;
            style.height = 24;
            style.backgroundColor = ColorPreset.DefaultBackground;

            _backLabel = CreateNavigationLabel("<", I18N.Get("UI.AssetManager.AssetToolbar.Back"));
            _backLabel.RegisterCallback<PointerDownEvent>(_ => OnBack?.Invoke());
            Add(_backLabel);

            _forwardLabel = CreateNavigationLabel(">", I18N.Get("UI.AssetManager.AssetToolbar.Forward"));
            _forwardLabel.RegisterCallback<PointerDownEvent>(_ => OnForward?.Invoke());
            Add(_forwardLabel);

            _breadcrumbContainer = new ScrollView(ScrollViewMode.Horizontal) {
                style = {
                    flexGrow = 1,
                    marginLeft = 4,
                    marginRight = 4,
                    alignContent = Align.Center
                },
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _breadcrumbContainer.contentContainer.style.flexDirection = FlexDirection.Row;
            _breadcrumbContainer.contentContainer.style.alignItems = Align.Center;
            Add(_breadcrumbContainer);

            var slider = new SliderInt(2, 10) {
                value = initialItemsPerRow,
                style = { minWidth = 80, maxWidth = 150, marginRight = 4 }
            };
            slider.RegisterValueChangedCallback(evt => OnItemSizeChanged?.Invoke(evt.newValue));
            Add(slider);

            _sortLabel = new Label {
                tooltip = I18N.Get("UI.AssetManager.AssetToolbar.SortTooltip"),
                style = {
                    height = 20,
                    width = 24,
                    paddingLeft = 2, paddingRight = 2,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    alignSelf = Align.Center,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                }
            };
            var sortIcon = EditorGUIUtility.IconContent("d_AlphabeticalSorting").image;
            if (sortIcon != null)
                _sortLabel.Add(new Image {
                    image = sortIcon,
                    style = { width = 16, height = 16 }
                });
            else
                _sortLabel.text = I18N.Get("UI.AssetManager.AssetToolbar.SortLabel");

            RegisterHoverEvents(_sortLabel);
            _sortLabel.RegisterCallback<PointerDownEvent>(_ => OnSortMenuRequested?.Invoke(_sortLabel));
            Add(_sortLabel);

            var searchField = new ToolbarSearchField {
                style = {
                    width = 200,
                    marginLeft = 4,
                    marginRight = 4,
                    alignSelf = Align.Center
                }
            };
            searchField.RegisterValueChangedCallback(evt => OnSearchTextChanged?.Invoke(evt.newValue));
            Add(searchField);
        }

        public event Action OnBack;
        public event Action OnForward;
        public event Action<int> OnItemSizeChanged;
        public event Action<AssetSortType> OnSortChanged;
        public event Action<string> OnSearchTextChanged;
        public event Action<Ulid> OnBreadcrumbClicked;
        public event Action<VisualElement> OnSortMenuRequested;

        public void SetSortVisible(bool visibleSort) {
            _sortLabel.style.display = visibleSort ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void UpdateNavigationState(bool canGoBack, bool canGoForward) {
            _backLabel.SetEnabled(canGoBack);
            _forwardLabel.SetEnabled(canGoForward);
            _backLabel.style.color = canGoBack ? ColorPreset.TextColor : ColorPreset.InActiveItem;
            _forwardLabel.style.color = canGoForward ? ColorPreset.TextColor : ColorPreset.InActiveItem;
        }

        public void UpdateBreadcrumbs(List<(string Name, Ulid Id)> path) {
            _breadcrumbContainer.Clear();
            if (path == null) return;

            for (var i = 0; i < path.Count; i++) {
                var (itemName, id) = path[i];
                var isLast = i == path.Count - 1;

                var btn = new Button(() => OnBreadcrumbClicked?.Invoke(id)) {
                    text = itemName,
                    style = {
                        backgroundColor = Color.clear,
                        borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                        marginLeft = 0, marginRight = 0, paddingLeft = 2, paddingRight = 2,
                        color = ColorPreset.TextColor,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        fontSize = 12
                    }
                };

                if (isLast) btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                _breadcrumbContainer.Add(btn);

                if (isLast) continue;
                var separator = new Label(">") {
                    style = {
                        marginLeft = 2, marginRight = 2,
                        color = ColorPreset.InActiveItem,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                _breadcrumbContainer.Add(separator);
            }
        }

        private Label CreateNavigationLabel(string text, string labelTooltip) {
            var label = new Label(text) {
                tooltip = labelTooltip,
                style = {
                    width = 24,
                    height = 24,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingLeft = 0, paddingRight = 0,
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center
                }
            };
            RegisterHoverEvents(label);
            return label;
        }

        private void RegisterHoverEvents(VisualElement element) {
            element.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (element.enabledSelf) element.style.backgroundColor = ColorPreset.MouseOverBackground;
            });
            element.RegisterCallback<MouseLeaveEvent>(_ => { element.style.backgroundColor = Color.clear; });
        }

        private void OnOnSortChanged(AssetSortType obj) {
            OnSortChanged?.Invoke(obj);
        }
    }
}