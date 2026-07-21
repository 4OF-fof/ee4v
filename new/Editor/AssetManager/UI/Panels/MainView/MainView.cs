using System.Threading;
using System.Threading.Tasks;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";
        private const string ContentClassName = "ee4v-asset-manager-panel__main-content";
        private const string StatusClassName = "ee4v-asset-manager-panel__main-status";
        private readonly MainViewController _controller;
        private readonly AssetItemGrid _itemGrid;
        private readonly UiTextElement _statusLabel;
        private int _loadVersion;
        private CancellationTokenSource _loadCancellation;
        private string _fileListItemId;
        private string _fileListItemName;
        private AssetItemGridNodeKind _browserNodeKind;
        private string _browserNodeId;
        private string _browserNodeName;
        private bool _applyingHistory;

        public MainView(MainViewController controller = null)
        {
            _controller = controller ?? new MainViewController();
            _itemGrid = new AssetItemGrid();
            _itemGrid.AddToClassList(ContentClassName);
            _statusLabel = UiTextFactory.Create(string.Empty, StatusClassName);
            _statusLabel.SetWhiteSpace(WhiteSpace.Normal);

            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);
            Add(_statusLabel);
            Add(_itemGrid);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            _itemGrid.SelectionChanged += OnGridSelectionChanged;
            _itemGrid.ItemDoubleClicked += OnGridItemDoubleClicked;
        }

        public AssetItemGridHistory History
        {
            get { return _itemGrid.History; }
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged += OnSelectedItemChanged;
            MainViewController.ContentChanged += OnContentChanged;
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged -= OnSelectedItemChanged;
            MainViewController.ContentChanged -= OnContentChanged;
            CancelPendingLoad();
            _loadVersion++;
        }

        private void OnSelectedItemChanged(string itemId)
        {
            if (_applyingHistory)
            {
                return;
            }

            ClearFileListMode();
            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnContentChanged()
        {
            _itemGrid.ClearCachedItems();
            ClearFileListMode();
            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        private void OnGridSelectionChanged(System.Collections.Generic.IReadOnlyList<ItemCardState> items)
        {
            AssetManagerViewState.SetSelectedAssetItems(CreateSelectionItems(items), contentKind: ResolveSelectionContentKind(items));
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

            AssetManagerViewState.SetSelectedAssetDetailTab("file-tree");
            ClearGridSelection();
            PushCurrentHistory();
            RefreshContent();
        }

        public void GoBack()
        {
            AssetItemGridHistoryEntry entry;
            if (_itemGrid.History.TryGoBack(out entry))
            {
                ApplyHistoryEntry(entry);
            }
        }

        public void GoForward()
        {
            AssetItemGridHistoryEntry entry;
            if (_itemGrid.History.TryGoForward(out entry))
            {
                ApplyHistoryEntry(entry);
            }
        }

        public void GoToBreadcrumb(int index)
        {
            var current = _itemGrid.History.State.Current;
            if (current == null
                || current.Kind != AssetItemGridHistoryEntryKind.FileList
                || index < 0)
            {
                return;
            }

            AssetItemGridHistoryEntry entry;
            if (index == 0)
            {
                entry = new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.View,
                    current.ViewId,
                    current.ViewLabel);
            }
            else if (index == 1)
            {
                entry = new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.FileList,
                    current.ViewId,
                    current.ViewLabel,
                    current.ItemId,
                    current.ItemName);
            }
            else
            {
                return;
            }

            _itemGrid.History.SetCurrent(entry);
            ApplyHistoryEntry(entry);
        }

        private void RefreshContent()
        {
            SettingApi.Preload(SettingScope.User);
            var cacheKey = CreateCurrentCacheKey();
            string cachedStatusText;
            if (_itemGrid.TrySetCachedItems(cacheKey, out cachedStatusText))
            {
                SetStatus(ResolveStatusText(cachedStatusText));
                return;
            }

            CancelPendingLoad();
            var loadCancellation = new CancellationTokenSource();
            _loadCancellation = loadCancellation;
            var cancellationToken = loadCancellation.Token;
            var version = ++_loadVersion;
            SetStatus(IsFileListMode
                ? I18N.Get("assetManager.mainView.loadingChildren")
                : I18N.Get("assetManager.mainView.loading"));
            _itemGrid.SetLoading();

            Task.Run(() => LoadCurrentGridItems(cancellationToken), cancellationToken).ContinueWith(task =>
            {
                EditorApplication.delayCall += () =>
                {
                    loadCancellation.Dispose();
                    if (ReferenceEquals(_loadCancellation, loadCancellation))
                    {
                        _loadCancellation = null;
                    }

                    if (version != _loadVersion)
                    {
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        SetStatus(task.Exception != null ? task.Exception.GetBaseException().Message : I18N.Get("assetManager.mainView.loadFailed"));
                        return;
                    }

                    if (task.IsCanceled)
                    {
                        SetStatus(I18N.Get("assetManager.mainView.loadCanceled"));
                        return;
                    }

                    ApplyItemList(cacheKey, task.Result);
                };
            });
        }

        private void ApplyItemList(string cacheKey, AssetItemGridList itemList)
        {
            string statusText;
            _itemGrid.SetAssetItems(cacheKey, itemList, out statusText);
            SetStatus(ResolveStatusText(statusText));
        }

        private void SetStatus(string message)
        {
            _statusLabel.SetText(message);
            _statusLabel.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ClearGridSelection()
        {
            _itemGrid.ClearSelection(notify: false);
            AssetManagerViewState.SetSelectedAssetItems(null);
        }

        private bool IsFileListMode
        {
            get { return !string.IsNullOrWhiteSpace(_fileListItemId); }
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

            var selectedItem = AssetManagerViewState.SelectedItem;
            return _controller.LoadItems(_controller.CreateRequest(selectedItem.Id), cancellationToken);
        }

        private void CancelPendingLoad()
        {
            if (_loadCancellation == null)
            {
                return;
            }

            _loadCancellation.Cancel();
            _loadCancellation = null;
        }

        private string CreateCurrentCacheKey()
        {
            if (IsFileListMode)
            {
                return "children|" + _fileListItemId + "|" + _browserNodeKind + "|" + _browserNodeId;
            }

            var selectedItem = AssetManagerViewState.SelectedItem;
            return _controller.CreateCacheKey(_controller.CreateRequest(selectedItem.Id));
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

            _itemGrid.History.SetCurrent(CreateCurrentHistoryEntry());
        }

        private AssetItemGridHistoryEntry CreateCurrentHistoryEntry()
        {
            var selectedItem = AssetManagerViewState.SelectedItem;
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
                    _browserNodeName);
            }

            return new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.View,
                selectedItem.Id,
                selectedItem.Label);
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
                AssetManagerViewState.SetSelectedItem(entry.ViewId);
                if (entry.Kind == AssetItemGridHistoryEntryKind.FileList)
                {
                    _fileListItemId = entry.ItemId;
                    _fileListItemName = entry.ItemName;
                    _browserNodeKind = entry.NodeKind;
                    _browserNodeId = entry.NodeId;
                    _browserNodeName = entry.NodeName;
                    AssetManagerViewState.SetSelectedAssetDetailTab("file-tree");
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
                result.Add(AssetItemGridNodeKey.TryDecode(item.ItemId, out kind, out rawId)
                    ? new ItemCardState(rawId, item.ItemName, item.ImageState, item.IconState, item.ParentItemId)
                    : item);
            }

            return result;
        }

        private static AssetManagerViewState.AssetSelectionContentKind ResolveSelectionContentKind(System.Collections.Generic.IReadOnlyList<ItemCardState> items)
        {
            if (items == null || items.Count == 0)
            {
                return AssetManagerViewState.AssetSelectionContentKind.AssetItem;
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
                else
                {
                    hasGroup = true;
                }
            }

            if (hasFile && !hasGroup)
            {
                return AssetManagerViewState.AssetSelectionContentKind.AssetFile;
            }

            return hasGroup
                ? AssetManagerViewState.AssetSelectionContentKind.AssetGroup
                : AssetManagerViewState.AssetSelectionContentKind.AssetItem;
        }
    }
}
