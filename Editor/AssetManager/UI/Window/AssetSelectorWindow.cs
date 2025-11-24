using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.Core.Utility;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetSelectorWindow : BaseWindow {
        private List<AssetMetadata> _allAssets;
        private VisualElement _assetContainer;
        private Ulid _currentAssetId;
        private IAssetRepository _repository;
        private ToolbarSearchField _searchField;

        protected override void OnLostFocus() {
            base.OnLostFocus();
            Close();
        }

        public event Action<Ulid> OnAssetSelected;

        public static void Show(Vector2 screenPosition, IAssetRepository repository, Ulid currentAssetId,
            Action<Ulid> onAssetSelected) {
            var window = OpenSetup<AssetSelectorWindow>(screenPosition);
            window._repository = repository;
            window._currentAssetId = currentAssetId;
            window.OnAssetSelected = onAssetSelected;
            window.position = new Rect(window.position.x, window.position.y, 400, 500);
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

            var titleLabel = new Label("Select Asset") {
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
            _searchField.RegisterValueChangedCallback(evt => FilterAssets(evt.newValue));
            searchRow.Add(_searchField);
            container.Add(searchRow);

            _searchField.schedule.Execute(() => _searchField?.Q<TextField>()?.Focus()).ExecuteLater(50);

            var scrollView = new ScrollView {
                style = {
                    flexGrow = 1
                }
            };

            _assetContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column
                }
            };
            scrollView.Add(_assetContainer);
            container.Add(scrollView);

            root.Add(container);

            RefreshAssetList();

            return root;
        }

        private void RefreshAssetList() {
            if (_repository == null) return;

            _allAssets = _repository.GetAllAssets()
                .Where(a => !a.IsDeleted && a.ID != _currentAssetId)
                .OrderBy(a => a.Name)
                .ToList();

            FilterAssets(string.Empty);
        }

        private void FilterAssets(string searchText) {
            _assetContainer.Clear();

            var filteredAssets = string.IsNullOrEmpty(searchText)
                ? _allAssets
                : _allAssets.Where(a => a.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            foreach (var assetButton in filteredAssets.Select(CreateAssetButton)) _assetContainer.Add(assetButton);

            if (filteredAssets.Count != 0) return;
            var noResultLabel =
                new Label(string.IsNullOrEmpty(searchText) ? "No assets available" : "No matching assets") {
                    style = {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        color = Color.gray,
                        marginTop = 20,
                        width = Length.Percent(100)
                    }
                };
            _assetContainer.Add(noResultLabel);
        }

        private VisualElement CreateAssetButton(AssetMetadata asset) {
            var button = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 32,
                    paddingLeft = 8,
                    paddingRight = 8,
                    marginBottom = 2,
                    backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                OnAssetSelected?.Invoke(asset.ID);
                Close();
                evt.StopPropagation();
            });

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            });
            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            });

            var nameLabel = new Label(asset.Name) {
                style = {
                    flexGrow = 1,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                    unityFontStyleAndWeight = FontStyle.Normal
                }
            };

            var extLabel = new Label(asset.Ext) {
                style = {
                    fontSize = 10,
                    color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)),
                    marginLeft = 8
                }
            };

            button.Add(nameLabel);
            button.Add(extLabel);

            return button;
        }
    }
}