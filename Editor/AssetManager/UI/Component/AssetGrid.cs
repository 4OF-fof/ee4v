using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.UI.Component {
    public class AssetGrid : VisualElement {
        private readonly VisualElement _emptyStateContainer;
        private readonly ListView _listView;
        private readonly List<List<object>> _rows = new();
        private readonly HashSet<object> _selectedItems = new();
        private AssetService _assetService;
        private CancellationTokenSource _cts;
        private Vector2 _dragStartPos;

        private List<object> _flatItems = new();
        private FolderService _folderService;

        private bool _isDragPending;
        private int _itemsPerRow;
        private object _lastSelectedReference;
        private float _lastWidth;
        private bool _pendingSelectionClear;
        private IAssetRepository _repository;
        private bool _rightClickHandledByDown;
        private Action<VisualElement> _showDialog;

        private TextureService _textureService;
        private AssetThumbnailLoader _thumbnailLoader;

        public AssetGrid(int initialItemsPerRow) {
            _itemsPerRow = initialItemsPerRow;
            style.flexGrow = 1;
            style.backgroundColor = ColorPreset.DefaultBackground;

            _listView = new ListView {
                style = { flexGrow = 1 },
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows,
                selectionType = SelectionType.None,
                fixedItemHeight = 220
            };

            _listView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Add(_listView);

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

            var emptyContent = new VisualElement {
                style = {
                    alignItems = Align.Center,
                    paddingTop = 40,
                    paddingBottom = 40
                }
            };

            var iconImage = new Image {
                image = (Texture2D)EditorGUIUtility.IconContent("ModelImporter Icon").image,
                style = {
                    width = 64,
                    height = 64,
                    marginBottom = 16,
                    opacity = 0.3f
                }
            };

            var messageLabel = new Label(I18N.Get("UI.AssetManager.AssetGrid.EmptyTitle")) {
                style = {
                    fontSize = 16,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ColorPreset.TransparentWhite50,
                    marginBottom = 8
                }
            };

            var hintLabel = new Label(I18N.Get("UI.AssetManager.AssetGrid.EmptyHint")) {
                style = {
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ColorPreset.TransparentWhite30
                }
            };

            emptyContent.Add(iconImage);
            emptyContent.Add(messageLabel);
            emptyContent.Add(hintLabel);
            _emptyStateContainer.Add(emptyContent);
            Add(_emptyStateContainer);

            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            RegisterCallback<PointerUpEvent>(OnPointerUpAnywhere, TrickleDown.TrickleDown);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeaveAnywhere, TrickleDown.TrickleDown);
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target != this && evt.target != _listView) return;
                ClearSelection();
            }, TrickleDown.TrickleDown);
        }

        public event Action<List<object>> OnSelectionChange;
        public event Action<BaseFolder> OnFolderDoubleClicked;
        public event Action<AssetMetadata> OnAssetDoubleClicked;
        public event Action<List<Ulid>, List<Ulid>, Ulid> OnItemsDroppedToFolder;

        public void Initialize(TextureService textureService, IAssetRepository repository, AssetService assetService,
            FolderService folderService, Action<VisualElement> showDialog) {
            _textureService = textureService;
            _repository = repository;
            _assetService = assetService;
            _folderService = folderService;
            _showDialog = showDialog;
            _thumbnailLoader = new AssetThumbnailLoader(_textureService);
        }

        public void SetItems(List<object> items) {
            _flatItems = items ?? new List<object>();

            _selectedItems.RemoveWhere(i => !_flatItems.Contains(i));
            if (_lastSelectedReference != null && !_flatItems.Contains(_lastSelectedReference))
                _lastSelectedReference = null;

            RebuildRows();
            UpdateEmptyState();
            Refresh();
        }

        private void UpdateEmptyState() {
            if (_flatItems.Count == 0) {
                _listView.style.display = DisplayStyle.None;
                _emptyStateContainer.style.display = DisplayStyle.Flex;
            }
            else {
                _listView.style.display = DisplayStyle.Flex;
                _emptyStateContainer.style.display = DisplayStyle.None;
            }
        }

        public void SetItemsPerRow(int count) {
            if (_itemsPerRow == count) return;
            _itemsPerRow = count;
            UpdateItemHeight();
            RebuildRows();
            Refresh();
        }

        private void Refresh() {
            _listView.RefreshItems();
        }

        public void ClearSelection() {
            _selectedItems.Clear();
            _lastSelectedReference = null;
            _listView.RefreshItems();
            OnSelectionChange?.Invoke(new List<object>());
        }

        public void UpdateItem(object item) {
            if (item == null || _flatItems == null) return;

            var idx = item switch {
                AssetMetadata asset => _flatItems.FindIndex(o => o is AssetMetadata a && a.ID == asset.ID),
                BaseFolder folder   => _flatItems.FindIndex(o => o is BaseFolder f && f.ID == folder.ID),
                _                   => -1
            };
            if (idx == -1) return;

            _flatItems[idx] = item;

            foreach (var row in _rows)
                for (var c = 0; c < row.Count; c++)
                    switch (item) {
                        case AssetMetadata am when row[c] is AssetMetadata am2 && am2.ID == am.ID:
                        case BaseFolder bf when row[c] is BaseFolder bf2 && bf2.ID == bf.ID:
                            row[c] = item;
                            break;
                    }

            try {
                var rowIndex = item switch {
                    AssetMetadata a => _rows.FindIndex(r => r.Any(o => o is AssetMetadata am && am.ID == a.ID)),
                    BaseFolder f    => _rows.FindIndex(r => r.Any(o => o is BaseFolder bf && bf.ID == f.ID)),
                    _               => -1
                };

                if (rowIndex >= 0)
                    try {
                        _listView.RefreshItem(rowIndex);
                    }
                    catch {
                        _listView.RefreshItems();
                    }
                else
                    _listView.RefreshItems();
            }
            catch {
                RebuildRows();
                _listView.RefreshItems();
            }
        }

        private void RebuildRows() {
            _rows.Clear();
            var currentRow = new List<object>();

            foreach (var item in _flatItems) {
                currentRow.Add(item);
                if (currentRow.Count < _itemsPerRow) continue;
                _rows.Add(currentRow);
                currentRow = new List<object>();
            }

            if (currentRow.Count > 0) _rows.Add(currentRow);
        }

        private void OnDetach(DetachFromPanelEvent evt) {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) {
            var newWidth = evt.newRect.width;
            if (float.IsNaN(newWidth) || newWidth == 0 || Math.Abs(newWidth - _lastWidth) < 1) return;
            _lastWidth = newWidth;
            UpdateItemHeight();
            _listView.Rebuild();
        }

        private void UpdateItemHeight() {
            var containerWidth = _listView.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || containerWidth == 0) return;
            containerWidth -= 20;
            var itemWidth = containerWidth / _itemsPerRow;
            var itemHeight = itemWidth + 50;
            _listView.fixedItemHeight = itemHeight;
        }

        private VisualElement MakeRow() {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.NoWrap,
                    flexGrow = 1,
                    backgroundColor = ColorPreset.DefaultBackground
                }
            };

            for (var i = 0; i < _itemsPerRow; i++) {
                var card = new AssetCard { style = { flexShrink = 0 } };
                card.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
                card.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
                card.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
                row.Add(card);
            }

            return row;
        }

        private void BindRow(VisualElement element, int index) {
            if (index < 0 || index >= _rows.Count) return;
            var rowData = _rows[index];

            var containerWidth = _listView.resolvedStyle.width;
            if (float.IsNaN(containerWidth) || !(containerWidth > 0)) return;
            containerWidth -= 20;
            var itemWidth = containerWidth / _itemsPerRow;

            var elementChildCount = element.childCount;
            if (elementChildCount < _itemsPerRow)
                for (var i = elementChildCount; i < _itemsPerRow; i++) {
                    var card = new AssetCard { style = { flexShrink = 0 } };
                    card.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
                    card.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
                    element.Add(card);
                }

            for (var i = 0; i < element.childCount; i++) {
                var card = element[i] as AssetCard;
                if (card == null) continue;

                card.UnregisterCallback<PointerUpEvent>(OnCardPointerUp);
                card.RegisterCallback<PointerUpEvent>(OnCardPointerUp);

                card.OnDropped -= OnCardDropped;
                card.DisableDropZone();

                if (i < rowData.Count) {
                    card.style.display = DisplayStyle.Flex;
                    card.style.width = itemWidth;

                    var item = rowData[i];
                    card.SetSelected(_selectedItems.Contains(item));

                    switch (item) {
                        case BaseFolder folder:
                            card.SetData(folder.Name);

                            var isSameFolder = card.userData is BaseFolder oldFolder && oldFolder.ID == folder.ID;
                            card.userData = folder;

                            var hasSubFolders = folder is Folder f && (f.Children?.Count ?? 0) > 0;
                            var hasAssets = _repository != null && _repository.GetAllAssets()
                                .Any(a => a.Folder == folder.ID && !a.IsDeleted);
                            var isEmpty = !hasSubFolders && !hasAssets;

                            if (!isSameFolder) card.SetThumbnail(null, true, isEmpty);

                            _thumbnailLoader?.LoadThumbnailAsync(card, folder.ID, true,
                                _cts?.Token ?? CancellationToken.None);

                            card.EnableDropZone();
                            card.OnDropped += OnCardDropped;
                            break;
                        case AssetMetadata asset:
                            card.SetData(asset.Name);

                            var isSameAsset = card.userData is AssetMetadata oldAsset && oldAsset.ID == asset.ID;
                            card.userData = asset;

                            if (!isSameAsset) card.SetThumbnail(null);

                            _thumbnailLoader?.LoadThumbnailAsync(card, asset.ID, false,
                                _cts?.Token ?? CancellationToken.None);
                            break;
                    }
                }
                else {
                    card.style.display = DisplayStyle.None;
                }
            }
        }

        private void OnCardPointerDown(PointerDownEvent evt) {
            if (evt.currentTarget is not AssetCard card) return;
            var targetItem = card.userData;
            if (targetItem == null) return;

            if (evt.button == 1) {
                if (!_selectedItems.Contains(targetItem)) {
                    _selectedItems.Clear();
                    _selectedItems.Add(targetItem);
                    _lastSelectedReference = targetItem;
                }

                _listView.RefreshItems();
                OnSelectionChange?.Invoke(_selectedItems.ToList());
                _rightClickHandledByDown = true;
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0) return;

            _isDragPending = true;
            _dragStartPos = evt.position;
            _pendingSelectionClear = false;

            var currentParent = parent;
            while (currentParent != null) {
                if (currentParent is AssetView assetView) {
                    assetView.Focus();
                    break;
                }

                currentParent = currentParent.parent;
            }

            if (evt.clickCount == 2) {
                _isDragPending = false;
                switch (targetItem) {
                    case BaseFolder folder:
                        OnFolderDoubleClicked?.Invoke(folder);
                        break;
                    case AssetMetadata asset:
                        OnAssetDoubleClicked?.Invoke(asset);
                        break;
                }

                evt.StopPropagation();
                return;
            }

            if (evt.ctrlKey || evt.commandKey) {
                if (!_selectedItems.Add(targetItem))
                    _selectedItems.Remove(targetItem);
                else
                    _lastSelectedReference = targetItem;
            }
            else if (evt.shiftKey && _lastSelectedReference != null && _flatItems.Contains(_lastSelectedReference)) {
                var startIndex = _flatItems.IndexOf(_lastSelectedReference);
                var endIndex = _flatItems.IndexOf(targetItem);
                if (startIndex != -1 && endIndex != -1) {
                    var min = Mathf.Min(startIndex, endIndex);
                    var max = Mathf.Max(startIndex, endIndex);
                    _selectedItems.Clear();
                    for (var i = min; i <= max; i++) _selectedItems.Add(_flatItems[i]);
                }
                else {
                    _selectedItems.Clear();
                    _selectedItems.Add(targetItem);
                    _lastSelectedReference = targetItem;
                }
            }
            else {
                if (_selectedItems.Contains(targetItem)) {
                    _pendingSelectionClear = true;
                }
                else {
                    _selectedItems.Clear();
                    _selectedItems.Add(targetItem);
                    _lastSelectedReference = targetItem;
                }
            }

            _listView.RefreshItems();
            OnSelectionChange?.Invoke(_selectedItems.ToList());

            evt.StopPropagation();
        }

        private void OnCardPointerMove(PointerMoveEvent evt) {
            if (!_isDragPending) return;
            if (evt.currentTarget is not AssetCard card) return;

            if (Vector2.Distance(_dragStartPos, evt.position) < 5f) return;

            _isDragPending = false;
            _pendingSelectionClear = false;

            StartDrag(card.userData);
        }

        private void StartDrag(object targetItem) {
            if (targetItem is not AssetMetadata && targetItem is not BaseFolder) return;

            var selectedAssets = _selectedItems.OfType<AssetMetadata>().ToList();
            var selectedFolders = _selectedItems.OfType<BaseFolder>().ToList();
            if (selectedAssets.Count == 0 && selectedFolders.Count == 0) return;

            DragAndDrop.PrepareStartDrag();

            if (selectedAssets.Count > 0) {
                var assetIds = selectedAssets.Select(a => a.ID.ToString()).ToArray();
                DragAndDrop.SetGenericData("AssetManagerAssets", assetIds);
            }

            if (selectedFolders.Count > 0) {
                var folderIds = selectedFolders.Select(f => f.ID.ToString()).ToArray();
                DragAndDrop.SetGenericData("AssetManagerFolders", folderIds);
            }

            DragAndDrop.objectReferences = Array.Empty<Object>();

            var dragLabel = selectedAssets.Count > 0 && selectedFolders.Count > 0
                ? I18N.Get("UI.AssetManager.AssetGrid.DraggingItems")
                : selectedFolders.Count > 0
                    ? I18N.Get("UI.AssetManager.AssetGrid.DraggingFolders")
                    : I18N.Get("UI.AssetManager.AssetGrid.DraggingAssets");
            DragAndDrop.StartDrag(dragLabel);
        }

        private void OnCardPointerUp(PointerUpEvent evt) {
            if (evt.currentTarget is not AssetCard card) return;
            var targetItem = card.userData;
            if (targetItem == null) return;

            if (evt.button == 0) {
                if (_pendingSelectionClear) {
                    _selectedItems.Clear();
                    _selectedItems.Add(targetItem);
                    _lastSelectedReference = targetItem;
                    _listView.RefreshItems();
                    OnSelectionChange?.Invoke(_selectedItems.ToList());
                }

                _pendingSelectionClear = false;
                _isDragPending = false;
                return;
            }

            if (evt.button != 1) return;

            if (_rightClickHandledByDown) {
                _rightClickHandledByDown = false;
            }
            else {
                if (_selectedItems.Contains(targetItem)) {
                    _selectedItems.Remove(targetItem);
                    if (_lastSelectedReference == targetItem) _lastSelectedReference = null;
                }
                else {
                    _selectedItems.Clear();
                    _selectedItems.Add(targetItem);
                    _lastSelectedReference = targetItem;
                }

                _listView.RefreshItems();
                OnSelectionChange?.Invoke(_selectedItems.ToList());
            }

            var menu = AssetContextMenuFactory.Create(
                _selectedItems.ToList(),
                _repository,
                _assetService,
                _folderService,
                _textureService,
                Refresh,
                _showDialog,
                out var menuHeight
            );

            if (menuHeight <= 20f) return;

            Rect anchorRect;
            try {
                var worldPos = card.LocalToWorld(evt.localPosition);

                if (panel != null) {
                    var rootHeight = panel.visualTree.layout.height;

                    if (worldPos.y + menuHeight > rootHeight) worldPos.y -= menuHeight;
                }

                anchorRect = new Rect(worldPos.x, worldPos.y, 1, 1);
            }
            catch {
                anchorRect = card.worldBound;
            }

            menu.DropDown(anchorRect, card);
            evt.StopPropagation();
        }

        private static void OnPointerUpAnywhere(PointerUpEvent evt) {
        }

        private static void OnPointerLeaveAnywhere(PointerLeaveEvent evt) {
        }

        private void OnCardDropped(Ulid targetFolderId, List<Ulid> assetIds, List<Ulid> folderIds) {
            OnItemsDroppedToFolder?.Invoke(assetIds, folderIds, targetFolderId);
        }
    }
}