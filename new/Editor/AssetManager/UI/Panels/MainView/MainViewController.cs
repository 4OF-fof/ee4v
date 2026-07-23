using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;

namespace Ee4v.AssetManager.UI
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

    internal sealed class MainViewLoadResult
    {
        public MainViewLoadResult(string cacheKey, AssetItemGridList items, Exception error, bool canceled)
        {
            CacheKey = cacheKey ?? string.Empty;
            Items = items;
            Error = error;
            Canceled = canceled;
        }

        public string CacheKey { get; }

        public AssetItemGridList Items { get; }

        public Exception Error { get; }

        public bool Canceled { get; }
    }

    internal sealed class MainViewController : IDisposable
    {
        private readonly IAssetManager _assetManager;
        private readonly IAssetManagerUiPreferences _preferences;
        private readonly IAssetManagerUiScheduler _scheduler;
        private readonly Dictionary<string, AssetItemGridList> _itemCache =
            new Dictionary<string, AssetItemGridList>(StringComparer.Ordinal);
        private int _contentVersion;
        private int _activationCount;
        private int _loadVersion;
        private CancellationTokenSource _loadCancellation;
        private string _selectedNavigationItemId = AssetManagerNavigationCatalog.DefaultItemId;

        public MainViewController(
            IAssetManager assetManager = null,
            IAssetManagerUiPreferences preferences = null,
            IAssetManagerUiScheduler scheduler = null)
        {
            _assetManager = assetManager ?? AssetManagerUiDependencies.AssetManager;
            _preferences = preferences ?? AssetManagerUiDependencies.Preferences;
            _scheduler = scheduler ?? AssetManagerUiDependencies.Scheduler;
            History = new AssetItemGridHistory();
        }

        public event Action ContentChanged;

        public event Action LayoutChanged;

        public event Action<int> HistoryOverlayMaximumItemsChanged;

        public event Action<MainViewLoadResult> LoadCompleted;

        public event Action<string> NavigationChanged;

        public AssetItemGridHistory History { get; }

        public int ItemsPerRow
        {
            get { return GetItemsPerRow(); }
        }

        public int HistoryOverlayMaximumItems
        {
            get { return GetHistoryOverlayMaximumItems(); }
        }

        public AssetManagerViewItemState[] NavigationItems
        {
            get { return AssetManagerNavigationCatalog.Items; }
        }

        public string SelectedNavigationItemId
        {
            get { return _selectedNavigationItemId; }
        }

        public AssetManagerViewItemState SelectedNavigationItem
        {
            get { return AssetManagerNavigationCatalog.GetItem(_selectedNavigationItemId); }
        }

        public void SetItemsPerRow(int value)
        {
            _preferences.ItemsPerRow = value;
        }

        public void SetSelectedNavigationItem(string itemId)
        {
            var resolvedId = AssetManagerNavigationCatalog.NormalizeItemId(itemId);
            if (string.Equals(_selectedNavigationItemId, resolvedId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedNavigationItemId = resolvedId;
            NavigationChanged?.Invoke(_selectedNavigationItemId);
        }

        public void Activate()
        {
            _activationCount++;
            if (_activationCount != 1)
            {
                return;
            }

            _assetManager.Changed += OnAssetManagerChanged;
            _preferences.Changed += OnSettingChanged;
        }

        public void Deactivate()
        {
            if (_activationCount == 0)
            {
                return;
            }

            _activationCount--;
            if (_activationCount != 0)
            {
                return;
            }

            _assetManager.Changed -= OnAssetManagerChanged;
            _preferences.Changed -= OnSettingChanged;
        }

        public void Dispose()
        {
            CancelPendingLoad();
            _activationCount = 1;
            Deactivate();
            _itemCache.Clear();
        }

        public void StartLoad(
            string cacheKey,
            Func<CancellationToken, AssetItemGridList> load)
        {
            if (load == null)
            {
                throw new ArgumentNullException(nameof(load));
            }

            _preferences.Preload();
            CancelPendingLoad();
            var loadCancellation = new CancellationTokenSource();
            _loadCancellation = loadCancellation;
            var cancellationToken = loadCancellation.Token;
            var version = ++_loadVersion;

            _scheduler.RunInBackground(load, cancellationToken, result =>
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

                LoadCompleted?.Invoke(new MainViewLoadResult(
                    cacheKey,
                    result.Succeeded ? result.Value : null,
                    result.Error,
                    result.Canceled));
            });
        }

        public void CancelPendingLoad()
        {
            _loadVersion++;
            if (_loadCancellation == null)
            {
                return;
            }

            _loadCancellation.Cancel();
            _loadCancellation = null;
        }

        public bool TryGetCachedItems(string cacheKey, out AssetItemGridList itemList)
        {
            return _itemCache.TryGetValue(cacheKey ?? string.Empty, out itemList);
        }

        public void StoreCachedItems(string cacheKey, AssetItemGridList itemList)
        {
            _itemCache[cacheKey ?? string.Empty] = itemList ?? new AssetItemGridList(null);
        }

        public void ClearCachedItems()
        {
            _itemCache.Clear();
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
            return _contentVersion + "|" + viewId + "|" + keyword + "|" + limit;
        }

        public AssetItemGridList LoadItems(MainViewRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            var query = CreateQuery(request);
            var result = _assetManager.SearchItemSummaries(query);
            var items = new List<AssetItemGridListItem>();
            if (result == null || result.Items == null)
            {
                return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noItems"));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var thumbnails = _assetManager.GetThumbnails(result.Items
                .Where(item => item != null)
                .Select(item => item.Id)
                .ToArray());
            for (var i = 0; i < result.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = result.Items[i];
                if (item == null)
                {
                    continue;
                }

                AssetThumbnail thumbnail;
                thumbnails.TryGetValue(item.Id, out thumbnail);
                items.Add(new AssetItemGridListItem(item.Id, item.Name, CreateThumbnailState(thumbnail)));
            }

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noItems"));
        }

        public AssetItemGridList LoadFiles(string itemId)
        {
            var files = _assetManager.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
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

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noFiles"));
        }

        public AssetItemGridList LoadItemChildren(string itemId)
        {
            var items = new List<AssetItemGridListItem>();
            var variants = _assetManager.GetVariantGroups(itemId);
            for (var i = 0; i < variants.Count; i++)
            {
                items.Add(CreateGroupListItem(AssetItemGridNodeKind.VariantGroup, variants[i].Id, variants[i].Name, itemId));
            }

            var versions = _assetManager.GetVersionGroups(itemId);
            for (var i = 0; i < versions.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(versions[i].VariantGroupId))
                {
                    items.Add(CreateGroupListItem(AssetItemGridNodeKind.VersionGroup, versions[i].Id, versions[i].Name, itemId));
                }
            }

            var files = _assetManager.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            AddFiles(items, files, itemId, file => !string.IsNullOrWhiteSpace(file.ItemId));
            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"));
        }

        public AssetItemGridList LoadGroupChildren(string itemId, AssetItemGridNodeKind groupKind, string groupId)
        {
            var items = new List<AssetItemGridListItem>();
            var files = _assetManager.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            if (groupKind == AssetItemGridNodeKind.VariantGroup)
            {
                var versions = _assetManager.GetVersionGroups(itemId);
                for (var i = 0; i < versions.Count; i++)
                {
                    if (string.Equals(versions[i].VariantGroupId, groupId, StringComparison.Ordinal))
                    {
                        items.Add(CreateGroupListItem(AssetItemGridNodeKind.VersionGroup, versions[i].Id, versions[i].Name, itemId));
                    }
                }

                AddFiles(items, files, itemId, file => string.Equals(file.VariantGroupId, groupId, StringComparison.Ordinal));
                return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"));
            }

            if (groupKind == AssetItemGridNodeKind.VersionGroup)
            {
                AddFiles(items, files, itemId, file => string.Equals(file.VersionGroupId, groupId, StringComparison.Ordinal));
            }

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"));
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (change != null && change.Kind == AssetManagerChangeKind.Catalog)
            {
                InvalidateContent();
            }
        }

        private void OnSettingChanged(AssetManagerUiPreference preference)
        {
            if (preference == AssetManagerUiPreference.ItemsPerRow)
            {
                LayoutChanged?.Invoke();
            }
            else if (preference == AssetManagerUiPreference.HistoryOverlayMaximumItems)
            {
                HistoryOverlayMaximumItemsChanged?.Invoke(GetHistoryOverlayMaximumItems());
            }
        }

        private void InvalidateContent()
        {
            _contentVersion++;
            _itemCache.Clear();
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

        private static ItemImageState CreateThumbnailState(AssetThumbnail thumbnail)
        {
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

        private int GetItemsPerRow()
        {
            return _preferences.ItemsPerRow;
        }

        private int GetHistoryOverlayMaximumItems()
        {
            return _preferences.HistoryOverlayMaximumItems;
        }
    }
}
