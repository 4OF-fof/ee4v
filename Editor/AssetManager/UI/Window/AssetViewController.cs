using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetViewController {
        private readonly Stack<NavigationState> _backHistory = new();
        private readonly Stack<NavigationState> _forwardHistory = new();
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
        public event Action<List<object>> ItemsChanged;

        public event Action<AssetMetadata> AssetSelected;
        public event Action<BaseFolder> FolderPreviewSelected;
        public event Action<List<BaseFolder>> FoldersChanged;

        public event Action OnHistoryChanged;
        public event Action<List<(string Name, Ulid Id)>> BreadcrumbsChanged;

        public void SetRootContext(string rootName, Func<AssetMetadata, bool> filter, bool isBoothMode = false,
            bool pushHistory = true) {
            if (pushHistory) PushCurrentStateToBackHistory();
            _forwardHistory.Clear();

            _rootPathName = rootName;
            _isBoothMode = isBoothMode;
            _filter = filter ?? (asset => !asset.IsDeleted);
            _selectedFolderId = Ulid.Empty;

            OnHistoryChanged?.Invoke();
            Refresh();
        }

        public void SetRootContextAndFolder(string rootName, Func<AssetMetadata, bool> filter, Ulid folderId,
            bool pushHistory = true) {
            if (pushHistory) PushCurrentStateToBackHistory();
            _forwardHistory.Clear();

            _rootPathName = rootName;
            _isBoothMode = false;
            _filter = filter ?? (asset => !asset.IsDeleted);
            _selectedFolderId = folderId;

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

        public void SelectFolder(Ulid folderId, bool pushHistory = true) {
            if (_selectedFolderId == folderId) return;

            if (pushHistory) PushCurrentStateToBackHistory();
            _forwardHistory.Clear();

            _selectedFolderId = folderId;
            Refresh();
            OnHistoryChanged?.Invoke();
        }

        public void GoBack() {
            if (!CanGoBack) return;

            var currentState = CreateCurrentState();
            _forwardHistory.Push(currentState);

            var prevState = _backHistory.Pop();
            RestoreState(prevState);
        }

        public void GoForward() {
            if (!CanGoForward) return;

            var currentState = CreateCurrentState();
            _backHistory.Push(currentState);

            var nextState = _forwardHistory.Pop();
            RestoreState(nextState);
        }

        private void PushCurrentStateToBackHistory() {
            _backHistory.Push(CreateCurrentState());
        }

        private NavigationState CreateCurrentState() {
            return new NavigationState {
                RootPathName = _rootPathName,
                IsBoothMode = _isBoothMode,
                SelectedFolderId = _selectedFolderId,
                Filter = _filter
            };
        }

        private void RestoreState(NavigationState state) {
            _rootPathName = state.RootPathName;
            _isBoothMode = state.IsBoothMode;
            _selectedFolderId = state.SelectedFolderId;
            _filter = state.Filter;

            Refresh();
            OnHistoryChanged?.Invoke();
        }

        private void ShowBoothItemFolders() {
            var boothItemFolders = new List<BoothItemFolder>();
            var libMetadata = _repository.GetLibraryMetadata();
            var rootFolders = libMetadata?.FolderList ?? new List<BaseFolder>();
            CollectBoothItemFolders(rootFolders, boothItemFolders);
            BoothItemFoldersChanged?.Invoke(boothItemFolders);
            ItemsChanged?.Invoke(new List<object>(boothItemFolders));
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
            var displayItems = new List<object>();

            if (_isBoothMode && _selectedFolderId == Ulid.Empty) {
                ShowBoothItemFolders();
            }
            else {
                var libMetadata = _repository.GetLibraryMetadata();

                if (_selectedFolderId == Ulid.Empty) {
                    if (_rootPathName == "Folders" && libMetadata != null)
                        displayItems.AddRange(libMetadata.FolderList.Where(f => !(f is BoothItemFolder)));
                }
                else {
                    var currentFolder = libMetadata?.GetFolder(_selectedFolderId);
                    if (currentFolder is Folder f) displayItems.AddRange(f.Children);
                }

                var allAssets = _repository.GetAllAssets();
                IEnumerable<AssetMetadata> assetsSource;

                if (_selectedFolderId == Ulid.Empty)
                    assetsSource = _rootPathName == "Folders" ? Enumerable.Empty<AssetMetadata>() : allAssets;
                else
                    assetsSource = allAssets.Where(a => a.Folder == _selectedFolderId);

                var filtered = assetsSource.Where(a => _filter(a)).ToList();
                displayItems.AddRange(filtered);

                AssetsChanged?.Invoke(filtered);
                ItemsChanged?.Invoke(displayItems);
            }

            var folders = _repository.GetLibraryMetadata()?.FolderList.Where(f => !(f is BoothItemFolder)).ToList() ??
                new List<BaseFolder>();
            FoldersChanged?.Invoke(folders);

            UpdateBreadcrumbs();
        }

        private void UpdateBreadcrumbs() {
            var breadcrumbs = new List<(string Name, Ulid Id)>();
            breadcrumbs.Add((_rootPathName, Ulid.Empty));

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

        private static bool FindPathRecursive(IReadOnlyList<BaseFolder> currentLevel, Ulid targetId,
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

        private struct NavigationState {
            public string RootPathName;
            public bool IsBoothMode;
            public Ulid SelectedFolderId;
            public Func<AssetMetadata, bool> Filter;
        }
    }
}