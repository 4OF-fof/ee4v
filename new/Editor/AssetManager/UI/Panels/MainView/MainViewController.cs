using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Api;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;

namespace Ee4v.AssetManager
{
    internal sealed class MainViewRequest
    {
        public MainViewRequest(string viewId, string keyword = null, int limit = 200)
        {
            ViewId = viewId ?? string.Empty;
            Keyword = keyword ?? string.Empty;
            Limit = limit <= 0 ? 200 : limit;
        }

        public string ViewId { get; }

        public string Keyword { get; }

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

        public static event Action LayoutChanged;

        public static event Action<int> HistoryOverlayMaximumItemsChanged;

        public int ItemsPerRow
        {
            get { return GetItemsPerRow(); }
        }

        public int HistoryOverlayMaximumItems
        {
            get { return GetHistoryOverlayMaximumItems(); }
        }

        public void SetItemsPerRow(int value)
        {
            SettingApi.Set(AssetManagerDefinitions.ItemGridItemsPerRow, Math.Min(12, Math.Max(1, value)));
        }

        public MainViewRequest CreateRequest(string viewId, string keyword = null)
        {
            return new MainViewRequest(viewId, keyword);
        }

        public string CreateCacheKey(MainViewRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var keyword = request != null ? request.Keyword : string.Empty;
            var limit = request != null ? request.Limit : 200;
            return _contentVersion + "|" + viewId + "|" + keyword + "|" + limit + "|" + GetItemsPerRow();
        }

        public AssetItemGridList LoadItems(MainViewRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            var itemsPerRow = GetItemsPerRow();
            var query = CreateQuery(request);
            var result = AssetManagerApi.SearchItems(query);
            var items = new List<AssetItemGridListItem>();
            if (result == null || result.Items == null)
            {
                return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noItems"), itemsPerRow);
            }

            var loadedItems = new AssetItemGridListItem[result.Items.Count];
            Parallel.For(0, result.Items.Count, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 4
            }, i =>
            {
                var item = result.Items[i];
                if (item == null)
                {
                    return;
                }

                loadedItems[i] = new AssetItemGridListItem(item.Id, item.Name, LoadThumbnail(item.Id));
            });
            items.AddRange(loadedItems.Where(item => item != null));

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

        public AssetItemGridList LoadItemChildren(string itemId)
        {
            var items = new List<AssetItemGridListItem>();
            var variants = AssetManagerApi.GetVariantGroups(itemId);
            for (var i = 0; i < variants.Count; i++)
            {
                items.Add(CreateGroupListItem(AssetItemGridNodeKind.VariantGroup, variants[i].Id, variants[i].Name, itemId));
            }

            var versions = AssetManagerApi.GetVersionGroups(itemId);
            for (var i = 0; i < versions.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(versions[i].VariantGroupId))
                {
                    items.Add(CreateGroupListItem(AssetItemGridNodeKind.VersionGroup, versions[i].Id, versions[i].Name, itemId));
                }
            }

            var files = AssetManagerApi.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            AddFiles(items, files, itemId, file => !string.IsNullOrWhiteSpace(file.ItemId));
            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"), GetItemsPerRow());
        }

        public AssetItemGridList LoadGroupChildren(string itemId, AssetItemGridNodeKind groupKind, string groupId)
        {
            var items = new List<AssetItemGridListItem>();
            var files = AssetManagerApi.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            if (groupKind == AssetItemGridNodeKind.VariantGroup)
            {
                var versions = AssetManagerApi.GetVersionGroups(itemId);
                for (var i = 0; i < versions.Count; i++)
                {
                    if (string.Equals(versions[i].VariantGroupId, groupId, StringComparison.Ordinal))
                    {
                        items.Add(CreateGroupListItem(AssetItemGridNodeKind.VersionGroup, versions[i].Id, versions[i].Name, itemId));
                    }
                }

                AddFiles(items, files, itemId, file => string.Equals(file.VariantGroupId, groupId, StringComparison.Ordinal));
                return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"), GetItemsPerRow());
            }

            if (groupKind == AssetItemGridNodeKind.VersionGroup)
            {
                AddFiles(items, files, itemId, file => string.Equals(file.VersionGroupId, groupId, StringComparison.Ordinal));
            }

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"), GetItemsPerRow());
        }

        private static void OnAssetManagerChanged()
        {
            InvalidateContent();
        }

        private static void OnSettingChanged(SettingDefinitionBase definition, object value)
        {
            if (definition == AssetManagerDefinitions.ItemGridItemsPerRow)
            {
                _contentVersion++;
                LayoutChanged?.Invoke();
            }
            else if (definition == AssetManagerDefinitions.HistoryOverlayMaximumItems)
            {
                HistoryOverlayMaximumItemsChanged?.Invoke(GetHistoryOverlayMaximumItems());
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
                Keyword = request != null ? request.Keyword : string.Empty,
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

        private static AssetItemGridListItem CreateGroupListItem(AssetItemGridNodeKind kind, string id, string name, string itemId)
        {
            return new AssetItemGridListItem(
                AssetItemGridNodeKey.Encode(kind, id),
                name,
                new ItemImageState(),
                IconState.FromBuiltinIcon(kind == AssetItemGridNodeKind.VariantGroup ? UiBuiltinIcon.DisclosureClosed : UiBuiltinIcon.DisclosureOpen, size: 44f),
                itemId);
        }

        private static void AddFiles(ICollection<AssetItemGridListItem> items, IReadOnlyList<AssetFile> files, string itemId, Func<AssetFile, bool> predicate)
        {
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file == null || !predicate(file))
                {
                    continue;
                }

                items.Add(new AssetItemGridListItem(
                    AssetItemGridNodeKey.Encode(AssetItemGridNodeKind.File, file.Id),
                    file.FileName,
                    new ItemImageState(),
                    CreateFileIcon(file.Extension),
                    itemId));
            }
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

        private static int GetHistoryOverlayMaximumItems()
        {
            return Math.Min(20, Math.Max(1, SettingApi.Get(AssetManagerDefinitions.HistoryOverlayMaximumItems)));
        }
    }
}
