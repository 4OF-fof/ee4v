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
            ClearGridSelection();
            RefreshContent();
        }

        private void OnContentChanged()
        {
            _itemGrid.ClearCachedItems();
            ClearGridSelection();
            RefreshContent();
        }

        private void OnGridSelectionChanged(System.Collections.Generic.IReadOnlyList<ItemCardState> items)
        {
            AssetManagerViewState.SetSelectedAssetItems(items);
        }

        private void RefreshContent()
        {
            var selectedItem = AssetManagerViewState.SelectedItem;
            var request = _controller.CreateRequest(selectedItem.Id);
            var cacheKey = _controller.CreateCacheKey(request);
            string cachedStatusText;
            if (_itemGrid.TrySetCachedItems(cacheKey, out cachedStatusText))
            {
                SetStatus(cachedStatusText);
                return;
            }

            var version = ++_loadVersion;
            SetStatus(I18N.Get("assetManager.mainView.loading"));
            _itemGrid.SetLoading();

            Task.Run(() => _controller.LoadItems(request)).ContinueWith(task =>
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
            SetStatus(statusText);
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
    }
}
