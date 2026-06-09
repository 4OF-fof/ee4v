using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Api;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.UI
{
    internal sealed class MainViewRequest
    {
        public MainViewRequest(string viewId, int limit = 200)
        {
            ViewId = viewId ?? string.Empty;
            Limit = limit <= 0 ? 200 : limit;
        }

        public string ViewId { get; }

        public int Limit { get; }
    }

    [InitializeOnLoad]
    internal sealed class MainViewController
    {
        private static int _contentVersion;

        static MainViewController()
        {
            AssetManagerApi.Changed -= OnAssetManagerChanged;
            AssetManagerApi.Changed += OnAssetManagerChanged;
            SettingApi.Changed -= OnSettingChanged;
            SettingApi.Changed += OnSettingChanged;
        }

        public static event Action ContentChanged;

        public MainViewRequest CreateRequest(string viewId)
        {
            return new MainViewRequest(viewId);
        }

        public string CreateCacheKey(MainViewRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var limit = request != null ? request.Limit : 200;
            return _contentVersion + "|" + viewId + "|" + limit + "|" + GetItemsPerRow();
        }

        public AssetItemGridList LoadItems(MainViewRequest request)
        {
            var itemsPerRow = GetItemsPerRow();
            var query = CreateQuery(request);
            var result = AssetManagerApi.SearchItems(query);
            var items = new List<AssetItemGridListItem>();
            if (result == null || result.Items == null)
            {
                return new AssetItemGridList(items, "No asset items.", itemsPerRow);
            }

            for (var i = 0; i < result.Items.Count; i++)
            {
                var item = result.Items[i];
                if (item == null)
                {
                    continue;
                }

                items.Add(new AssetItemGridListItem(item.Id, item.Name, LoadThumbnail(item.Id)));
            }

            return new AssetItemGridList(items, "No asset items.", itemsPerRow);
        }

        private static void OnAssetManagerChanged()
        {
            InvalidateContent();
        }

        private static void OnSettingChanged(SettingDefinitionBase definition, object value)
        {
            if (definition == AssetManagerDefinitions.ItemGridItemsPerRow)
            {
                InvalidateContent();
            }
        }

        private static void InvalidateContent()
        {
            _contentVersion++;
            ContentChanged?.Invoke();
        }

        private static AssetItemQuery CreateQuery(MainViewRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var query = new AssetItemQuery
            {
                Limit = request != null ? request.Limit : 200
            };

            if (string.Equals(viewId, "booth-library", StringComparison.Ordinal))
            {
                query.SourceTypes = new[] { AssetSourceType.Blm, AssetSourceType.Eagle };
            }

            return query;
        }

        private static ItemImageState LoadThumbnail(string itemId)
        {
            var thumbnail = AssetManagerApi.GetThumbnail(itemId);
            if (thumbnail == null || !thumbnail.Found)
            {
                return new ItemImageState();
            }

            return new ItemImageState(
                string.IsNullOrWhiteSpace(thumbnail.Path) ? null : thumbnail.Path,
                thumbnail.Data);
        }

        private static int GetItemsPerRow()
        {
            return Math.Min(12, Math.Max(1, SettingApi.Get(AssetManagerDefinitions.ItemGridItemsPerRow)));
        }
    }
}
