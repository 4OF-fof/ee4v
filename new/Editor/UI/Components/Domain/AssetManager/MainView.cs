using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";
        private const string ContentClassName = "ee4v-asset-manager-panel__main-content";
        private const string StatusClassName = "ee4v-asset-manager-panel__main-status";
        private readonly Func<IAssetManagerItemListProvider> _itemListProviderResolver;
        private readonly ItemGrid _itemGrid;
        private readonly UiTextElement _statusLabel;
        private int _loadVersion;

        public MainView(Func<IAssetManagerItemListProvider> itemListProviderResolver = null)
        {
            _itemListProviderResolver = itemListProviderResolver ?? AssetManagerItemListProviderRegistry.GetCurrent;
            ItemGridCache.EnsureCacheInvalidationRegistered();
            _itemGrid = new ItemGrid();
            _itemGrid.AddToClassList(ContentClassName);
            _statusLabel = UiTextFactory.Create(string.Empty, StatusClassName);
            _statusLabel.SetWhiteSpace(WhiteSpace.Normal);

            AssetManagerPanelFactory.PrepareHost(this, RootClassName);
            Add(_statusLabel);
            Add(_itemGrid);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged += OnSelectedItemChanged;
            AssetManagerItemListProviderRegistry.SessionCacheCleared += OnSessionCacheCleared;
            RefreshContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged -= OnSelectedItemChanged;
            AssetManagerItemListProviderRegistry.SessionCacheCleared -= OnSessionCacheCleared;
            _loadVersion++;
        }

        private void OnSelectedItemChanged(string itemId)
        {
            RefreshContent();
        }

        private void OnSessionCacheCleared()
        {
            RefreshContent();
        }

        private void RefreshContent()
        {
            var selectedItem = AssetManagerViewState.SelectedItem;
            var request = new AssetManagerItemListRequest(selectedItem.Id);
            ItemGridState cachedGridState;
            string cachedStatusText;
            if (ItemGridCache.TryGet(request, out cachedGridState, out cachedStatusText))
            {
                _itemGrid.SetState(cachedGridState);
                SetStatus(cachedStatusText);
                return;
            }

            var version = ++_loadVersion;
            SetStatus("Loading assets...");
            _itemGrid.SetState(new ItemGridState(null));

            Task.Run(() => ResolveItemListProvider().GetItems(request)).ContinueWith(task =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (version != _loadVersion)
                    {
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        SetStatus(task.Exception != null ? task.Exception.GetBaseException().Message : "Failed to load assets.");
                        return;
                    }

                    if (task.IsCanceled)
                    {
                        SetStatus("Asset loading was canceled.");
                        return;
                    }

                    ApplyItemList(request, task.Result);
                };
            });
        }

        private void ApplyItemList(AssetManagerItemListRequest request, AssetManagerItemList itemList)
        {
            ItemGridState gridState;
            string statusText;
            ItemGridCache.Store(request, itemList, out gridState, out statusText);
            _itemGrid.SetState(gridState);
            SetStatus(statusText);
        }

        private void SetStatus(string message)
        {
            _statusLabel.SetText(message);
            _statusLabel.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private IAssetManagerItemListProvider ResolveItemListProvider()
        {
            var provider = _itemListProviderResolver != null ? _itemListProviderResolver() : null;
            return provider ?? AssetManagerItemListProviderRegistry.Current;
        }
    }
}
