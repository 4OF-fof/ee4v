using System;
using System.Collections.Generic;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";
        private const string ContentClassName = "ee4v-asset-manager-panel__main-content";
        private const string ExternalFileDropClassName =
            "ee4v-asset-manager-panel--external-file-drop";
        private const string ExternalFileDropOverlayClassName =
            "ee4v-asset-manager-panel__external-file-drop-overlay";
        private readonly MainViewController _controller;
        private readonly AssetItemGrid _itemGrid;
        private readonly ErrorScreen _errorScreen;
        private readonly TagListPage _tagListPage;
        private readonly VisualElement _externalFileDropOverlay;
        private VisualElement _externalFileDropEventSurface;
        private VisualElement _externalFileDropOverlaySurface;
        private string _fileListItemId;
        private string _fileListItemName;
        private AssetItemGridNodeKind _browserNodeKind;
        private string _browserNodeId;
        private string _browserNodeName;
        private string _searchText = string.Empty;
        private string _currentItemContentKey = string.Empty;
        private AssetItemGridList _currentItemList;
        private bool _applyingHistory;
        private ItemCardState[] _selectedAssetItems = System.Array.Empty<ItemCardState>();
        private string[] _selectionIdsPendingRefresh =
            System.Array.Empty<string>();
        private bool _restoreSelectionAfterRefresh;
        private AssetSelectionContentKind _selectionContentKind = AssetSelectionContentKind.AssetItem;
        private string _statusMessage = string.Empty;
        private ErrorScreenKind _statusKind =
            ErrorScreenKind.Loading;
        private string _emptyMessage = string.Empty;
        private string _contentErrorMessage = string.Empty;
        private string _externalErrorMessage = string.Empty;

        public MainView(MainViewController controller)
        {
            _controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
            _itemGrid = new AssetItemGrid();
            ApplyGridSize(_controller.ItemsPerRow);
            _itemGrid.AddToClassList(ContentClassName);
            _errorScreen = new ErrorScreen();
            _errorScreen.style.display = DisplayStyle.None;
            _tagListPage = new TagListPage();
            _tagListPage.style.display = DisplayStyle.None;
            _externalFileDropOverlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _externalFileDropOverlay.AddToClassList(
                ExternalFileDropOverlayClassName);

            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);
            Add(_errorScreen);
            Add(_tagListPage);
            Add(_itemGrid);
            SetExternalFileDropSurface(this);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            _itemGrid.SelectionChanged += OnGridSelectionChanged;
            _itemGrid.ItemDoubleClicked += OnGridItemDoubleClicked;
            _itemGrid.ItemContextClicked +=
                OnGridItemContextClicked;
            _itemGrid.RecommendedMinimumItemsPerRowChanged +=
                _controller.SetMinimumItemsPerRow;
        }

        public AssetItemGridHistory History
        {
            get { return _controller.History; }
        }

        public int GridSize
        {
            get { return _controller.ItemsPerRow; }
        }

        public int MinimumGridSize
        {
            get { return _controller.MinimumItemsPerRow; }
        }

        internal int DisplayedGridSize
        {
            get { return _itemGrid.ItemsPerRow; }
        }

        public int HistoryOverlayMaximumItems
        {
            get { return _controller.HistoryOverlayMaximumItems; }
        }

        public event System.Action<int> HistoryOverlayMaximumItemsChanged;

        public event System.Action<int> GridSizeChanged;

        public event System.Action<int> GridSizeMinimumChanged;

        public event System.Action<System.Collections.Generic.IReadOnlyList<ItemCardState>, AssetSelectionContentKind> SelectionChanged;

        internal void SetExternalError(string message)
        {
            _externalErrorMessage = message ?? string.Empty;
            RefreshMessageState();
        }

        internal void SetLoadingState(string message)
        {
            SetStatus(message);
        }

        public void SetGridSize(int value)
        {
            _controller.SetItemsPerRow(value);
        }

        public void SetSearchText(string value)
        {
            var nextValue = value ?? string.Empty;
            if (string.Equals(_searchText, nextValue, System.StringComparison.Ordinal))
            {
                return;
            }

            _searchText = nextValue;
            _controller.ClearCachedTags();
            ClearCurrentItemList();
            ClearGridSelection();
            RefreshContent();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            HideExternalFileDropOverlay();
            _controller.Activate();
            _controller.NavigationChanged += OnSelectedItemChanged;
            _controller.ContentChanged += OnContentChanged;
            _controller.CollectionPresentationChanged +=
                OnCollectionPresentationChanged;
            _controller.LayoutChanged += OnLayoutChanged;
            _controller.MinimumItemsPerRowChanged +=
                OnMinimumItemsPerRowChanged;
            _controller.HistoryOverlayMaximumItemsChanged += OnHistoryOverlayMaximumItemsChanged;
            _controller.LoadCompleted += OnLoadCompleted;
            _controller.TagListLoadCompleted += OnTagListLoadCompleted;
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            HideExternalFileDropOverlay();
            _controller.NavigationChanged -= OnSelectedItemChanged;
            _controller.ContentChanged -= OnContentChanged;
            _controller.CollectionPresentationChanged -=
                OnCollectionPresentationChanged;
            _controller.LayoutChanged -= OnLayoutChanged;
            _controller.MinimumItemsPerRowChanged -=
                OnMinimumItemsPerRowChanged;
            _controller.HistoryOverlayMaximumItemsChanged -= OnHistoryOverlayMaximumItemsChanged;
            _controller.LoadCompleted -= OnLoadCompleted;
            _controller.TagListLoadCompleted -= OnTagListLoadCompleted;
            _controller.Deactivate();
            _controller.CancelPendingLoad();
        }

        private void OnExternalFileDragUpdated(
            DragUpdatedEvent evt)
        {
            var accepted = _controller
                .CanRegisterDroppedFiles(
                    DragAndDrop.paths) &&
                IsInsideExternalFileDropSurface(
                    evt.mousePosition);
            SetExternalFileDropOverlayVisible(accepted);
            DragAndDrop.visualMode = accepted
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;
            if (accepted)
            {
                evt.StopPropagation();
            }
        }

        private void OnExternalFileDragPerform(
            DragPerformEvent evt)
        {
            if (!_controller.CanRegisterDroppedFiles(
                    DragAndDrop.paths) ||
                !IsInsideExternalFileDropSurface(
                    evt.mousePosition))
            {
                HideExternalFileDropOverlay();
                return;
            }

            try
            {
                var paths =
                    (string[])DragAndDrop.paths.Clone();
                DragAndDrop.AcceptDrag();
                var registered = _controller
                    .RegisterDroppedFiles(
                        paths);
                if (registered > 0)
                {
                    SetExternalError(string.Empty);
                    _controller.SetSelectedNavigationItem(
                        "uncategorized");
                }
            }
            catch (Exception exception)
            {
                SetExternalError(
                    AssetManagerUiErrorMessage.Format(
                        exception));
            }
            finally
            {
                HideExternalFileDropOverlay();
            }

            evt.StopPropagation();
        }

        private void OnExternalFileDragLeave(
            DragLeaveEvent evt)
        {
            if (!IsInsideExternalFileDropSurface(
                    evt.mousePosition))
            {
                HideExternalFileDropOverlay();
            }
        }

        private void OnExternalFileDragExited(
            DragExitedEvent evt)
        {
            HideExternalFileDropOverlay();
        }

        internal void SetExternalFileDropOverlayVisible(
            bool visible)
        {
            _externalFileDropOverlaySurface?.EnableInClassList(
                ExternalFileDropClassName,
                visible);
        }

        internal void SetExternalFileDropSurface(
            VisualElement surface)
        {
            SetExternalFileDropSurface(
                surface,
                surface);
        }

        internal void SetExternalFileDropSurface(
            VisualElement eventSurface,
            VisualElement overlaySurface)
        {
            if (_externalFileDropEventSurface == eventSurface &&
                _externalFileDropOverlaySurface == overlaySurface)
            {
                return;
            }

            HideExternalFileDropOverlay();
            UnregisterExternalFileDropCallbacks(
                _externalFileDropEventSurface);
            _externalFileDropOverlay.RemoveFromHierarchy();
            _externalFileDropEventSurface = eventSurface;
            _externalFileDropOverlaySurface = overlaySurface;
            if (_externalFileDropEventSurface == null ||
                _externalFileDropOverlaySurface == null)
            {
                return;
            }

            _externalFileDropOverlaySurface.Add(
                _externalFileDropOverlay);
            RegisterExternalFileDropCallbacks(
                _externalFileDropEventSurface);
        }

        private void HideExternalFileDropOverlay()
        {
            SetExternalFileDropOverlayVisible(false);
        }

        private bool IsInsideExternalFileDropSurface(
            Vector2 panelPosition)
        {
            return _externalFileDropOverlaySurface != null &&
                   _externalFileDropOverlaySurface
                       .worldBound
                       .Contains(panelPosition);
        }

        private void RegisterExternalFileDropCallbacks(
            VisualElement surface)
        {
            surface.RegisterCallback<DragUpdatedEvent>(
                OnExternalFileDragUpdated,
                TrickleDown.TrickleDown);
            surface.RegisterCallback<DragPerformEvent>(
                OnExternalFileDragPerform,
                TrickleDown.TrickleDown);
            surface.RegisterCallback<DragLeaveEvent>(
                OnExternalFileDragLeave,
                TrickleDown.TrickleDown);
            surface.RegisterCallback<DragExitedEvent>(
                OnExternalFileDragExited,
                TrickleDown.TrickleDown);
        }

        private void UnregisterExternalFileDropCallbacks(
            VisualElement surface)
        {
            if (surface == null)
            {
                return;
            }

            surface.UnregisterCallback<DragUpdatedEvent>(
                OnExternalFileDragUpdated,
                TrickleDown.TrickleDown);
            surface.UnregisterCallback<DragPerformEvent>(
                OnExternalFileDragPerform,
                TrickleDown.TrickleDown);
            surface.UnregisterCallback<DragLeaveEvent>(
                OnExternalFileDragLeave,
                TrickleDown.TrickleDown);
            surface.UnregisterCallback<DragExitedEvent>(
                OnExternalFileDragExited,
                TrickleDown.TrickleDown);
        }

        private void OnSelectedItemChanged(string itemId)
        {
            if (_applyingHistory)
            {
                return;
            }

            ClearFileListMode();
            ClearCurrentItemList();
            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnContentChanged()
        {
            PreserveGridSelectionForRefresh();
            ClearCurrentItemList();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnCollectionPresentationChanged()
        {
            if (IsFileListMode || IsTagListMode)
            {
                return;
            }

            var contentKey = CreateCurrentContentKey();
            if (_currentItemList == null ||
                !string.Equals(
                    _currentItemContentKey,
                    contentKey,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            ClearGridSelection();
            ApplyItemList(contentKey, _currentItemList);
        }

        private void OnLayoutChanged()
        {
            ApplyGridSize(_controller.ItemsPerRow);
        }

        private void OnMinimumItemsPerRowChanged(int value)
        {
            GridSizeMinimumChanged?.Invoke(value);
        }

        private void ApplyGridSize(int settingValue)
        {
            _itemGrid.SetItemsPerRow(settingValue);
            GridSizeChanged?.Invoke(settingValue);
        }

        private void OnHistoryOverlayMaximumItemsChanged(int value)
        {
            HistoryOverlayMaximumItemsChanged?.Invoke(value);
        }

        private void OnGridSelectionChanged(System.Collections.Generic.IReadOnlyList<ItemCardState> items)
        {
            SetSelection(CreateSelectionItems(items), ResolveSelectionContentKind(items));
        }

        private void OnGridItemDoubleClicked(ItemCardState item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
            {
                return;
            }

            AssetItemGridNodeKind kind;
            string rawId;
            if (!AssetItemGridNodeKey.TryDecode(item.ItemId, out kind, out rawId))
            {
                _fileListItemId = item.ItemId;
                _fileListItemName = item.ItemName;
                _browserNodeKind = AssetItemGridNodeKind.Item;
                _browserNodeId = string.Empty;
                _browserNodeName = string.Empty;
            }
            else if (kind == AssetItemGridNodeKind.Collection)
            {
                ClearGridSelection();
                _controller.SetSelectedNavigationItem(
                    AssetManagerCollectionViewId.Encode(rawId));
                return;
            }
            else if (kind == AssetItemGridNodeKind.File)
            {
                return;
            }
            else
            {
                _browserNodeKind = kind;
                _browserNodeId = rawId;
                _browserNodeName = item.ItemName;
            }

            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnGridItemContextClicked(
            VisualElement target,
            ItemCardState item,
            Vector2 panelPosition)
        {
            AssetSelectionContentKind targetKind;
            string targetId;
            if (!TryResolveHighlightTarget(
                    item,
                    out targetKind,
                    out targetId))
            {
                return;
            }

            var projectActions =
                AssetManagerUiDependencies.ProjectActions;
            var isFile =
                targetKind == AssetSelectionContentKind.AssetFile;
            var canHighlight = isFile
                ? projectActions.CanHighlightFile(targetId)
                : projectActions.CanHighlightItem(targetId);
            var isHighlighted = isFile
                ? projectActions.IsFileHighlighted(targetId)
                : projectActions.IsItemHighlighted(targetId);
            Action highlight = isFile
                ? (Action)(() =>
                    projectActions.HighlightFile(targetId))
                : () => projectActions.HighlightItem(targetId);
            Action import;
            var importItemId = isFile
                ? _fileListItemId
                : targetId;
            var importFileId = isFile
                ? targetId
                : null;
            var canImport =
                _controller.TryCreateImportAction(
                    importItemId,
                    importFileId,
                    out import);
            var menuItem = isHighlighted
                ? new ContextMenuItemState(
                    "clear-imported-asset-highlights",
                    I18N.Get(
                        "assetManager.mainView.context.clearHighlight"),
                    projectActions.ClearHighlights)
                : new ContextMenuItemState(
                    "highlight-imported-assets",
                    I18N.Get(
                        "assetManager.mainView.context.highlight"),
                    highlight,
                    canHighlight);
            IReadOnlyList<AssetItemContextAction>
                extensionActions =
                    Array.Empty<AssetItemContextAction>();
            if (!isFile)
            {
                var screenPosition =
                    ContextMenuWindow.GetScreenPosition(
                        target,
                        panelPosition);
                extensionActions =
                    AssetManagerUiDependencies
                        .ItemContextActions
                        .CreateActions(
                            new AssetItemContextActionRequest(
                                targetId,
                                screenPosition.x,
                                screenPosition.y));
            }

            var menuItems = CreateContextMenuItems(
                extensionActions,
                import,
                canImport,
                menuItem);
            var menu = new ContextMenuState(
                menuItems);
            ContextMenuWindow.Show(
                target,
                panelPosition,
                menu);
        }

        internal static IReadOnlyList<ContextMenuItemState>
            CreateContextMenuItems(
                IReadOnlyList<AssetItemContextAction>
                    extensionActions,
                Action import,
                bool canImport,
                ContextMenuItemState highlight)
        {
            var menuItems =
                new List<ContextMenuItemState>();
            menuItems.Add(
                new ContextMenuItemState(
                    "import",
                    I18N.Get(
                        "assetManager.mainView.context.import"),
                    import,
                    canImport));

            var extensions = extensionActions ??
                Array.Empty<AssetItemContextAction>();
            for (var i = 0; i < extensions.Count; i++)
            {
                var action = extensions[i];
                if (action == null)
                {
                    continue;
                }

                menuItems.Add(
                    new ContextMenuItemState(
                        action.Id,
                        action.Label,
                        action.Execute,
                        action.Enabled));
            }

            menuItems.Add(
                ContextMenuItemState.Separator());
            if (highlight != null)
            {
                menuItems.Add(highlight);
            }

            return menuItems;
        }

        internal static bool TryResolveHighlightTarget(
            ItemCardState item,
            out AssetSelectionContentKind kind,
            out string targetId)
        {
            kind = AssetSelectionContentKind.AssetItem;
            targetId = string.Empty;
            if (item == null ||
                string.IsNullOrWhiteSpace(item.ItemId))
            {
                return false;
            }

            AssetItemGridNodeKind nodeKind;
            string nodeId;
            if (!AssetItemGridNodeKey.TryDecode(
                    item.ItemId,
                    out nodeKind,
                    out nodeId))
            {
                targetId = item.ItemId;
                return true;
            }

            if (nodeKind != AssetItemGridNodeKind.File)
            {
                return false;
            }

            kind = AssetSelectionContentKind.AssetFile;
            targetId = nodeId;
            return true;
        }

        public void GoBack()
        {
            GoBack(1);
        }

        public void GoBack(int steps)
        {
            AssetItemGridHistoryEntry entry;
            if (_controller.History.TryGoBack(steps, out entry))
            {
                ApplyHistoryEntry(entry);
            }
        }

        public void GoForward()
        {
            GoForward(1);
        }

        public void GoForward(int steps)
        {
            AssetItemGridHistoryEntry entry;
            if (_controller.History.TryGoForward(steps, out entry))
            {
                ApplyHistoryEntry(entry);
            }
        }

        public void GoToBreadcrumb(int index)
        {
            var current = _controller.History.State.Current;
            if (current == null
                || index < 0
                || index >= current.Breadcrumbs.Count)
            {
                return;
            }

            AssetItemGridHistoryEntry entry;
            if (index < current.ViewPath.Count)
            {
                var targetView = current.ViewPath[index];
                entry = new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.View,
                    targetView.Id,
                    targetView.Label,
                    viewPath:
                    CreateViewPathPrefix(
                        current.ViewPath,
                        index));
            }
            else if (
                index == current.ViewPath.Count &&
                current.Kind ==
                AssetItemGridHistoryEntryKind.FileList)
            {
                if (string.IsNullOrWhiteSpace(current.ItemId))
                {
                    return;
                }

                entry = new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.FileList,
                    current.ViewId,
                    current.ViewLabel,
                    current.ItemId,
                    current.ItemName,
                    viewPath: current.ViewPath);
            }
            else
            {
                return;
            }

            if (current.IsSameLocation(entry))
            {
                return;
            }

            _controller.History.SetCurrent(entry);
            ApplyHistoryEntry(entry);
        }

        private void RefreshContent()
        {
            SetEmptyState(string.Empty);
            SetContentError(string.Empty);
            if (IsTagListMode)
            {
                ClearCurrentItemList();
                ApplyContentVisibility();
                var tagCacheKey = CreateCurrentContentKey();
                System.Collections.Generic.IReadOnlyList<AssetTag> cachedTags;
                if (_controller.TryGetCachedTags(tagCacheKey, out cachedTags))
                {
                    _controller.CancelPendingLoad();
                    ApplyTagList(tagCacheKey, cachedTags);
                    return;
                }

                SetStatus(I18N.Get("assetManager.mainView.tags.loading"));
                _tagListPage.SetLoading();
                _controller.StartTagListLoad(
                    tagCacheKey,
                    cancellationToken => _controller.LoadTags(_searchText, cancellationToken));
                return;
            }

            ApplyContentVisibility();
            var contentKey = CreateCurrentContentKey();
            if (IsFileListMode)
            {
                AssetItemGridList cachedChildren;
                if (_controller.TryGetCachedChildren(
                        contentKey,
                        out cachedChildren))
                {
                    _controller.CancelPendingLoad();
                    ApplyItemList(contentKey, cachedChildren);
                    return;
                }
            }
            else
            {
                var request =
                    _controller.CreateRequest(
                        _controller
                            .SelectedNavigationItemId,
                        _searchText);
                AssetItemGridList snapshotItems;
                bool requiresThumbnailLoad;
                if (_controller.TryLoadItemsImmediately(
                        request,
                        out snapshotItems,
                        out requiresThumbnailLoad))
                {
                    _controller.CancelPendingLoad();
                    ApplyItemList(
                        contentKey,
                        snapshotItems);
                    if (requiresThumbnailLoad)
                    {
                        _controller.StartLoad(
                            contentKey,
                            LoadCurrentGridItems,
                            silent: true);
                    }

                    return;
                }
            }

            ClearCurrentItemList();
            SetStatus(IsFileListMode
                ? I18N.Get("assetManager.mainView.loadingChildren")
                : I18N.Get("assetManager.mainView.loading"));
            _itemGrid.SetLoading();
            _controller.StartLoad(
                contentKey,
                LoadCurrentGridItems);
        }

        private void OnLoadCompleted(MainViewLoadResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.Error != null)
            {
                if (result.Silent)
                {
                    return;
                }

                SetStatus(string.Empty);
                SetContentError(
                    AssetManagerUiErrorMessage.Format(
                        result.Error));
                return;
            }

            if (result.Canceled)
            {
                if (result.Silent)
                {
                    return;
                }

                SetContentError(string.Empty);
                SetStatus(
                    I18N.Get("assetManager.mainView.loadCanceled"),
                    ErrorScreenKind.Info);
                return;
            }

            SetContentError(string.Empty);
            ApplyItemList(result.ContentKey, result.Items);
        }

        private void OnTagListLoadCompleted(TagListLoadResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.Error != null)
            {
                SetStatus(string.Empty);
                SetContentError(
                    AssetManagerUiErrorMessage.Format(
                        result.Error));
                return;
            }

            if (result.Canceled)
            {
                SetContentError(string.Empty);
                SetStatus(
                    I18N.Get("assetManager.mainView.loadCanceled"),
                    ErrorScreenKind.Info);
                return;
            }

            SetContentError(string.Empty);
            ApplyTagList(result.CacheKey, result.Tags);
        }

        private void ApplyItemList(
            string contentKey,
            AssetItemGridList itemList)
        {
            _currentItemContentKey =
                contentKey ?? string.Empty;
            _currentItemList =
                itemList ?? new AssetItemGridList(null);
            if (IsFileListMode)
            {
                _controller.StoreCachedChildren(
                    _currentItemContentKey,
                    _currentItemList);
            }

            var displayItems = IsFileListMode
                ? _currentItemList
                : _controller.CreateDisplayItems(
                    _controller.CreateRequest(
                        _controller.SelectedNavigationItemId,
                        _searchText),
                    _currentItemList);
            string statusText;
            _itemGrid.SetAssetItems(
                displayItems,
                out statusText);
            RestoreGridSelectionAfterRefresh();
            SetStatus(string.Empty);
            SetEmptyState(ResolveStatusText(statusText));
        }

        private void ApplyTagList(
            string cacheKey,
            System.Collections.Generic.IReadOnlyList<AssetTag> tags)
        {
            _controller.StoreCachedTags(cacheKey, tags);
            _tagListPage.SetTags(tags);
            SetStatus(string.Empty);
            SetEmptyState(
                _tagListPage.IsEmpty
                    ? I18N.Get("assetManager.mainView.tags.empty")
                    : string.Empty);
        }

        private void SetStatus(
            string message,
            ErrorScreenKind kind = ErrorScreenKind.Loading)
        {
            _statusMessage = message ?? string.Empty;
            _statusKind = kind;
            RefreshMessageState();
        }

        private void SetContentError(string message)
        {
            _contentErrorMessage = message ?? string.Empty;
            RefreshMessageState();
        }

        internal void SetEmptyState(string message)
        {
            _emptyMessage = message ?? string.Empty;
            RefreshMessageState();
        }

        private void RefreshMessageState()
        {
            var hasError =
                !string.IsNullOrWhiteSpace(_externalErrorMessage) ||
                !string.IsNullOrWhiteSpace(_contentErrorMessage);
            var message = !string.IsNullOrWhiteSpace(_externalErrorMessage)
                ? _externalErrorMessage
                : !string.IsNullOrWhiteSpace(_contentErrorMessage)
                    ? _contentErrorMessage
                    : !string.IsNullOrWhiteSpace(_statusMessage)
                        ? _statusMessage
                        : _emptyMessage;
            var hasMessage = !string.IsNullOrWhiteSpace(message);
            if (hasMessage)
            {
                _errorScreen.SetState(new ErrorScreenState(
                    message,
                    hasError
                        ? ErrorScreenKind.Error
                        : !string.IsNullOrWhiteSpace(_statusMessage)
                            ? _statusKind
                            : ErrorScreenKind.Info));
            }

            _errorScreen.style.display = hasMessage
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            ApplyContentVisibility();
        }

        private void ApplyContentVisibility()
        {
            var hasMessage = HasMessage;
            _tagListPage.style.display =
                !hasMessage && IsTagListMode
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _itemGrid.style.display =
                !hasMessage && !IsTagListMode
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private bool HasMessage
        {
            get
            {
                return
                    !string.IsNullOrWhiteSpace(_externalErrorMessage) ||
                    !string.IsNullOrWhiteSpace(_contentErrorMessage) ||
                    !string.IsNullOrWhiteSpace(_statusMessage) ||
                    !string.IsNullOrWhiteSpace(_emptyMessage);
            }
        }

        private void ClearGridSelection()
        {
            _selectionIdsPendingRefresh =
                System.Array.Empty<string>();
            _restoreSelectionAfterRefresh = false;
            _itemGrid.ClearSelection(notify: false);
            SetSelection(null, AssetSelectionContentKind.AssetItem);
        }

        private void PreserveGridSelectionForRefresh()
        {
            var selectedItems = _itemGrid.SelectedItems;
            if (selectedItems.Count == 0 &&
                _restoreSelectionAfterRefresh)
            {
                return;
            }

            _selectionIdsPendingRefresh =
                new string[selectedItems.Count];
            for (var i = 0; i < selectedItems.Count; i++)
            {
                _selectionIdsPendingRefresh[i] =
                    selectedItems[i].ItemId;
            }

            _restoreSelectionAfterRefresh =
                _selectionIdsPendingRefresh.Length > 0;
            _itemGrid.ClearSelection(notify: false);
        }

        private void RestoreGridSelectionAfterRefresh()
        {
            if (!_restoreSelectionAfterRefresh)
            {
                return;
            }

            var itemIds = _selectionIdsPendingRefresh;
            _selectionIdsPendingRefresh =
                System.Array.Empty<string>();
            _restoreSelectionAfterRefresh = false;
            _itemGrid.SetSelectedItemIds(itemIds);
        }

        private bool IsFileListMode
        {
            get { return !string.IsNullOrWhiteSpace(_fileListItemId); }
        }

        private bool IsTagListMode
        {
            get
            {
                return !IsFileListMode &&
                       string.Equals(
                           _controller.SelectedNavigationItemId,
                           "tags",
                           System.StringComparison.Ordinal);
            }
        }

        private AssetItemGridList LoadCurrentGridItems(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsFileListMode)
            {
                if (_browserNodeKind == AssetItemGridNodeKind.Item)
                {
                    return _controller.LoadItemChildren(_fileListItemId);
                }

                return _controller.LoadGroupChildren(_fileListItemId, _browserNodeKind, _browserNodeId);
            }

            return _controller.LoadItems(
                _controller.CreateRequest(
                    _controller.SelectedNavigationItemId,
                    _searchText),
                cancellationToken);
        }

        private string CreateCurrentContentKey()
        {
            if (IsFileListMode)
            {
                return "children|" + _fileListItemId + "|" + _browserNodeKind + "|" + _browserNodeId;
            }

            return _controller.CreateContentKey(
                _controller.CreateRequest(
                    _controller.SelectedNavigationItemId,
                    _searchText));
        }

        private void ClearCurrentItemList()
        {
            _currentItemContentKey = string.Empty;
            _currentItemList = null;
        }

        private string ResolveStatusText(string statusText)
        {
            if (!IsFileListMode || !string.IsNullOrWhiteSpace(statusText))
            {
                return statusText;
            }

            return string.Empty;
        }

        private void ClearFileListMode()
        {
            _fileListItemId = string.Empty;
            _fileListItemName = string.Empty;
            _browserNodeKind = AssetItemGridNodeKind.Item;
            _browserNodeId = string.Empty;
            _browserNodeName = string.Empty;
        }

        private void PushCurrentHistory()
        {
            if (_applyingHistory)
            {
                return;
            }

            _controller.History.SetCurrent(CreateCurrentHistoryEntry());
        }

        private AssetItemGridHistoryEntry CreateCurrentHistoryEntry()
        {
            var selectedItem = _controller.SelectedNavigationItem;
            var viewPath = _controller.CreateHistoryViewPath();
            if (IsFileListMode)
            {
                return new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.FileList,
                    selectedItem.Id,
                    selectedItem.Label,
                    _fileListItemId,
                    _fileListItemName,
                    _browserNodeKind,
                    _browserNodeId,
                    _browserNodeName,
                    viewPath: viewPath);
            }

            return new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.View,
                selectedItem.Id,
                selectedItem.Label,
                viewPath: viewPath);
        }

        private static System.Collections.Generic.IReadOnlyList<
                AssetItemGridHistoryView>
            CreateViewPathPrefix(
                System.Collections.Generic.IReadOnlyList<
                    AssetItemGridHistoryView> viewPath,
                int lastIndex)
        {
            var result =
                new AssetItemGridHistoryView[lastIndex + 1];
            for (var i = 0; i <= lastIndex; i++)
            {
                result[i] = viewPath[i];
            }

            return result;
        }

        private void ApplyHistoryEntry(AssetItemGridHistoryEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            try
            {
                _applyingHistory = true;
                _controller.SetSelectedNavigationItem(entry.ViewId);
                if (entry.Kind == AssetItemGridHistoryEntryKind.FileList)
                {
                    _fileListItemId = entry.ItemId;
                    _fileListItemName = entry.ItemName;
                    _browserNodeKind = entry.NodeKind;
                    _browserNodeId = entry.NodeId;
                    _browserNodeName = entry.NodeName;
                }
                else
                {
                    ClearFileListMode();
                }

                ClearGridSelection();
            }
            finally
            {
                _applyingHistory = false;
            }

            RefreshContent();
        }

        private static System.Collections.Generic.IReadOnlyList<ItemCardState> CreateSelectionItems(System.Collections.Generic.IReadOnlyList<ItemCardState> items)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            var result = new System.Collections.Generic.List<ItemCardState>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                AssetItemGridNodeKind kind;
                string rawId;
                if (AssetItemGridNodeKey.TryDecode(
                        item.ItemId,
                        out kind,
                        out rawId))
                {
                    if (kind != AssetItemGridNodeKind.Collection)
                    {
                        result.Add(new ItemCardState(
                            rawId,
                            item.ItemName,
                            item.ImageState,
                            item.IconState,
                            item.ParentItemId,
                            item.StackStates,
                            item.NameIconState));
                    }
                }
                else
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private ItemCardState SelectedAssetItem
        {
            get { return _selectedAssetItems.Length > 0 ? _selectedAssetItems[0] : null; }
        }

        private void SetSelection(
            System.Collections.Generic.IReadOnlyList<ItemCardState> items,
            AssetSelectionContentKind contentKind)
        {
            if (items == null || items.Count == 0)
            {
                _selectedAssetItems = System.Array.Empty<ItemCardState>();
                _selectionContentKind = AssetSelectionContentKind.AssetItem;
            }
            else
            {
                var nextItems = new System.Collections.Generic.List<ItemCardState>(items.Count);
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        nextItems.Add(items[i]);
                    }
                }

                _selectedAssetItems = nextItems.ToArray();
                _selectionContentKind = contentKind;
            }

            SelectionChanged?.Invoke(_selectedAssetItems, _selectionContentKind);
        }

        private static AssetSelectionContentKind ResolveSelectionContentKind(System.Collections.Generic.IReadOnlyList<ItemCardState> items)
        {
            if (items == null || items.Count == 0)
            {
                return AssetSelectionContentKind.AssetItem;
            }

            var hasFile = false;
            var hasGroup = false;
            var groupContentKind = AssetSelectionContentKind.AssetGroup;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                AssetItemGridNodeKind kind;
                string rawId;
                if (!AssetItemGridNodeKey.TryDecode(items[i].ItemId, out kind, out rawId))
                {
                    continue;
                }

                if (kind == AssetItemGridNodeKind.File)
                {
                    hasFile = true;
                }
                else if (
                    kind == AssetItemGridNodeKind.VariantGroup ||
                    kind == AssetItemGridNodeKind.VersionGroup)
                {
                    var nextGroupContentKind =
                        kind == AssetItemGridNodeKind.VariantGroup
                            ? AssetSelectionContentKind.AssetVariantGroup
                            : AssetSelectionContentKind.AssetVersionGroup;
                    if (!hasGroup)
                    {
                        groupContentKind = nextGroupContentKind;
                    }
                    else if (groupContentKind != nextGroupContentKind)
                    {
                        groupContentKind = AssetSelectionContentKind.AssetGroup;
                    }

                    hasGroup = true;
                }
            }

            if (hasFile && !hasGroup)
            {
                return AssetSelectionContentKind.AssetFile;
            }

            return hasGroup
                ? groupContentKind
                : AssetSelectionContentKind.AssetItem;
        }
    }
}
