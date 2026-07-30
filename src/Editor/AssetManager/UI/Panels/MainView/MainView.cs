using System;
using System.Collections.Generic;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";
        private const string ContentClassName = "ee4v-asset-manager-panel__main-content";
        private const string StatusClassName = "ee4v-asset-manager-panel__main-status";
        private const string ErrorStateClassName =
            "ee4v-asset-manager-main-error";
        private const string ErrorIconClassName =
            "ee4v-asset-manager-main-error__icon";
        private const string ErrorMessageClassName =
            "ee4v-asset-manager-main-error__message";
        private readonly MainViewController _controller;
        private readonly AssetItemGrid _itemGrid;
        private readonly UiTextElement _statusLabel;
        private readonly VisualElement _errorState;
        private readonly UiTextElement _errorMessage;
        private readonly FileTreeDetailView _fileDetailView;
        private readonly TagListPage _tagListPage;
        private string _fileListItemId;
        private string _fileListItemName;
        private AssetItemGridNodeKind _browserNodeKind;
        private string _browserNodeId;
        private string _browserNodeName;
        private FileTreeDetailState _fileDetailState;
        private string _searchText = string.Empty;
        private string _currentItemContentKey = string.Empty;
        private AssetItemGridList _currentItemList;
        private bool _applyingHistory;
        private ItemCardState[] _selectedAssetItems = System.Array.Empty<ItemCardState>();
        private AssetSelectionContentKind _selectionContentKind = AssetSelectionContentKind.AssetItem;
        private string _statusMessage = string.Empty;
        private string _contentErrorMessage = string.Empty;
        private string _externalErrorMessage = string.Empty;

        public MainView(MainViewController controller)
        {
            _controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
            _itemGrid = new AssetItemGrid();
            ApplyGridSize(_controller.ItemsPerRow);
            _itemGrid.AddToClassList(ContentClassName);
            _statusLabel = UiTextFactory.Create(string.Empty, StatusClassName);
            _statusLabel.SetWhiteSpace(WhiteSpace.Normal);
            _errorState = new VisualElement();
            _errorState.AddToClassList(ErrorStateClassName);
            _errorState.style.display = DisplayStyle.None;
            var errorIcon = CreateErrorIcon();
            errorIcon.AddToClassList(ErrorIconClassName);
            _errorState.Add(errorIcon);
            _errorMessage = UiTextFactory.Create(
                string.Empty,
                UiClassNames.MainViewErrorMessage,
                ErrorMessageClassName);
            _errorMessage.SetWhiteSpace(WhiteSpace.Normal);
            _errorState.Add(_errorMessage);
            _fileDetailView = new FileTreeDetailView();
            _fileDetailView.style.display = DisplayStyle.None;
            _tagListPage = new TagListPage();
            _tagListPage.style.display = DisplayStyle.None;

            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);
            Add(_statusLabel);
            Add(_errorState);
            Add(_fileDetailView);
            Add(_tagListPage);
            Add(_itemGrid);

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

        public event System.Action<string> DetailTabRequested;

        internal void SetExternalError(string message)
        {
            _externalErrorMessage = message ?? string.Empty;
            RefreshErrorState();
        }

        public void SetGridSize(int value)
        {
            _controller.SetItemsPerRow(value);
        }

        public void ShowFileDetail(FileTreeDetailState state)
        {
            if (state == null)
            {
                return;
            }

            if (_controller.History.State.Current == null)
            {
                _controller.History.SetCurrent(CreateCurrentHistoryEntry());
            }

            var selectedItem = SelectedAssetItem;
            if (!IsFileListMode &&
                selectedItem != null &&
                _selectionContentKind == AssetSelectionContentKind.AssetItem)
            {
                _fileListItemId = selectedItem.ItemId;
                _fileListItemName = selectedItem.ItemName;
                _browserNodeKind = AssetItemGridNodeKind.Item;
                _browserNodeId = string.Empty;
                _browserNodeName = string.Empty;
            }

            _fileDetailState = state;
            PushCurrentHistory();
            RefreshContent();
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

        private void OnSelectedItemChanged(string itemId)
        {
            if (_applyingHistory)
            {
                return;
            }

            ClearFileListMode();
            ClearFileDetailMode();
            ClearCurrentItemList();
            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnContentChanged()
        {
            ClearFileListMode();
            ClearFileDetailMode();
            ClearCurrentItemList();
            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnCollectionPresentationChanged()
        {
            if (IsFileListMode ||
                IsFileDetailMode ||
                IsTagListMode)
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

            ClearFileDetailMode();

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
                ShowFileDetail(FileTreeDetailState.FromAssetFile(rawId, item.ItemName));
                return;
            }
            else
            {
                _browserNodeKind = kind;
                _browserNodeId = rawId;
                _browserNodeName = item.ItemName;
            }

            DetailTabRequested?.Invoke("file-tree");
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
            Action highlight = isFile
                ? (Action)(() =>
                    projectActions.HighlightFile(targetId))
                : () => projectActions.HighlightItem(targetId);
            var menu = new ContextMenuState(
                new List<ContextMenuItemState>
                {
                    new ContextMenuItemState(
                        "highlight-imported-assets",
                        I18N.Get(
                            isFile
                                ? "assetManager.mainView.context.highlightFile"
                                : "assetManager.mainView.context.highlightItem"),
                        highlight,
                        canHighlight),
                    new ContextMenuItemState(
                        "clear-imported-asset-highlights",
                        I18N.Get(
                            "assetManager.mainView.context.clearHighlights"),
                        projectActions.ClearHighlights)
                });
            ContextMenuWindow.Show(
                target,
                panelPosition,
                menu);
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
                (current.Kind ==
                 AssetItemGridHistoryEntryKind.FileList ||
                 current.Kind ==
                 AssetItemGridHistoryEntryKind.FileDetail))
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
            SetContentError(string.Empty);
            if (IsFileDetailMode)
            {
                _controller.CancelPendingLoad();
                ClearCurrentItemList();
                SetStatus(string.Empty);
                ApplyContentVisibility();
                _fileDetailView.SetState(_fileDetailState);
                return;
            }

            _fileDetailView.SetState(null);
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
                SetStatus(I18N.Get("assetManager.mainView.loadCanceled"));
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
                SetStatus(I18N.Get("assetManager.mainView.loadCanceled"));
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
            SetStatus(ResolveStatusText(statusText));
        }

        private void ApplyTagList(
            string cacheKey,
            System.Collections.Generic.IReadOnlyList<AssetTag> tags)
        {
            _controller.StoreCachedTags(cacheKey, tags);
            _tagListPage.SetTags(tags);
            SetStatus(string.Empty);
        }

        private void SetStatus(string message)
        {
            _statusMessage = message ?? string.Empty;
            _statusLabel.SetText(_statusMessage);
            RefreshStatusVisibility();
        }

        private void SetContentError(string message)
        {
            _contentErrorMessage = message ?? string.Empty;
            RefreshErrorState();
        }

        private void RefreshErrorState()
        {
            var message = !string.IsNullOrWhiteSpace(
                _externalErrorMessage)
                ? _externalErrorMessage
                : _contentErrorMessage;
            var hasError = !string.IsNullOrWhiteSpace(message);
            _errorMessage.SetText(message);
            _errorState.style.display = hasError
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            ApplyContentVisibility();
            RefreshStatusVisibility();
        }

        private void ApplyContentVisibility()
        {
            var hasError =
                !string.IsNullOrWhiteSpace(_externalErrorMessage) ||
                !string.IsNullOrWhiteSpace(_contentErrorMessage);
            _fileDetailView.style.display =
                !hasError && IsFileDetailMode
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _tagListPage.style.display =
                !hasError && !IsFileDetailMode && IsTagListMode
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _itemGrid.style.display =
                !hasError && !IsFileDetailMode && !IsTagListMode
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private void RefreshStatusVisibility()
        {
            var hasError =
                !string.IsNullOrWhiteSpace(_externalErrorMessage) ||
                !string.IsNullOrWhiteSpace(_contentErrorMessage);
            _statusLabel.style.display =
                !hasError && !string.IsNullOrWhiteSpace(_statusMessage)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private static Image CreateErrorIcon()
        {
            Texture2D texture;
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.ErrorCircle,
                out texture);
            var image = new Image
            {
                image = texture,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.style.width =
                FileIconDefinition.StandardIconSize;
            image.style.height =
                FileIconDefinition.StandardIconSize;
            if (texture == null)
            {
                image.style.display = DisplayStyle.None;
            }

            return image;
        }

        private void ClearGridSelection()
        {
            _itemGrid.ClearSelection(notify: false);
            SetSelection(null, AssetSelectionContentKind.AssetItem);
        }

        private bool IsFileListMode
        {
            get { return !string.IsNullOrWhiteSpace(_fileListItemId); }
        }

        private bool IsFileDetailMode
        {
            get { return _fileDetailState != null; }
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

        private void ClearFileDetailMode()
        {
            _fileDetailState = null;
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
            if (IsFileDetailMode)
            {
                return new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.FileDetail,
                    selectedItem.Id,
                    selectedItem.Label,
                    _fileListItemId,
                    _fileListItemName,
                    _browserNodeKind,
                    _browserNodeId,
                    _browserNodeName,
                    _fileDetailState.Id,
                    _fileDetailState.Name,
                    _fileDetailState.ParentName,
                    viewPath,
                    _fileDetailState.Extension);
            }

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
                if (entry.Kind == AssetItemGridHistoryEntryKind.FileList ||
                    entry.Kind == AssetItemGridHistoryEntryKind.FileDetail)
                {
                    _fileListItemId = entry.ItemId;
                    _fileListItemName = entry.ItemName;
                    _browserNodeKind = entry.NodeKind;
                    _browserNodeId = entry.NodeId;
                    _browserNodeName = entry.NodeName;
                    DetailTabRequested?.Invoke("file-tree");
                    _fileDetailState = entry.Kind == AssetItemGridHistoryEntryKind.FileDetail
                        ? new FileTreeDetailState(
                            entry.DetailId,
                            entry.DetailName,
                            entry.DetailParentName,
                            entry.DetailExtension)
                        : null;
                }
                else
                {
                    ClearFileListMode();
                    ClearFileDetailMode();
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
