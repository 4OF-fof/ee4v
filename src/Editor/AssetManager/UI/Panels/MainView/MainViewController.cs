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
        public MainViewLoadResult(
            string contentKey,
            AssetItemGridList items,
            Exception error,
            bool canceled,
            bool silent)
        {
            ContentKey = contentKey ?? string.Empty;
            Items = items;
            Error = error;
            Canceled = canceled;
            Silent = silent;
        }

        public string ContentKey { get; }

        public AssetItemGridList Items { get; }

        public Exception Error { get; }

        public bool Canceled { get; }

        public bool Silent { get; }
    }

    internal sealed class TagListLoadResult
    {
        public TagListLoadResult(
            string cacheKey,
            IReadOnlyList<AssetTag> tags,
            Exception error,
            bool canceled)
        {
            CacheKey = cacheKey ?? string.Empty;
            Tags = tags ?? Array.Empty<AssetTag>();
            Error = error;
            Canceled = canceled;
        }

        public string CacheKey { get; }

        public IReadOnlyList<AssetTag> Tags { get; }

        public Exception Error { get; }

        public bool Canceled { get; }
    }

    internal interface IMainViewCatalogReader
    {
        bool TrySearch(
            AssetItemQuery query,
            out AssetSearchResult result);

        bool TryGetThumbnails(
            IReadOnlyList<string> itemIds,
            out IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails);
    }

    internal sealed class MainViewCatalogReader :
        IMainViewCatalogReader
    {
        private readonly IAssetManager _assetManager;

        internal MainViewCatalogReader(
            IAssetManager assetManager)
        {
            _assetManager = assetManager ??
                            throw new ArgumentNullException(
                                nameof(assetManager));
        }

        public bool TrySearch(
            AssetItemQuery query,
            out AssetSearchResult result)
        {
            result =
                _assetManager.SearchItemSummaries(query);
            return true;
        }

        public bool TryGetThumbnails(
            IReadOnlyList<string> itemIds,
            out IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails)
        {
            thumbnails =
                _assetManager.GetThumbnails(itemIds);
            return true;
        }
    }

    internal sealed class MainViewSnapshotCatalogReader :
        IMainViewCatalogReader
    {
        private readonly IAssetManagerSnapshotReader _snapshot;

        internal MainViewSnapshotCatalogReader(
            IAssetManagerSnapshotReader snapshot)
        {
            _snapshot = snapshot ??
                        throw new ArgumentNullException(
                            nameof(snapshot));
        }

        public bool TrySearch(
            AssetItemQuery query,
            out AssetSearchResult result) =>
            _snapshot.TrySearchItemSummaries(
                query,
                out result);

        public bool TryGetThumbnails(
            IReadOnlyList<string> itemIds,
            out IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails) =>
            _snapshot.TryGetThumbnails(
                itemIds,
                out thumbnails);
    }

    internal sealed class MainViewController : IDisposable
    {
        private readonly IAssetManager _assetManager;
        private readonly IMainViewCatalogReader _catalogReader;
        private readonly IMainViewCatalogReader
            _snapshotCatalogReader;
        private readonly IAssetManagerUiPreferences _preferences;
        private readonly IAssetManagerUiScheduler _scheduler;
        private readonly Dictionary<string, AssetItemGridList>
            _childItemCache =
            new Dictionary<string, AssetItemGridList>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<AssetTag>> _tagCache =
            new Dictionary<string, IReadOnlyList<AssetTag>>(StringComparer.Ordinal);
        private readonly Dictionary<string, AssetCollection> _collectionsByViewId =
            new Dictionary<string, AssetCollection>(StringComparer.Ordinal);
        private IReadOnlyList<AssetCollection> _collections =
            Array.Empty<AssetCollection>();
        private int _contentVersion;
        private int _activationCount;
        private int _loadVersion;
        private int _itemsPerRow;
        private int _minimumItemsPerRow;
        private CancellationTokenSource _loadCancellation;
        private string _selectedNavigationItemId = AssetManagerNavigationCatalog.DefaultItemId;
        private bool _hasPendingCollectionNavigation;

        public MainViewController(
            IAssetManager assetManager = null,
            IAssetManagerUiPreferences preferences = null,
            IAssetManagerUiScheduler scheduler = null)
        {
            _assetManager = assetManager ?? AssetManagerUiDependencies.AssetManager;
            _catalogReader =
                new MainViewCatalogReader(_assetManager);
            var snapshotReader =
                _assetManager as IAssetManagerSnapshotReader;
            _snapshotCatalogReader =
                snapshotReader != null
                    ? new MainViewSnapshotCatalogReader(
                        snapshotReader)
                    : null;
            _preferences = preferences ?? AssetManagerUiDependencies.Preferences;
            _scheduler = scheduler ?? AssetManagerUiDependencies.Scheduler;
            _preferences.Preload();
            _minimumItemsPerRow = _preferences.MinimumItemsPerRow;
            _itemsPerRow = ClampItemsPerRow(_preferences.DefaultItemsPerRow);
            History = new AssetItemGridHistory();
        }

        public event Action ContentChanged;

        public event Action LayoutChanged;

        public event Action<int> MinimumItemsPerRowChanged;

        public event Action<int> HistoryOverlayMaximumItemsChanged;

        public event Action<MainViewLoadResult> LoadCompleted;

        public event Action<TagListLoadResult> TagListLoadCompleted;

        public event Action<string> NavigationChanged;

        public event Action CollectionPresentationChanged;

        public AssetItemGridHistory History { get; }

        public int ItemsPerRow
        {
            get { return _itemsPerRow; }
        }

        public int MinimumItemsPerRow
        {
            get { return _minimumItemsPerRow; }
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
            get
            {
                AssetCollection collection;
                if (_collectionsByViewId.TryGetValue(
                        _selectedNavigationItemId,
                        out collection))
                {
                    return CreateCollectionNavigationItem(
                        _selectedNavigationItemId,
                        collection.Name);
                }

                string pendingCollectionId;
                if (AssetManagerCollectionViewId.TryDecode(
                        _selectedNavigationItemId,
                        out pendingCollectionId))
                {
                    return CreateCollectionNavigationItem(
                        _selectedNavigationItemId,
                        pendingCollectionId);
                }

                return AssetManagerNavigationCatalog.GetItem(
                    _selectedNavigationItemId);
            }
        }

        internal static AssetManagerViewItemState
            CreateCollectionNavigationItem(
                string viewId,
                string label)
        {
            var resolvedLabel = label ?? string.Empty;
            return new AssetManagerViewItemState(
                viewId ?? string.Empty,
                resolvedLabel,
                string.Empty,
                string.Empty,
                resolvedLabel,
                string.Empty,
                Array.Empty<string>());
        }

        public IReadOnlyList<AssetItemGridHistoryView>
            CreateHistoryViewPath()
        {
            var selectedItem = SelectedNavigationItem;
            return CreateHistoryViewPath(
                _collections,
                selectedItem.Id,
                selectedItem.Label);
        }

        public void SetItemsPerRow(int value)
        {
            var nextValue = ClampItemsPerRow(value);
            if (_itemsPerRow == nextValue)
            {
                return;
            }

            _itemsPerRow = nextValue;
            LayoutChanged?.Invoke();
        }

        public void SetMinimumItemsPerRow(int value)
        {
            var nextMinimum = Math.Min(
                _preferences.MaximumItemsPerRow,
                Math.Max(_preferences.MinimumItemsPerRow, value));
            if (_minimumItemsPerRow == nextMinimum)
            {
                return;
            }

            _minimumItemsPerRow = nextMinimum;
            MinimumItemsPerRowChanged?.Invoke(_minimumItemsPerRow);
            SetItemsPerRow(_itemsPerRow);
        }

        public void SetSelectedNavigationItem(string itemId)
        {
            var requestedId = itemId ?? string.Empty;
            var collectionExists =
                _collectionsByViewId.ContainsKey(requestedId);
            string requestedCollectionId;
            _hasPendingCollectionNavigation =
                !collectionExists &&
                AssetManagerCollectionViewId.TryDecode(
                    requestedId,
                    out requestedCollectionId);
            var resolvedId = ResolveNavigationItemId(
                requestedId,
                collectionExists);
            if (string.Equals(_selectedNavigationItemId, resolvedId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedNavigationItemId = resolvedId;
            NavigationChanged?.Invoke(_selectedNavigationItemId);
        }

        internal static string ResolveNavigationItemId(
            string itemId,
            bool collectionExists)
        {
            var requestedId = itemId ?? string.Empty;
            string collectionId;
            if (collectionExists ||
                AssetManagerCollectionViewId.TryDecode(
                    requestedId,
                    out collectionId))
            {
                return requestedId;
            }

            return AssetManagerNavigationCatalog.NormalizeItemId(
                requestedId);
        }

        public void SetCollections(IReadOnlyList<AssetCollection> collections)
        {
            _collections = (collections ??
                            Array.Empty<AssetCollection>())
                .Where(collection =>
                    collection != null &&
                    !string.IsNullOrWhiteSpace(collection.Id))
                .ToArray();
            _collectionsByViewId.Clear();
            for (var i = 0; i < _collections.Count; i++)
            {
                var collection = _collections[i];
                _collectionsByViewId[
                    AssetManagerCollectionViewId.Encode(collection.Id)] =
                    collection;
            }

            var navigationChanged = false;
            string selectedCollectionId;
            if (AssetManagerCollectionViewId.TryDecode(
                    _selectedNavigationItemId,
                    out selectedCollectionId) &&
                !_collectionsByViewId.ContainsKey(_selectedNavigationItemId))
            {
                if (!_hasPendingCollectionNavigation)
                {
                    _selectedNavigationItemId =
                        AssetManagerNavigationCatalog.DefaultItemId;
                    navigationChanged = true;
                    NavigationChanged?.Invoke(
                        _selectedNavigationItemId);
                }
            }
            else if (_hasPendingCollectionNavigation)
            {
                _hasPendingCollectionNavigation = false;
            }

            if (!navigationChanged && _activationCount > 0)
            {
                CollectionPresentationChanged?.Invoke();
            }
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
            _childItemCache.Clear();
            _tagCache.Clear();
        }

        public void StartLoad(
            string contentKey,
            Func<CancellationToken, AssetItemGridList> load,
            bool silent = false)
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
                    contentKey,
                    result.Succeeded ? result.Value : null,
                    result.Error,
                    result.Canceled,
                    silent));
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

        public void StartTagListLoad(
            string cacheKey,
            Func<CancellationToken, IReadOnlyList<AssetTag>> load)
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

                TagListLoadCompleted?.Invoke(new TagListLoadResult(
                    cacheKey,
                    result.Succeeded ? result.Value : null,
                    result.Error,
                    result.Canceled));
            });
        }

        public bool TryGetCachedChildren(
            string cacheKey,
            out AssetItemGridList itemList)
        {
            return _childItemCache.TryGetValue(
                cacheKey ?? string.Empty,
                out itemList);
        }

        public void StoreCachedChildren(
            string cacheKey,
            AssetItemGridList itemList)
        {
            _childItemCache[cacheKey ?? string.Empty] =
                itemList ?? new AssetItemGridList(null);
        }

        public bool TryGetCachedTags(string cacheKey, out IReadOnlyList<AssetTag> tags)
        {
            return _tagCache.TryGetValue(cacheKey ?? string.Empty, out tags);
        }

        public void StoreCachedTags(string cacheKey, IReadOnlyList<AssetTag> tags)
        {
            _tagCache[cacheKey ?? string.Empty] = tags ?? Array.Empty<AssetTag>();
        }

        public void ClearCachedTags()
        {
            _tagCache.Clear();
        }

        public MainViewRequest CreateRequest(string viewId, string keyword = null)
        {
            return new MainViewRequest(viewId, keyword);
        }

        public string CreateContentKey(MainViewRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var keyword = request != null ? request.Keyword : string.Empty;
            var limit = request != null ? request.Limit : 200;
            return _contentVersion + "|" + viewId + "|" + keyword + "|" + limit;
        }

        public AssetItemGridList LoadItems(MainViewRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            AssetItemGridList itemList;
            bool thumbnailsReady;
            if (!TryLoadItems(
                    _catalogReader,
                    request,
                    cancellationToken,
                    out itemList,
                    out thumbnailsReady))
            {
                throw new InvalidOperationException(
                    "The catalog reader did not return data.");
            }

            return itemList;
        }

        public bool TryLoadItemsImmediately(
            MainViewRequest request,
            out AssetItemGridList itemList,
            out bool requiresThumbnailLoad)
        {
            itemList = null;
            requiresThumbnailLoad = false;
            if (_snapshotCatalogReader == null)
            {
                return false;
            }

            return TryLoadItems(
                _snapshotCatalogReader,
                request,
                CancellationToken.None,
                out itemList,
                out requiresThumbnailLoad);
        }

        private bool TryLoadItems(
            IMainViewCatalogReader reader,
            MainViewRequest request,
            CancellationToken cancellationToken,
            out AssetItemGridList itemList,
            out bool requiresThumbnailLoad)
        {
            itemList = null;
            requiresThumbnailLoad = false;
            cancellationToken.ThrowIfCancellationRequested();
            AssetSearchResult result;
            if (!reader.TrySearch(
                    CreateQuery(request),
                    out result))
            {
                return false;
            }

            var resultItems =
                result != null && result.Items != null
                    ? result.Items
                    : Array.Empty<AssetItem>();
            IReadOnlyDictionary<
                string,
                IReadOnlyList<AssetItem>>
                previewItemsByCollection;
            if (!TryLoadCollectionPreviewItems(
                    reader,
                    request,
                    cancellationToken,
                    out previewItemsByCollection))
            {
                return false;
            }

            IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails;
            var thumbnailsReady =
                reader.TryGetThumbnails(
                    CollectItemIds(
                        resultItems,
                        previewItemsByCollection),
                    out thumbnails);
            itemList = CreateItemList(
                resultItems,
                previewItemsByCollection,
                thumbnails,
                cancellationToken);
            requiresThumbnailLoad = !thumbnailsReady;
            return true;
        }

        private AssetItemGridList CreateItemList(
            IReadOnlyList<AssetItem> resultItems,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<AssetItem>>
                previewItemsByCollection,
            IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails,
            CancellationToken cancellationToken)
        {
            var items = new List<AssetItemGridListItem>();
            for (var i = 0; i < resultItems.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = resultItems[i];
                if (item == null)
                {
                    continue;
                }

                AssetThumbnail thumbnail;
                thumbnails.TryGetValue(item.Id, out thumbnail);
                items.Add(new AssetItemGridListItem(item.Id, item.Name, CreateThumbnailState(thumbnail)));
            }

            var collectionPreviewStates =
                CreateCollectionPreviewStates(
                    previewItemsByCollection,
                    thumbnails,
                    cancellationToken);
            return new AssetItemGridList(
                items,
                I18N.Get("assetManager.mainView.noItems"),
                collectionPreviewStates);
        }

        public AssetItemGridList CreateDisplayItems(
            MainViewRequest request,
            AssetItemGridList itemList)
        {
            var source = itemList ?? new AssetItemGridList(null);
            string collectionId;
            if (!AssetManagerCollectionViewId.TryDecode(
                    request != null ? request.ViewId : string.Empty,
                    out collectionId))
            {
                return source;
            }

            var children = GetChildCollections(
                _collections,
                collectionId,
                request != null ? request.Keyword : string.Empty);
            if (children.Count == 0)
            {
                return source;
            }

            var items = new List<AssetItemGridListItem>(
                children.Count + source.Items.Count);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                IReadOnlyList<ItemCardState> previewStates;
                source.CollectionPreviewStates.TryGetValue(
                    child.Id,
                    out previewStates);
                var collectionIcon =
                    AssetCollectionIconPresenter.CreateState(
                        child,
                        44f);
                var artwork =
                    CreateCollectionArtworkState(
                        previewStates,
                        collectionIcon);
                items.Add(new AssetItemGridListItem(
                    AssetItemGridNodeKey.Encode(
                        AssetItemGridNodeKind.Collection,
                        child.Id),
                    child.Name,
                    artwork.ImageState,
                    artwork.IconState,
                    string.Empty,
                    artwork.StackStates,
                    collectionIcon));
            }

            items.AddRange(source.Items);
            return new AssetItemGridList(
                items,
                source.EmptyText,
                source.CollectionPreviewStates);
        }

        private bool TryLoadCollectionPreviewItems(
            IMainViewCatalogReader reader,
            MainViewRequest request,
            CancellationToken cancellationToken,
            out IReadOnlyDictionary<
                string,
                IReadOnlyList<AssetItem>>
                previewItemsByCollection)
        {
            var previews =
                new Dictionary<
                    string,
                    IReadOnlyList<AssetItem>>(
                    StringComparer.Ordinal);
            var children =
                GetPreviewCollections(request);
            for (var i = 0; i < children.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var child = children[i];
                AssetSearchResult result;
                if (!reader.TrySearch(
                        new AssetItemQuery
                        {
                            CollectionId = child.Id,
                            Limit = 3
                        },
                        out result))
                {
                    previewItemsByCollection = null;
                    return false;
                }

                previews[child.Id] =
                    result != null && result.Items != null
                        ? result.Items
                        : Array.Empty<AssetItem>();
            }

            previewItemsByCollection = previews;
            return true;
        }

        private IReadOnlyList<AssetCollection>
            GetPreviewCollections(MainViewRequest request)
        {
            string collectionId;
            if (!AssetManagerCollectionViewId.TryDecode(
                    request != null
                        ? request.ViewId
                        : string.Empty,
                    out collectionId))
            {
                return Array.Empty<AssetCollection>();
            }

            return GetChildCollections(
                _collections,
                collectionId,
                request != null
                    ? request.Keyword
                    : string.Empty);
        }

        private static IReadOnlyList<string> CollectItemIds(
            IReadOnlyList<AssetItem> resultItems,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<AssetItem>>
                previewItemsByCollection)
        {
            var itemIds =
                new HashSet<string>(StringComparer.Ordinal);
            AddItemIds(itemIds, resultItems);
            foreach (var pair in previewItemsByCollection)
            {
                AddItemIds(itemIds, pair.Value);
            }

            return itemIds.ToArray();
        }

        private static void AddItemIds(
            ISet<string> itemIds,
            IReadOnlyList<AssetItem> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null &&
                    !string.IsNullOrWhiteSpace(item.Id))
                {
                    itemIds.Add(item.Id);
                }
            }
        }

        private static IReadOnlyDictionary<
                string,
                IReadOnlyList<ItemCardState>>
            CreateCollectionPreviewStates(
                IReadOnlyDictionary<
                    string,
                    IReadOnlyList<AssetItem>>
                    previewItemsByCollection,
                IReadOnlyDictionary<string, AssetThumbnail>
                    thumbnails,
                CancellationToken cancellationToken)
        {
            var previews =
                new Dictionary<
                    string,
                    IReadOnlyList<ItemCardState>>(
                    StringComparer.Ordinal);
            foreach (var pair in previewItemsByCollection)
            {
                var previewItems = pair.Value;
                var states = new List<ItemCardState>(
                    previewItems.Count);
                for (var itemIndex = 0;
                     itemIndex < previewItems.Count;
                     itemIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = previewItems[itemIndex];
                    if (item == null)
                    {
                        continue;
                    }

                    AssetThumbnail thumbnail;
                    thumbnails.TryGetValue(
                        item.Id,
                        out thumbnail);
                    states.Add(new ItemCardState(
                        item.Id,
                        item.Name,
                        CreateThumbnailState(
                            thumbnail)));
                }

                previews[pair.Key] = states;
            }

            return previews;
        }

        internal static AssetItemGridArtworkState
            CreateCollectionArtworkState(
                IReadOnlyList<ItemCardState> contentStates,
                IconState typeIcon)
        {
            var visibleStates =
                new List<ItemCardState>(3);
            if (contentStates != null)
            {
                for (var i = 0;
                     i < contentStates.Count &&
                     visibleStates.Count < 3;
                     i++)
                {
                    var state = contentStates[i];
                    if (state != null &&
                        (state.ImageState.CacheKey.Length >
                         0 ||
                         state.IconState != null))
                    {
                        visibleStates.Add(state);
                    }
                }
            }

            if (visibleStates.Count == 0)
            {
                return new AssetItemGridArtworkState(
                    iconState: typeIcon);
            }

            return new AssetItemGridArtworkState(
                stackStates: visibleStates);
        }

        internal static IReadOnlyList<AssetCollection>
            GetChildCollections(
                IReadOnlyList<AssetCollection> collections,
                string parentCollectionId,
                string keyword)
        {
            var normalizedKeyword = keyword ?? string.Empty;
            return (collections ?? Array.Empty<AssetCollection>())
                .Where(collection =>
                    collection != null &&
                    string.Equals(
                        collection.ParentCollectionId,
                        parentCollectionId,
                        StringComparison.Ordinal) &&
                    (normalizedKeyword.Length == 0 ||
                     (collection.Name ?? string.Empty).IndexOf(
                         normalizedKeyword,
                         StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(collection => collection.SortOrder)
                .ThenBy(
                    collection => collection.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    collection => collection.Id,
                    StringComparer.Ordinal)
                .ToArray();
        }

        internal static IReadOnlyList<AssetItemGridHistoryView>
            CreateHistoryViewPath(
                IReadOnlyList<AssetCollection> collections,
                string viewId,
                string viewLabel)
        {
            string collectionId;
            if (!AssetManagerCollectionViewId.TryDecode(
                    viewId,
                    out collectionId))
            {
                return new[]
                {
                    new AssetItemGridHistoryView(
                        viewId,
                        viewLabel)
                };
            }

            var collectionsById =
                (collections ??
                 Array.Empty<AssetCollection>())
                .Where(collection =>
                    collection != null &&
                    !string.IsNullOrWhiteSpace(collection.Id))
                .GroupBy(
                    collection => collection.Id,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
            var path =
                new List<AssetItemGridHistoryView>();
            var visited =
                new HashSet<string>(StringComparer.Ordinal);
            var currentId = collectionId;
            while (!string.IsNullOrWhiteSpace(currentId) &&
                   visited.Add(currentId))
            {
                AssetCollection collection;
                if (!collectionsById.TryGetValue(
                        currentId,
                        out collection))
                {
                    break;
                }

                path.Add(new AssetItemGridHistoryView(
                    AssetManagerCollectionViewId.Encode(
                        collection.Id),
                    collection.Name));
                currentId = collection.ParentCollectionId;
            }

            if (path.Count == 0)
            {
                return new[]
                {
                    new AssetItemGridHistoryView(
                        viewId,
                        viewLabel)
                };
            }

            path.Reverse();
            return path;
        }

        public IReadOnlyList<AssetTag> LoadTags(
            string keyword,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tags = _assetManager.GetTags(keyword);
            cancellationToken.ThrowIfCancellationRequested();
            return tags ?? Array.Empty<AssetTag>();
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

                var artwork =
                    CreateFileArtworkState(
                        file.Extension);
                items.Add(new AssetItemGridListItem(
                    file.Id,
                    file.FileName,
                    artwork.ImageState,
                    artwork.IconState,
                    itemId,
                    artwork.StackStates,
                    artwork.IconState));
            }

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noFiles"));
        }

        public AssetItemGridList LoadItemChildren(string itemId)
        {
            var items = new List<AssetItemGridListItem>();
            var variants = _assetManager.GetVariantGroups(itemId);
            var versions = _assetManager.GetVersionGroups(itemId);
            var files = _assetManager.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            for (var i = 0; i < variants.Count; i++)
            {
                items.Add(CreateGroupListItem(
                    AssetItemGridNodeKind.VariantGroup,
                    variants[i].Id,
                    variants[i].Name,
                    itemId));
            }

            for (var i = 0; i < versions.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(versions[i].VariantGroupId))
                {
                    items.Add(CreateGroupListItem(
                        AssetItemGridNodeKind.VersionGroup,
                        versions[i].Id,
                        versions[i].Name,
                        itemId));
                }
            }

            AddFiles(
                items,
                files,
                itemId,
                file =>
                    !string.IsNullOrWhiteSpace(
                        file.ItemId));
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
                        items.Add(CreateGroupListItem(
                            AssetItemGridNodeKind.VersionGroup,
                            versions[i].Id,
                            versions[i].Name,
                            itemId));
                    }
                }

                AddFiles(
                    items,
                    files,
                    itemId,
                    file => string.Equals(
                        file.VariantGroupId,
                        groupId,
                        StringComparison.Ordinal));
                return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"));
            }

            if (groupKind == AssetItemGridNodeKind.VersionGroup)
            {
                AddFiles(
                    items,
                    files,
                    itemId,
                    file => string.Equals(
                        file.VersionGroupId,
                        groupId,
                        StringComparison.Ordinal));
            }

            return new AssetItemGridList(items, I18N.Get("assetManager.mainView.noChildren"));
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (change == null)
            {
                return;
            }

            var refreshCurrent =
                change.Kind == AssetManagerChangeKind.Catalog ||
                (change.Kind ==
                 AssetManagerChangeKind.Collections &&
                 string.Equals(
                     _selectedNavigationItemId,
                     "uncategorized",
                     StringComparison.Ordinal)) ||
                ShouldInvalidateForItemCollections(
                    change,
                    _selectedNavigationItemId) ||
                ShouldInvalidateForSmartCollectionRule(
                    change,
                    _selectedNavigationItemId);
            if (refreshCurrent)
            {
                _scheduler.RunOnMainThread(() =>
                {
                    if (_activationCount <= 0)
                    {
                        return;
                    }

                    InvalidateContent();
                });
            }
        }

        internal static bool ShouldInvalidateForItemCollections(
            AssetManagerChange change,
            string selectedNavigationItemId)
        {
            if (change == null ||
                change.Kind !=
                AssetManagerChangeKind.ItemCollections)
            {
                return false;
            }

            if (string.Equals(
                    selectedNavigationItemId,
                    "uncategorized",
                    StringComparison.Ordinal))
            {
                return true;
            }

            string collectionId;
            return AssetManagerCollectionViewId.TryDecode(
                       selectedNavigationItemId,
                       out collectionId) &&
                   string.Equals(
                       collectionId,
                       change.RelatedId,
                       StringComparison.Ordinal);
        }

        internal static bool ShouldInvalidateForSmartCollectionRule(
            AssetManagerChange change,
            string selectedNavigationItemId)
        {
            if (change == null ||
                change.Kind !=
                AssetManagerChangeKind.SmartCollectionRule)
            {
                return false;
            }

            if (string.Equals(
                    selectedNavigationItemId,
                    "uncategorized",
                    StringComparison.Ordinal))
            {
                return true;
            }

            string collectionId;
            return AssetManagerCollectionViewId.TryDecode(
                       selectedNavigationItemId,
                       out collectionId) &&
                   string.Equals(
                       collectionId,
                       change.SubjectId,
                       StringComparison.Ordinal);
        }

        private void OnSettingChanged(AssetManagerUiPreference preference)
        {
            if (preference == AssetManagerUiPreference.HistoryOverlayMaximumItems)
            {
                HistoryOverlayMaximumItemsChanged?.Invoke(GetHistoryOverlayMaximumItems());
            }
        }

        private void InvalidateContent()
        {
            _contentVersion++;
            _childItemCache.Clear();
            _tagCache.Clear();
            ContentChanged?.Invoke();
        }

        internal static AssetItemQuery CreateQuery(MainViewRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var query = new AssetItemQuery
            {
                Keyword = request != null ? request.Keyword : string.Empty,
                Limit = request != null ? request.Limit : 200
            };

            string collectionId;
            if (AssetManagerCollectionViewId.TryDecode(
                    viewId,
                    out collectionId))
            {
                query.CollectionId = collectionId;
            }
            else if (string.Equals(viewId, "booth-items", StringComparison.Ordinal))
            {
                query.HasBoothInformation = true;
            }
            else if (string.Equals(viewId, "uncategorized", StringComparison.Ordinal))
            {
                query.UncategorizedOnly = true;
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
            return FileIconCatalog
                .Resolve(
                    FileEntryKind.File,
                    extension)
                .CreateArtworkIconState();
        }

        private static AssetItemGridListItem CreateGroupListItem(
            AssetItemGridNodeKind kind,
            string id,
            string name,
            string itemId)
        {
            var typeIcon = CreateGroupTypeIcon(kind);
            var artwork = CreateGroupArtworkState(kind);
            return new AssetItemGridListItem(
                AssetItemGridNodeKey.Encode(kind, id),
                name,
                artwork.ImageState,
                artwork.IconState,
                itemId,
                artwork.StackStates,
                typeIcon);
        }

        internal static AssetItemGridArtworkState
            CreateGroupArtworkState(
                AssetItemGridNodeKind kind)
        {
            return new AssetItemGridArtworkState(
                iconState: CreateGroupTypeIcon(kind));
        }

        internal static IconState CreateGroupTypeIcon(
            AssetItemGridNodeKind kind)
        {
            return FileIconCatalog
                .Resolve(
                    kind == AssetItemGridNodeKind.VariantGroup
                        ? FileEntryKind.VariantGroup
                        : FileEntryKind.VersionGroup,
                    string.Empty)
                .CreateArtworkIconState();
        }

        private static void AddFiles(
            ICollection<AssetItemGridListItem> items,
            IReadOnlyList<AssetFile> files,
            string itemId,
            Func<AssetFile, bool> predicate)
        {
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file == null || !predicate(file))
                {
                    continue;
                }

                var artwork =
                    CreateFileArtworkState(
                        file.Extension);
                items.Add(new AssetItemGridListItem(
                    AssetItemGridNodeKey.Encode(AssetItemGridNodeKind.File, file.Id),
                    file.FileName,
                    artwork.ImageState,
                    artwork.IconState,
                    itemId,
                    artwork.StackStates,
                    artwork.IconState));
            }
        }

        internal static AssetItemGridArtworkState
            CreateFileArtworkState(
                string extension)
        {
            return new AssetItemGridArtworkState(
                iconState: CreateFileIcon(extension));
        }

        private int ClampItemsPerRow(int value)
        {
            return Math.Min(
                _preferences.MaximumItemsPerRow,
                Math.Max(_minimumItemsPerRow, value));
        }

        private int GetHistoryOverlayMaximumItems()
        {
            return _preferences.HistoryOverlayMaximumItems;
        }
    }
}
