using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public enum NavigationMode {
        AllItems,
        BoothItems,
        Tag,
        TagList,
        Uncategorized,
        Trash,
        Folders
    }

    public class AssetViewController {
        private readonly Stack<NavigationState> _backHistory = new();
        private readonly Stack<NavigationState> _forwardHistory = new();
        private readonly IAssetRepository _repository;
        private string _contextName = "All Items";
        private NavigationMode _currentMode = NavigationMode.AllItems;

        private string _currentTagFilter;

        private Func<AssetMetadata, bool> _filter = asset => !asset.IsDeleted;
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
        public event Action<NavigationMode> ModeChanged;
        public event Action<List<(string Name, Ulid Id)>> BreadcrumbsChanged;

        public void SetMode(NavigationMode mode, string contextName, Func<AssetMetadata, bool> filter,
            bool pushHistory = true) {
            if (_currentMode == mode && _contextName == contextName && _selectedFolderId == Ulid.Empty)
                return;

            if (pushHistory) PushCurrentStateToBackHistory();
            _forwardHistory.Clear();

            _currentMode = mode;
            _contextName = contextName;
            _filter = filter ?? (asset => !asset.IsDeleted);
            _selectedFolderId = Ulid.Empty;
            _currentTagFilter = null;
            if (mode == NavigationMode.Tag && contextName.StartsWith("Tag: "))
                _currentTagFilter = contextName["Tag: ".Length..];

            ModeChanged?.Invoke(_currentMode);
            OnHistoryChanged?.Invoke();
            Refresh();
        }

        public void SetFolder(Ulid folderId, bool pushHistory = true) {
            if (_selectedFolderId == folderId) return;

            if (pushHistory) PushCurrentStateToBackHistory();
            _forwardHistory.Clear();

            _currentMode = NavigationMode.Folders;
            _contextName = "Folders";
            _selectedFolderId = folderId;
            _filter = asset => !asset.IsDeleted;
            _currentTagFilter = null;

            ModeChanged?.Invoke(_currentMode);
            OnHistoryChanged?.Invoke();
            Refresh();
        }

        public void SelectAsset(AssetMetadata asset) {
            AssetSelected?.Invoke(asset);
        }

        public void PreviewFolder(BaseFolder folder) {
            FolderPreviewSelected?.Invoke(folder);
        }

        public void GoBack() {
            if (!CanGoBack) return;
            _forwardHistory.Push(CreateCurrentState());
            RestoreState(_backHistory.Pop());
        }

        public void GoForward() {
            if (!CanGoForward) return;
            _backHistory.Push(CreateCurrentState());
            RestoreState(_forwardHistory.Pop());
        }

        private void PushCurrentStateToBackHistory() {
            var currentState = CreateCurrentState();

            if (_backHistory.Count > 0) {
                var prevState = _backHistory.Peek();
                if (AreStatesEqual(prevState, currentState)) return;
            }

            _backHistory.Push(currentState);
        }

        private NavigationState CreateCurrentState() {
            return new NavigationState {
                Mode = _currentMode,
                ContextName = _contextName,
                SelectedFolderId = _selectedFolderId,
                Filter = _filter,
                TagFilter = _currentTagFilter
            };
        }

        private static bool AreStatesEqual(NavigationState a, NavigationState b) {
            return a.Mode == b.Mode &&
                a.SelectedFolderId == b.SelectedFolderId &&
                a.ContextName == b.ContextName;
        }

        private void RestoreState(NavigationState state) {
            _currentMode = state.Mode;
            _contextName = state.ContextName;
            _selectedFolderId = state.SelectedFolderId;
            _filter = state.Filter;
            _currentTagFilter = state.TagFilter;

            ModeChanged?.Invoke(_currentMode);
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

            switch (_currentMode) {
                case NavigationMode.BoothItems when _selectedFolderId == Ulid.Empty:
                    ShowBoothItemFolders();
                    break;
                case NavigationMode.TagList: {
                    AssetsChanged?.Invoke(new List<AssetMetadata>());
                    ItemsChanged?.Invoke(new List<object>());

                    UpdateBreadcrumbs();

                    var foldersDummy = _repository.GetLibraryMetadata()?.FolderList.Where(f => f is not BoothItemFolder)
                            .ToList() ??
                        new List<BaseFolder>();
                    FoldersChanged?.Invoke(foldersDummy);
                    return;
                }
                case NavigationMode.AllItems:
                case NavigationMode.Tag:
                case NavigationMode.Uncategorized:
                case NavigationMode.Trash:
                case NavigationMode.Folders:
                default: {
                    var libMetadata = _repository.GetLibraryMetadata();

                    if (_selectedFolderId == Ulid.Empty) {
                        if (_currentMode == NavigationMode.Folders && libMetadata != null)
                            displayItems.AddRange(libMetadata.FolderList.Where(f => f is not BoothItemFolder));
                        else if (_currentMode == NavigationMode.Tag && !string.IsNullOrEmpty(_currentTagFilter))
                            if (libMetadata != null) {
                                var matchingFolders = new List<BaseFolder>();
                                CollectFoldersByTag(libMetadata.FolderList, _currentTagFilter, matchingFolders);
                                displayItems.AddRange(matchingFolders);
                            }
                    }
                    else {
                        var currentFolder = libMetadata?.GetFolder(_selectedFolderId);
                        if (currentFolder is Folder f) displayItems.AddRange(f.Children);
                    }

                    var allAssets = _repository.GetAllAssets();
                    IEnumerable<AssetMetadata> assetsSource;

                    if (_selectedFolderId == Ulid.Empty)
                        assetsSource = _currentMode == NavigationMode.Folders
                            ? Enumerable.Empty<AssetMetadata>()
                            : allAssets;
                    else
                        assetsSource = allAssets.Where(a => a.Folder == _selectedFolderId);

                    var filtered = assetsSource.Where(a => _filter(a)).ToList();
                    displayItems.AddRange(filtered);

                    AssetsChanged?.Invoke(filtered);
                    ItemsChanged?.Invoke(displayItems);
                    break;
                }
            }

            var folders = _repository.GetLibraryMetadata()?.FolderList.Where(f => !(f is BoothItemFolder)).ToList() ??
                new List<BaseFolder>();
            FoldersChanged?.Invoke(folders);

            UpdateBreadcrumbs();
        }

        private static void CollectFoldersByTag(IEnumerable<BaseFolder> folders, string tag, List<BaseFolder> result) {
            foreach (var folder in folders) {
                if (folder.Tags.Contains(tag)) result.Add(folder);
                if (folder is Folder f) CollectFoldersByTag(f.Children, tag, result);
            }
        }

        private void UpdateBreadcrumbs() {
            var breadcrumbs = new List<(string Name, Ulid Id)> { (_contextName, Ulid.Empty) };

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
            public NavigationMode Mode;
            public string ContextName;
            public Ulid SelectedFolderId;
            public Func<AssetMetadata, bool> Filter;
            public string TagFilter;
        }
    }
}