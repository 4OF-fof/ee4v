using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    internal sealed class CollectionNavigationController : IDisposable
    {
        private readonly IAssetManager _assetManager;
        private readonly IAssetManagerUiScheduler _scheduler;
        private CancellationTokenSource _loadCancellation;
        private CancellationTokenSource _createCancellation;
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

        public event Action<AssetCollection> CollectionCreated;

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
                                    ? result.Error.Message
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
                                ? result.Error.Message
                                : string.Empty);
                        return;
                    }

                    AddOrReplace(result.Value);
                    CollectionCreated?.Invoke(result.Value);
                    Reload();
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

        private void SetCollections(IReadOnlyList<AssetCollection> collections)
        {
            _collections =
                collections ?? Array.Empty<AssetCollection>();
            CollectionsChanged?.Invoke(_collections);
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (change != null &&
                change.Kind == AssetManagerChangeKind.Catalog)
            {
                Reload();
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
    }
}
