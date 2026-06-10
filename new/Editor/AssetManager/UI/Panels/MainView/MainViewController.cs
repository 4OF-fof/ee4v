using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Api;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;

namespace Ee4v.AssetManager
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
                return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noItems"), itemsPerRow);
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

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noItems"), itemsPerRow);
        }

        public AssetItemGridList LoadFiles(string itemId)
        {
            var files = AssetManagerApi.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            var items = new List<AssetItemGridListItem>();
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file == null)
                {
                    continue;
                }

                items.Add(new AssetItemGridListItem(
                    file.Id,
                    file.FileName,
                    new ItemImageState(),
                    CreateFileIcon(file.Extension),
                    itemId));
            }

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noFiles"), GetItemsPerRow());
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

        private static IconState CreateFileIcon(string extension)
        {
            return IconState.FromBuiltinIcon(ResolveFileIcon(extension), size: 44f);
        }

        private static UiBuiltinIcon ResolveFileIcon(string extension)
        {
            switch ((extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant())
            {
                case "zip":
                case "rar":
                case "7z":
                case "tar":
                case "gz":
                case "unitypackage":
                    return UiBuiltinIcon.ArchiveFile;
                case "png":
                case "jpg":
                case "jpeg":
                case "gif":
                case "webp":
                case "psd":
                case "clip":
                    return UiBuiltinIcon.ImageFile;
                case "txt":
                case "md":
                case "json":
                case "jsonc":
                case "xml":
                case "yaml":
                case "yml":
                    return UiBuiltinIcon.TextFile;
                case "unity":
                case "asset":
                case "prefab":
                case "mat":
                case "controller":
                case "anim":
                    return UiBuiltinIcon.UnityFile;
                case "fbx":
                case "obj":
                case "blend":
                case "vrm":
                case "glb":
                case "gltf":
                    return UiBuiltinIcon.ModelFile;
                case "wav":
                case "mp3":
                case "ogg":
                case "aiff":
                    return UiBuiltinIcon.AudioFile;
                case "cs":
                case "js":
                case "ts":
                case "shader":
                case "cginc":
                    return UiBuiltinIcon.ScriptFile;
                default:
                    return UiBuiltinIcon.GenericFile;
            }
        }

        private static int GetItemsPerRow()
        {
            return Math.Min(12, Math.Max(1, SettingApi.Get(AssetManagerDefinitions.ItemGridItemsPerRow)));
        }
    }
}
