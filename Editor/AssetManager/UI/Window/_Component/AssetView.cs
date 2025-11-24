using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetView : VisualElement {
        private readonly AssetGrid _grid;
        private readonly AssetToolbar _toolbar;
        private List<object> _allItems = new();

        private AssetViewController _controller;
        private string _currentSearchText = string.Empty;

        private AssetSortType _currentSortType = AssetSortType.NameAsc;
        private List<object> _filteredItems = new();

        public AssetView() {
            style.flexGrow = 1;
            style.backgroundColor = ColorPreset.DefaultBackground;
            focusable = true;

            _toolbar = new AssetToolbar(5);
            Add(_toolbar);

            _grid = new AssetGrid(5);
            Add(_grid);

            _toolbar.OnBack += () =>
            {
                if (_controller is { CanGoBack: true }) _controller.GoBack();
            };
            _toolbar.OnForward += () =>
            {
                if (_controller is { CanGoForward: true }) _controller.GoForward();
            };
            _toolbar.OnBreadcrumbClicked += id => _controller?.SetFolder(id);
            _toolbar.OnItemSizeChanged += size => _grid.SetItemsPerRow(size);
            _toolbar.OnSearchTextChanged += text =>
            {
                _currentSearchText = text;
                ApplyFilterAndSort();
            };
            _toolbar.OnSortChanged += type =>
            {
                _currentSortType = type;
                ApplyFilterAndSort();
            };

            _grid.OnSelectionChange += NotifySelectionChange;
            _grid.OnFolderDoubleClicked += folder => { _controller?.SetFolder(folder.ID); };
            _grid.OnAssetDoubleClicked += OpenAssetDetailWindow;

            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                _grid.ClearSelection();
                evt.StopPropagation();
            });

            RegisterCallback<PointerDownEvent>(_ => Focus(), TrickleDown.TrickleDown);
        }

        public event Action<List<object>> OnSelectionChange;
        public event Action<List<Ulid>, List<Ulid>, Ulid> OnItemsDroppedToFolder;

        public void Initialize(TextureService textureService, IAssetRepository repository = null) {
            _grid.Initialize(textureService, repository);
            _grid.OnItemsDroppedToFolder += (assetIds, folderIds, targetFolderId) =>
            {
                OnItemsDroppedToFolder?.Invoke(assetIds, folderIds, targetFolderId);
            };
        }

        public void ClearSelection() {
            _grid.ClearSelection();
        }

        public void SetController(AssetViewController controller) {
            if (_controller != null) {
                _controller.ItemsChanged -= OnItemsChanged;
                _controller.OnHistoryChanged -= UpdateNavigationState;
                _controller.BreadcrumbsChanged -= UpdateBreadcrumbs;
            }

            _controller = controller;

            if (_controller == null) return;
            _controller.ItemsChanged += OnItemsChanged;
            _controller.OnHistoryChanged += UpdateNavigationState;
            _controller.BreadcrumbsChanged += UpdateBreadcrumbs;

            _controller.Refresh();
            UpdateNavigationState();
        }

        private void NotifySelectionChange(List<object> selectedItems) {
            OnSelectionChange?.Invoke(selectedItems);

            if (selectedItems.Count == 1) {
                var item = selectedItems.First();
                switch (item) {
                    case AssetMetadata asset:
                        _controller?.SelectAsset(asset);
                        break;
                    case BaseFolder f:
                        _controller?.PreviewFolder(f);
                        break;
                }
            }
            else {
                _controller?.SelectAsset(null);
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

        private void OnItemsChanged(List<object> items) {
            _allItems = items ?? new List<object>();
            ApplyFilterAndSort();
        }

        public void ShowBoothItemFolders(List<BoothItemFolder> folders) {
            _allItems = new List<object>(folders ?? new List<BoothItemFolder>());
            _grid.ClearSelection();
            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort() {
            var searchText = _currentSearchText;
            IEnumerable<object> filtered;

            if (string.IsNullOrWhiteSpace(searchText))
                filtered = _allItems;
            else
                filtered = _allItems.Where(item =>
                {
                    var targetName = "";
                    var desc = "";
                    switch (item) {
                        case AssetMetadata asset:
                            targetName = asset.Name;
                            desc = asset.Description;
                            break;
                        case BaseFolder folder:
                            targetName = folder.Name;
                            desc = folder.Description;
                            break;
                    }

                    return (targetName != null &&
                            targetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (desc != null && desc.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
                });

            _filteredItems = filtered.ToList();
            ApplySort();
            _grid.SetItems(_filteredItems);
        }

        private void ApplySort() {
            if (_filteredItems == null || _filteredItems.Count == 0) return;

            _filteredItems.Sort((a, b) =>
            {
                var nameA = "";
                var nameB = "";
                long dateA = 0;
                long dateB = 0;
                long sizeA = 0;
                long sizeB = 0;
                var extA = "";
                var extB = "";
                var idA = Ulid.Empty;
                var idB = Ulid.Empty;

                switch (a) {
                    case AssetMetadata asset:
                        nameA = asset.Name ?? "";
                        dateA = asset.ModificationTime;
                        sizeA = asset.Size;
                        extA = asset.Ext ?? "";
                        idA = asset.ID;
                        break;
                    case BaseFolder folder:
                        nameA = folder.Name ?? "";
                        dateA = folder.ModificationTime;
                        idA = folder.ID;
                        break;
                }

                switch (b) {
                    case AssetMetadata asset:
                        nameB = asset.Name ?? "";
                        dateB = asset.ModificationTime;
                        sizeB = asset.Size;
                        extB = asset.Ext ?? "";
                        idB = asset.ID;
                        break;
                    case BaseFolder folder:
                        nameB = folder.Name ?? "";
                        dateB = folder.ModificationTime;
                        idB = folder.ID;
                        break;
                }

                return _currentSortType switch {
                    AssetSortType.DateAddedNewest => idB.CompareTo(idA),
                    AssetSortType.DateAddedOldest => idA.CompareTo(idB),
                    AssetSortType.NameAsc         => string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase),
                    AssetSortType.NameDesc        => string.Compare(nameB, nameA, StringComparison.OrdinalIgnoreCase),
                    AssetSortType.DateNewest      => dateB.CompareTo(dateA),
                    AssetSortType.DateOldest      => dateA.CompareTo(dateB),
                    AssetSortType.SizeSmallest    => sizeA.CompareTo(sizeB),
                    AssetSortType.SizeLargest     => sizeB.CompareTo(sizeA),
                    AssetSortType.ExtAsc          => string.Compare(extA, extB, StringComparison.OrdinalIgnoreCase),
                    AssetSortType.ExtDesc         => string.Compare(extB, extA, StringComparison.OrdinalIgnoreCase),
                    _                             => 0
                };
            });
        }

        private static void OpenAssetDetailWindow(AssetMetadata asset) {
            if (asset == null) return;
            var mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            AssetDetailWindow.Open(mousePos, asset);
        }
    }
}