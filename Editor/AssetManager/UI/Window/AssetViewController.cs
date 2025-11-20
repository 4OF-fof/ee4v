using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetViewController {
        private readonly Stack<Ulid> _backHistory = new();
        private readonly Stack<Ulid> _forwardHistory = new();
        private readonly IAssetRepository _repository;
        private Func<AssetMetadata, bool> _filter = asset => !asset.IsDeleted;
        private bool _isBoothMode;

        private string _rootPathName = "All Items";
        private Ulid _selectedFolderId = Ulid.Empty;

        public AssetViewController(IAssetRepository repository) {
            _repository = repository;
        }

        public bool CanGoBack => _backHistory.Count > 0;
        public bool CanGoForward => _forwardHistory.Count > 0;

        public event Action<List<AssetMetadata>> AssetsChanged;
        public event Action<List<BoothItemFolder>> BoothItemFoldersChanged;
        public event Action<AssetMetadata> AssetSelected;
        public event Action<BaseFolder> FolderPreviewSelected;
        public event Action<List<BaseFolder>> FoldersChanged;

        public event Action OnHistoryChanged;
        public event Action<List<(string Name, Ulid Id)>> BreadcrumbsChanged;

        public void SetRootContext(string rootName, bool isBoothMode = false) {
            _rootPathName = rootName;
            _isBoothMode = isBoothMode;

            _backHistory.Clear();
            _forwardHistory.Clear();
            _selectedFolderId = Ulid.Empty;

            OnHistoryChanged?.Invoke();
            Refresh();
        }

        public void SetFilter(Func<AssetMetadata, bool> filter) {
            _filter = filter ?? (asset => !asset.IsDeleted);
            Refresh();
        }

        public void SelectAsset(AssetMetadata asset) {
            AssetSelected?.Invoke(asset);
        }

        public void PreviewFolder(BaseFolder folder) {
            FolderPreviewSelected?.Invoke(folder);
        }

        public void SelectFolder(Ulid folderId) {
            if (_selectedFolderId == folderId) return;

            _backHistory.Push(_selectedFolderId);
            _forwardHistory.Clear();

            InternalSetFolder(folderId);
        }

        public void GoBack() {
            if (!CanGoBack) return;
            var prev = _backHistory.Pop();
            _forwardHistory.Push(_selectedFolderId);
            InternalSetFolder(prev);
        }

        public void GoForward() {
            if (!CanGoForward) return;
            var next = _forwardHistory.Pop();
            _backHistory.Push(_selectedFolderId);
            InternalSetFolder(next);
        }

        private void InternalSetFolder(Ulid folderId) {
            _selectedFolderId = folderId;
            Refresh();
            OnHistoryChanged?.Invoke();
        }

        private void ShowBoothItemFolders() {
            var boothItemFolders = new List<BoothItemFolder>();
            var libMetadata = _repository.GetLibraryMetadata();
            var rootFolders = libMetadata?.FolderList ?? new List<BaseFolder>();
            CollectBoothItemFolders(rootFolders, boothItemFolders);
            BoothItemFoldersChanged?.Invoke(boothItemFolders);
        }

        private static void CollectBoothItemFolders(IEnumerable<BaseFolder> folders, List<BoothItemFolder> result) {
            foreach (var folder in folders)
                switch (folder) {
                    case BoothItemFolder boothItemFolder:
                        result.Add(boothItemFolder);
                        break;
                    case Folder parentFolder:
                        CollectBoothItemFolders(parentFolder.Children, result);
                        break;
                }
        }

        public void Refresh() {
            if (_isBoothMode && _selectedFolderId == Ulid.Empty) {
                ShowBoothItemFolders();
            }
            else {
                var allAssets = _repository.GetAllAssets();
                var assets = _selectedFolderId == Ulid.Empty
                    ? allAssets
                    : allAssets.Where(a => a.Folder == _selectedFolderId);

                var filtered = assets.Where(a => _filter(a)).ToList();
                AssetsChanged?.Invoke(filtered);
            }

            var libMetadata = _repository.GetLibraryMetadata();
            var folders = libMetadata?.FolderList.Where(f => !(f is BoothItemFolder)).ToList() ??
                new List<BaseFolder>();
            FoldersChanged?.Invoke(folders);

            UpdateBreadcrumbs();
        }

        private void UpdateBreadcrumbs() {
            var breadcrumbs = new List<(string Name, Ulid Id)>();
            var showRoot = !(_rootPathName == "Folders" && _selectedFolderId != Ulid.Empty);

            if (showRoot) breadcrumbs.Add((_rootPathName, Ulid.Empty));

            if (_selectedFolderId != Ulid.Empty) {
                var libMetadata = _repository.GetLibraryMetadata();
                if (libMetadata != null) {
                    var pathStack = new Stack<(string Name, Ulid Id)>();
                    if (FindPathRecursive(libMetadata.FolderList, _selectedFolderId, pathStack))
                        while (pathStack.Count > 0)
                            breadcrumbs.Add(pathStack.Pop());
                }
            }

            BreadcrumbsChanged?.Invoke(breadcrumbs);
        }

        private bool FindPathRecursive(IReadOnlyList<BaseFolder> currentLevel, Ulid targetId,
            Stack<(string Name, Ulid Id)> pathStack) {
            foreach (var folder in currentLevel) {
                if (folder.ID == targetId) {
                    pathStack.Push((folder.Name, folder.ID));
                    return true;
                }

                switch (folder) {
                    case Folder parentFolder when FindPathRecursive(parentFolder.Children, targetId, pathStack):
                        pathStack.Push((folder.Name, folder.ID));
                        return true;
                    case BoothItemFolder boothFolder when boothFolder.ID == targetId:
                        pathStack.Push((boothFolder.Name, boothFolder.ID));
                        return true;
                }
            }

            return false;
        }
    }
}