using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;

namespace Ee4v.AssetManager.UI
{
    internal sealed class CollectionNavigationController : IDisposable
    {
        private readonly IAssetManager _assetManager;
        private readonly IAssetManagerUiScheduler _scheduler;
        private CancellationTokenSource _loadCancellation;
        private CancellationTokenSource _createCancellation;
        private CancellationTokenSource _moveCancellation;
        private CancellationTokenSource _mutationCancellation;
        private CancellationTokenSource _itemDropCancellation;
        private int _loadVersion;
        private bool _active;
        private bool _disposed;
        private IReadOnlyList<AssetCollection> _collections =
            Array.Empty<AssetCollection>();

        public CollectionNavigationController(
            IAssetManager assetManager = null,
            IAssetManagerUiScheduler scheduler = null)
        {
            _assetManager =
                assetManager ?? AssetManagerUiDependencies.AssetManager;
            _scheduler =
                scheduler ?? AssetManagerUiDependencies.Scheduler;
        }

        public event Action<IReadOnlyList<AssetCollection>>
            CollectionsChanged;

        public event Action<AssetCollection> CollectionOpenRequested;

        public event Action<string> ErrorChanged;

        public IReadOnlyList<AssetCollection> Collections
        {
            get { return _collections; }
        }

        public void Activate()
        {
            if (_disposed || _active)
            {
                return;
            }

            _active = true;
            _assetManager.Changed += OnAssetManagerChanged;
            Reload();
        }

        public void Deactivate()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            _assetManager.Changed -= OnAssetManagerChanged;
            CancelLoad();
            CancelCreate();
            CancelMove();
            CancelMutation();
            CancelItemDrop();
        }

        public void Reload()
        {
            if (_disposed)
            {
                return;
            }

            CancelLoad();
            var cancellation = new CancellationTokenSource();
            _loadCancellation = cancellation;
            var version = ++_loadVersion;
            _scheduler.RunInBackground(
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    var result = _assetManager.GetCollections();
                    token.ThrowIfCancellationRequested();
                    return result;
                },
                cancellation.Token,
                result =>
                {
                    cancellation.Dispose();
                    if (ReferenceEquals(_loadCancellation, cancellation))
                    {
                        _loadCancellation = null;
                    }

                    if (_disposed || version != _loadVersion)
                    {
                        return;
                    }

                    if (!result.Succeeded)
                    {
                        if (!result.Canceled)
                        {
                            ErrorChanged?.Invoke(
                                result.Error != null
                                    ? FormatError(result.Error)
                                    : string.Empty);
                        }

                        return;
                    }

                    ErrorChanged?.Invoke(string.Empty);
                    SetCollections(result.Value);
                });
        }

        public void CreateCollection(CreateCollectionRequest request)
        {
            Create(() => _assetManager.CreateCollection(request));
        }

        public void CreateSmartCollection(
            CreateSmartCollectionRequest request)
        {
            Create(() => _assetManager.CreateSmartCollection(request));
        }

        public void UpdateCollection(
            string collectionId,
            UpdateCollectionRequest request)
        {
            Mutate(
                () => _assetManager.UpdateCollection(
                    collectionId,
                    request),
                AddOrReplace);
        }

        public void UpdateSmartCollection(
            string collectionId,
            UpdateSmartCollectionRequest request)
        {
            Mutate(
                () => _assetManager.UpdateSmartCollection(
                    collectionId,
                    request),
                AddOrReplace);
        }

        public void DeleteCollections(
            IReadOnlyList<string> collectionIds)
        {
            var requestedIds = (collectionIds ??
                                Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (requestedIds.Length == 0)
            {
                return;
            }

            Mutate(
                () =>
                {
                    _assetManager.DeleteCollections(requestedIds);
                    return (IReadOnlyList<string>)requestedIds;
                },
                RemoveCollections);
        }

        public void MoveCollection(
            string collectionId,
            string parentCollectionId,
            int siblingIndex)
        {
            MoveCollections(
                new[] { collectionId },
                parentCollectionId,
                siblingIndex);
        }

        public void MoveCollections(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex)
        {
            if (_disposed ||
                collectionIds == null ||
                collectionIds.Count == 0 ||
                _moveCancellation != null)
            {
                return;
            }

            ErrorChanged?.Invoke(string.Empty);
            var cancellation = new CancellationTokenSource();
            _moveCancellation = cancellation;
            _scheduler.RunInBackground(
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    _assetManager.MoveCollections(
                        collectionIds,
                        parentCollectionId,
                        siblingIndex);
                    token.ThrowIfCancellationRequested();
                    return true;
                },
                cancellation.Token,
                result =>
                {
                    cancellation.Dispose();
                    if (ReferenceEquals(
                            _moveCancellation,
                            cancellation))
                    {
                        _moveCancellation = null;
                    }

                    if (_disposed || result.Canceled)
                    {
                        return;
                    }

                    if (!result.Succeeded)
                    {
                        ErrorChanged?.Invoke(
                            result.Error != null
                                ? FormatError(result.Error)
                                : string.Empty);
                        Reload();
                        return;
                    }
                });
        }

        public void AddItemsToCollection(
            IReadOnlyList<string> itemIds,
            string collectionId)
        {
            if (_disposed ||
                itemIds == null ||
                itemIds.Count == 0 ||
                string.IsNullOrWhiteSpace(collectionId))
            {
                return;
            }

            ErrorChanged?.Invoke(string.Empty);
            CancelItemDrop();
            var cancellation = new CancellationTokenSource();
            _itemDropCancellation = cancellation;
            _scheduler.RunInBackground(
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    _assetManager.AddItemsToCollection(
                        itemIds,
                        collectionId);
                    token.ThrowIfCancellationRequested();
                    return true;
                },
                cancellation.Token,
                result =>
                {
                    cancellation.Dispose();
                    if (ReferenceEquals(
                            _itemDropCancellation,
                            cancellation))
                    {
                        _itemDropCancellation = null;
                    }

                    if (_disposed || result.Canceled)
                    {
                        return;
                    }

                    if (!result.Succeeded)
                    {
                        ErrorChanged?.Invoke(
                            result.Error != null
                                ? FormatError(result.Error)
                                : string.Empty);
                    }
                });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Deactivate();
            _disposed = true;
        }

        private void Create(Func<AssetCollection> operation)
        {
            if (_disposed || operation == null)
            {
                return;
            }

            ErrorChanged?.Invoke(string.Empty);
            CancelCreate();
            var cancellation = new CancellationTokenSource();
            _createCancellation = cancellation;
            _scheduler.RunInBackground(
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    var result = operation();
                    token.ThrowIfCancellationRequested();
                    return result;
                },
                cancellation.Token,
                result =>
                {
                    cancellation.Dispose();
                    if (ReferenceEquals(_createCancellation, cancellation))
                    {
                        _createCancellation = null;
                    }

                    if (_disposed || result.Canceled)
                    {
                        return;
                    }

                    if (!result.Succeeded)
                    {
                        ErrorChanged?.Invoke(
                            result.Error != null
                                ? FormatError(result.Error)
                                : string.Empty);
                        return;
                    }

                    AddOrReplace(result.Value);
                    if (result.Value != null &&
                        result.Value.IsSmartCollection)
                    {
                        CollectionOpenRequested?.Invoke(
                            result.Value);
                    }
                });
        }

        private void Mutate<T>(
            Func<T> operation,
            Action<T> applyResult)
        {
            if (_disposed || operation == null)
            {
                return;
            }

            ErrorChanged?.Invoke(string.Empty);
            CancelMutation();
            var cancellation = new CancellationTokenSource();
            _mutationCancellation = cancellation;
            _scheduler.RunInBackground(
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    var result = operation();
                    token.ThrowIfCancellationRequested();
                    return result;
                },
                cancellation.Token,
                result =>
                {
                    cancellation.Dispose();
                    if (ReferenceEquals(
                            _mutationCancellation,
                            cancellation))
                    {
                        _mutationCancellation = null;
                    }

                    if (_disposed || result.Canceled)
                    {
                        return;
                    }

                    if (!result.Succeeded)
                    {
                        ErrorChanged?.Invoke(
                            result.Error != null
                                ? FormatError(result.Error)
                                : string.Empty);
                        Reload();
                        return;
                    }

                    applyResult?.Invoke(result.Value);
                });
        }

        private void AddOrReplace(AssetCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            SetCollections(
                _collections
                    .Where(item =>
                        item != null &&
                        !string.Equals(
                            item.Id,
                            collection.Id,
                            StringComparison.Ordinal))
                    .Concat(new[] { collection })
                    .ToArray());
        }

        private void RemoveCollections(
            IReadOnlyList<string> collectionIds)
        {
            if (collectionIds == null ||
                collectionIds.Count == 0)
            {
                return;
            }

            var removedIds = new HashSet<string>(
                collectionIds,
                StringComparer.Ordinal);
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = 0; i < _collections.Count; i++)
                {
                    var collection = _collections[i];
                    if (collection == null ||
                        removedIds.Contains(collection.Id) ||
                        string.IsNullOrWhiteSpace(
                            collection.ParentCollectionId) ||
                        !removedIds.Contains(
                            collection.ParentCollectionId))
                    {
                        continue;
                    }

                    removedIds.Add(collection.Id);
                    changed = true;
                }
            }

            SetCollections(
                _collections
                    .Where(item =>
                        item != null &&
                        !removedIds.Contains(item.Id))
                    .ToArray());
        }

        private void SetCollections(IReadOnlyList<AssetCollection> collections)
        {
            _collections =
                collections ?? Array.Empty<AssetCollection>();
            CollectionsChanged?.Invoke(_collections);
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (change != null &&
                change.Kind == AssetManagerChangeKind.Collections)
            {
                _scheduler.RunOnMainThread(() =>
                {
                    if (_active && !_disposed)
                    {
                        Reload();
                    }
                });
            }
        }

        private void CancelLoad()
        {
            _loadVersion++;
            if (_loadCancellation == null)
            {
                return;
            }

            _loadCancellation.Cancel();
            _loadCancellation = null;
        }

        private void CancelCreate()
        {
            if (_createCancellation == null)
            {
                return;
            }

            _createCancellation.Cancel();
            _createCancellation = null;
        }

        private void CancelMove()
        {
            if (_moveCancellation == null)
            {
                return;
            }

            _moveCancellation.Cancel();
            _moveCancellation = null;
        }

        private void CancelMutation()
        {
            if (_mutationCancellation == null)
            {
                return;
            }

            _mutationCancellation.Cancel();
            _mutationCancellation = null;
        }

        private void CancelItemDrop()
        {
            if (_itemDropCancellation == null)
            {
                return;
            }

            _itemDropCancellation.Cancel();
            _itemDropCancellation = null;
        }

        private static string FormatError(Exception exception)
        {
            var assetManagerException =
                exception as AssetManagerException;
            if (assetManagerException != null &&
                assetManagerException.Code ==
                AssetManagerErrorCode.InvalidCollectionHierarchy)
            {
                return I18N.Get(
                    "assetManager.navigation.collections.error.smartCollectionCannotContainCollections");
            }

            return exception != null
                ? exception.Message
                : string.Empty;
        }
    }
}
