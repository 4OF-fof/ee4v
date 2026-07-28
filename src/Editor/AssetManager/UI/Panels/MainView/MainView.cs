using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";
        private const string ContentClassName = "ee4v-asset-manager-panel__main-content";
        private const string StatusClassName = "ee4v-asset-manager-panel__main-status";
        private readonly MainViewController _controller;
        private readonly AssetItemGrid _itemGrid;
        private readonly UiTextElement _statusLabel;
        private readonly FileTreeDetailView _fileDetailView;
        private readonly TagListPage _tagListPage;
        private string _fileListItemId;
        private string _fileListItemName;
        private AssetItemGridNodeKind _browserNodeKind;
        private string _browserNodeId;
        private string _browserNodeName;
        private FileTreeDetailState _fileDetailState;
        private string _searchText = string.Empty;
        private bool _applyingHistory;
        private ItemCardState[] _selectedAssetItems = System.Array.Empty<ItemCardState>();
        private AssetSelectionContentKind _selectionContentKind = AssetSelectionContentKind.AssetItem;

        public MainView(MainViewController controller)
        {
            _controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
            _itemGrid = new AssetItemGrid();
            ApplyGridSize(_controller.ItemsPerRow);
            _itemGrid.AddToClassList(ContentClassName);
            _statusLabel = UiTextFactory.Create(string.Empty, StatusClassName);
            _statusLabel.SetWhiteSpace(WhiteSpace.Normal);
            _fileDetailView = new FileTreeDetailView();
            _fileDetailView.style.display = DisplayStyle.None;
            _tagListPage = new TagListPage();
            _tagListPage.style.display = DisplayStyle.None;

            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);
            Add(_statusLabel);
            Add(_fileDetailView);
            Add(_tagListPage);
            Add(_itemGrid);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            _itemGrid.SelectionChanged += OnGridSelectionChanged;
            _itemGrid.ItemDoubleClicked += OnGridItemDoubleClicked;
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
            _controller.ClearCachedItems();
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
            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnContentChanged()
        {
            _controller.ClearCachedItems();
            ClearFileListMode();
            ClearFileDetailMode();
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

            var cacheKey = CreateCurrentCacheKey();
            AssetItemGridList cachedItems;
            if (!_controller.TryGetCachedItems(
                    cacheKey,
                    out cachedItems))
            {
                return;
            }

            ClearGridSelection();
            ApplyItemList(cacheKey, cachedItems);
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
            if (IsFileDetailMode)
            {
                _controller.CancelPendingLoad();
                SetStatus(string.Empty);
                _itemGrid.style.display = DisplayStyle.None;
                _tagListPage.style.display = DisplayStyle.None;
                _fileDetailView.style.display = DisplayStyle.Flex;
                _fileDetailView.SetState(_fileDetailState);
                return;
            }

            _fileDetailView.SetState(null);
            _fileDetailView.style.display = DisplayStyle.None;
            if (IsTagListMode)
            {
                _itemGrid.style.display = DisplayStyle.None;
                _tagListPage.style.display = DisplayStyle.Flex;
                var tagCacheKey = CreateCurrentCacheKey();
                System.Collections.Generic.IReadOnlyList<AssetTag> cachedTags;
                if (_controller.TryGetCachedTags(tagCacheKey, out cachedTags))
                {
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

            _tagListPage.style.display = DisplayStyle.None;
            _itemGrid.style.display = DisplayStyle.Flex;
            var cacheKey = CreateCurrentCacheKey();
            AssetItemGridList cachedItems;
            if (_controller.TryGetCachedItems(cacheKey, out cachedItems))
            {
                ApplyItemList(cacheKey, cachedItems);
                return;
            }

            SetStatus(IsFileListMode
                ? I18N.Get("assetManager.mainView.loadingChildren")
                : I18N.Get("assetManager.mainView.loading"));
            _itemGrid.SetLoading();
            _controller.StartLoad(cacheKey, LoadCurrentGridItems);
        }

        private void OnLoadCompleted(MainViewLoadResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.Error != null)
            {
                SetStatus(result.Error.Message);
                return;
            }

            if (result.Canceled)
            {
                SetStatus(I18N.Get("assetManager.mainView.loadCanceled"));
                return;
            }

            ApplyItemList(result.CacheKey, result.Items);
        }

        private void OnTagListLoadCompleted(TagListLoadResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.Error != null)
            {
                SetStatus(result.Error.Message);
                return;
            }

            if (result.Canceled)
            {
                SetStatus(I18N.Get("assetManager.mainView.loadCanceled"));
                return;
            }

            ApplyTagList(result.CacheKey, result.Tags);
        }

        private void ApplyItemList(string cacheKey, AssetItemGridList itemList)
        {
            _controller.StoreCachedItems(cacheKey, itemList);
            var displayItems = IsFileListMode
                ? itemList
                : _controller.CreateDisplayItems(
                    _controller.CreateRequest(
                        _controller.SelectedNavigationItemId,
                        _searchText),
                    itemList);
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
            _statusLabel.SetText(message);
            _statusLabel.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
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

            var selectedItem = _controller.SelectedNavigationItem;
            return _controller.LoadItems(_controller.CreateRequest(selectedItem.Id, _searchText), cancellationToken);
        }

        private string CreateCurrentCacheKey()
        {
            if (IsFileListMode)
            {
                return "children|" + _fileListItemId + "|" + _browserNodeKind + "|" + _browserNodeId;
            }

            var selectedItem = _controller.SelectedNavigationItem;
            return _controller.CreateCacheKey(_controller.CreateRequest(selectedItem.Id, _searchText));
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
                    viewPath);
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
                        ? new FileTreeDetailState(entry.DetailId, entry.DetailName, entry.DetailParentName)
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
                            item.ParentItemId));
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
                    hasGroup = true;
                }
            }

            if (hasFile && !hasGroup)
            {
                return AssetSelectionContentKind.AssetFile;
            }

            return hasGroup
                ? AssetSelectionContentKind.AssetGroup
                : AssetSelectionContentKind.AssetItem;
        }
    }
}
