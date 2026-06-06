using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class MainView : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--main-view";
        private const string ContentClassName = "ee4v-asset-manager-panel__main-content";
        private const string StatusClassName = "ee4v-asset-manager-panel__main-status";
        private static readonly Dictionary<string, CachedGridState> GridCache = new Dictionary<string, CachedGridState>(StringComparer.Ordinal);
        private static bool _cacheInvalidationRegistered;
        private readonly Func<IAssetManagerItemListProvider> _itemListProviderResolver;
        private readonly ItemGrid _itemGrid;
        private readonly UiTextElement _statusLabel;
        private int _loadVersion;

        public MainView(Func<IAssetManagerItemListProvider> itemListProviderResolver = null)
        {
            _itemListProviderResolver = itemListProviderResolver ?? AssetManagerItemListProviderRegistry.GetCurrent;
            EnsureCacheInvalidationRegistered();
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
            var cacheKey = CreateGridCacheKey(request);
            CachedGridState cached;
            if (GridCache.TryGetValue(cacheKey, out cached))
            {
                _itemGrid.SetState(cached.GridState);
                SetStatus(cached.StatusText);
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

                    ApplyItemList(cacheKey, task.Result);
                };
            });
        }

        private void ApplyItemList(string cacheKey, AssetManagerItemList itemList)
        {
            var list = itemList ?? new AssetManagerItemList(null);
            var itemCardStates = new List<ItemCardState>(list.Items.Count);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var item = list.Items[i];
                if (item == null)
                {
                    continue;
                }

                itemCardStates.Add(new ItemCardState(item.ItemName, CreateThumbnail(item.ThumbnailData)));
            }

            var gridState = new ItemGridState(itemCardStates, list.ItemsPerRow);
            var statusText = itemCardStates.Count == 0 ? list.EmptyText : string.Empty;
            GridCache[cacheKey] = new CachedGridState(gridState, statusText);
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

        private Texture2D CreateThumbnail(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            var texture = new Texture2D(2, 2)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (texture.LoadImage(data))
            {
                return texture;
            }

            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }

        private static string CreateGridCacheKey(AssetManagerItemListRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var limit = request != null ? request.Limit : 200;
            return AssetManagerItemListProviderRegistry.CacheVersion + "|" + viewId + "|" + limit;
        }

        private static void EnsureCacheInvalidationRegistered()
        {
            if (_cacheInvalidationRegistered)
            {
                return;
            }

            _cacheInvalidationRegistered = true;
            AssetManagerItemListProviderRegistry.SessionCacheCleared += ClearGridCache;
        }

        private static void ClearGridCache()
        {
            foreach (var cached in GridCache.Values)
            {
                if (cached == null || cached.GridState == null || cached.GridState.Items == null)
                {
                    continue;
                }

                for (var i = 0; i < cached.GridState.Items.Count; i++)
                {
                    var item = cached.GridState.Items[i];
                    if (item != null && item.Thumbnail != null)
                    {
                        UnityEngine.Object.DestroyImmediate(item.Thumbnail);
                    }
                }
            }

            GridCache.Clear();
        }

        private sealed class CachedGridState
        {
            public CachedGridState(ItemGridState gridState, string statusText)
            {
                GridState = gridState;
                StatusText = statusText ?? string.Empty;
            }

            public ItemGridState GridState { get; }

            public string StatusText { get; }
        }
    }
}
