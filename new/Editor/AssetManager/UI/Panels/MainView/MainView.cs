using System.Threading.Tasks;
using Ee4v.Core.I18n;
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
        private string _fileListItemId;
        private string _fileListItemName;

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

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged += OnSelectedItemChanged;
            MainViewController.ContentChanged += OnContentChanged;
            RefreshContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged -= OnSelectedItemChanged;
            MainViewController.ContentChanged -= OnContentChanged;
            _loadVersion++;
        }

        private void OnSelectedItemChanged(string itemId)
        {
            ClearFileListMode();
            ClearGridSelection();
            RefreshContent();
        }

        private void OnContentChanged()
        {
            _itemGrid.ClearCachedItems();
            ClearFileListMode();
            ClearGridSelection();
            RefreshContent();
        }

        private void OnGridSelectionChanged(System.Collections.Generic.IReadOnlyList<ItemCardState> items)
        {
            AssetManagerViewState.SetSelectedAssetItems(
                items,
                contentKind: IsFileListMode
                    ? AssetManagerViewState.AssetSelectionContentKind.AssetFile
                    : AssetManagerViewState.AssetSelectionContentKind.AssetItem);
        }

        private void OnGridItemDoubleClicked(ItemCardState item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || IsFileListMode)
            {
                return;
            }

            _fileListItemId = item.ItemId;
            _fileListItemName = item.ItemName;
            AssetManagerViewState.SetSelectedAssetDetailTab("file-tree");
            ClearGridSelection();
            RefreshContent();
        }

        private void RefreshContent()
        {
            var cacheKey = CreateCurrentCacheKey();
            string cachedStatusText;
            if (_itemGrid.TrySetCachedItems(cacheKey, out cachedStatusText))
            {
                SetStatus(ResolveStatusText(cachedStatusText));
                return;
            }

            var version = ++_loadVersion;
            SetStatus(IsFileListMode
                ? I18N.Get("assetManager.mainView.loadingFiles")
                : I18N.Get("assetManager.mainView.loading"));
            _itemGrid.SetLoading();

            Task.Run(LoadCurrentGridItems).ContinueWith(task =>
            {
                EditorApplication.delayCall += () =>
                {
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

        private AssetItemGridList LoadCurrentGridItems()
        {
            if (IsFileListMode)
            {
                return _controller.LoadFiles(_fileListItemId);
            }

            var selectedItem = AssetManagerViewState.SelectedItem;
            return _controller.LoadItems(_controller.CreateRequest(selectedItem.Id));
        }

        private string CreateCurrentCacheKey()
        {
            if (IsFileListMode)
            {
                return "files|" + _fileListItemId;
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
        }
    }
}
