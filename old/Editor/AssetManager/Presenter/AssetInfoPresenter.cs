using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Presenter {
    public class AssetInfoPresenter {
        private readonly IAssetRepository _repository;
        private readonly TextureService _textureService;

        private AssetMetadata _currentAsset;
        private BaseFolder _currentFolder;
        private IReadOnlyList<object> _lastSelection;

        public AssetInfoPresenter(IAssetRepository repository, TextureService textureService) {
            _repository = repository;
            _textureService = textureService;

            if (_repository == null) return;
            _repository.LibraryChanged += OnRepositoryLibraryChanged;
            _repository.AssetChanged += OnRepositoryAssetChanged;
            _repository.FolderChanged += OnRepositoryFolderChanged;
        }

        public event Action<AssetDisplayData> AssetDataUpdated;
        public event Action<FolderDisplayData> FolderDataUpdated;
        public event Action<LibraryDisplayData> LibraryDataUpdated;
        public event Action<int> MultiSelectionUpdated;

        public void Dispose() {
            if (_repository == null) return;
            try {
                _repository.LibraryChanged -= OnRepositoryLibraryChanged;
                _repository.AssetChanged -= OnRepositoryAssetChanged;
                _repository.FolderChanged -= OnRepositoryFolderChanged;
            }
            catch {
                // ignore
            }
        }

        public void UpdateSelection(IReadOnlyList<object> selectedItems) {
            _lastSelection = selectedItems;
            if (selectedItems == null || selectedItems.Count == 0) {
                ShowLibraryInfo();
            }
            else if (selectedItems.Count == 1) {
                var item = selectedItems[0];
                switch (item) {
                    case AssetMetadata asset:
                        SetAsset(asset);
                        break;
                    case BaseFolder folder:
                        SetFolder(folder);
                        break;
                }
            }
            else {
                MultiSelectionUpdated?.Invoke(selectedItems.Count);
            }
        }

        public async void LoadThumbnail(Ulid id, bool isFolder, Action<Texture2D> onLoaded) {
            if (_textureService == null) {
                onLoaded?.Invoke(null);
                return;
            }

            try {
                Texture2D tex;
                if (isFolder) {
                    tex = await _textureService.GetFolderThumbnailAsync(id);
                    if (_currentFolder?.ID != id) return;
                }
                else {
                    tex = await _textureService.GetAssetThumbnailAsync(id);
                    if (_currentAsset?.ID != id) return;
                }

                onLoaded?.Invoke(tex);
            }
            catch {
                onLoaded?.Invoke(null);
            }
        }

        private void OnRepositoryLibraryChanged() {
            _textureService?.ClearCache();
            EditorApplication.delayCall += () =>
            {
                try {
                    if (_lastSelection == null || _lastSelection.Count == 0) {
                        ShowLibraryInfo();
                    }
                    else if (_lastSelection.Count == 1) {
                        var item = _lastSelection[0];
                        switch (item) {
                            case AssetMetadata a:
                                var fresh = _repository?.GetAsset(a.ID);
                                if (fresh != null) SetAsset(fresh);
                                else UpdateSelection(null);
                                break;
                            case BaseFolder f:
                                var freshFolder = _repository?.GetLibraryMetadata()?.GetFolder(f.ID);
                                if (freshFolder != null) SetFolder(freshFolder);
                                else UpdateSelection(null);
                                break;
                        }
                    }
                }
                catch {
                    // ignore
                }
            };
        }

        private void OnRepositoryAssetChanged(Ulid id) {
            _textureService?.RemoveAssetFromCache(id);
            EditorApplication.delayCall += () =>
            {
                try {
                    if (_lastSelection is not { Count: 1 }) return;
                    if (_currentAsset == null || _currentAsset.ID != id) return;
                    var fresh = _repository?.GetAsset(id);
                    if (fresh != null) SetAsset(fresh);
                    else UpdateSelection(null);
                }
                catch {
                    // ignore
                }
            };
        }

        private void OnRepositoryFolderChanged(Ulid id) {
            _textureService?.RemoveFolderFromCache(id);
            EditorApplication.delayCall += () =>
            {
                try {
                    if (_lastSelection is not { Count: 1 }) return;
                    if (_currentFolder == null || _currentFolder.ID != id) return;
                    var freshFolder = _repository?.GetLibraryMetadata()?.GetFolder(id);
                    if (freshFolder != null) SetFolder(freshFolder);
                    else UpdateSelection(null);
                }
                catch {
                    // ignore
                }
            };
        }

        private void ShowLibraryInfo() {
            if (_repository == null) {
                LibraryDataUpdated?.Invoke(new LibraryDisplayData());
                return;
            }

            var allAssets = _repository.GetAllAssets().ToList();
            var totalSize = allAssets.Sum(a => a.Size);

            var data = new LibraryDisplayData {
                TotalAssets = allAssets.Count,
                TotalSize = totalSize,
                TotalTags = _repository.GetAllTags().Count
            };

            LibraryDataUpdated?.Invoke(data);
        }

        private void SetAsset(AssetMetadata asset) {
            _currentAsset = asset;
            _currentFolder = null;

            if (asset == null) {
                ShowLibraryInfo();
                return;
            }

            var folder = _repository?.GetLibraryMetadata()?.GetFolder(asset.Folder);

            var dependencies = GetDependencies(asset);
            var hasPhysicalFile = HasPhysicalFile(asset);

            var data = new AssetDisplayData {
                Id = asset.ID,
                Name = asset.Name,
                Description = asset.Description,
                Tags = asset.Tags,
                Size = asset.Size,
                Extension = asset.Ext.TrimStart('.').ToUpper(),
                ModificationTime = DateTimeOffset.FromUnixTimeMilliseconds(asset.ModificationTime).ToLocalTime(),
                FolderId = asset.Folder,
                FolderName = folder?.Name ?? "-",
                Dependencies = dependencies,
                DownloadUrl = asset.BoothData?.DownloadUrl ?? "",
                HasPhysicalFile = hasPhysicalFile,
                ShopName = asset.BoothData?.ShopDomain ?? "",
                ShopUrl = asset.BoothData?.ShopUrl ?? "",
                ItemId = asset.BoothData?.ItemId ?? "",
                ItemUrl = asset.BoothData?.ItemUrl ?? ""
            };

            AssetDataUpdated?.Invoke(data);
        }

        private void SetFolder(BaseFolder folder) {
            _currentAsset = null;
            _currentFolder = folder;

            if (folder == null) {
                ShowLibraryInfo();
                return;
            }

            var parentFolder = GetParentFolder(folder.ID);
            var subFolderCount = 0;
            if (folder is Folder f) subFolderCount = f.Children?.Count ?? 0;

            var assetCount = 0;
            if (_repository != null)
                assetCount = _repository.GetAllAssets()
                    .Count(a => a.Folder == folder.ID && !a.IsDeleted);

            var isBoothItemFolder = folder is BoothItemFolder;
            string shopName = null;
            string shopUrl = null;
            string itemUrl = null;
            string itemId = null;

            if (folder is BoothItemFolder boothFolder) {
                shopName = boothFolder.ShopName;
                shopUrl = boothFolder.ShopUrl;
                itemUrl = boothFolder.ItemUrl;
                itemId = boothFolder.ItemId;
            }
            else if (folder is BackupFolder backupFolder) {
                itemId = backupFolder.AvatarId;
                itemUrl = $"https://vrchat.com/home/avatar/{backupFolder.AvatarId}";
            }

            var data = new FolderDisplayData {
                Id = folder.ID,
                Name = folder.Name,
                Description = folder.Description,
                Tags = folder.Tags,
                ParentFolderId = parentFolder?.ID ?? Ulid.Empty,
                ParentFolderName = parentFolder?.Name ?? "-",
                SubFolderCount = subFolderCount,
                AssetCount = assetCount,
                ModificationTime = DateTimeOffset.FromUnixTimeMilliseconds(folder.ModificationTime).ToLocalTime(),
                IsFolder = folder is Folder,
                IsBoothItemFolder = isBoothItemFolder,
                ShopName = shopName,
                ShopUrl = shopUrl,
                ItemUrl = itemUrl,
                ItemId = itemId
            };

            FolderDataUpdated?.Invoke(data);
        }

        private BaseFolder GetParentFolder(Ulid childId) {
            var lib = _repository?.GetLibraryMetadata();
            if (lib == null) return null;

            foreach (var root in lib.FolderList) {
                if (root.ID == childId) return null;
                var parentFolder = FindParentRecursive(root, childId);
                if (parentFolder != null) return parentFolder;
            }

            return null;
        }

        private static BaseFolder FindParentRecursive(BaseFolder current, Ulid childId) {
            if (current is not Folder f || f.Children == null) return null;

            foreach (var child in f.Children) {
                if (child.ID == childId) return f;
                var res = FindParentRecursive(child, childId);
                if (res != null) return res;
            }

            return null;
        }

        private List<DependencyDisplayData> GetDependencies(AssetMetadata asset) {
            var result = new List<DependencyDisplayData>();
            if (asset?.UnityData?.DependenceItemList == null || _repository == null) return result;

            result.AddRange(from depId in asset.UnityData.DependenceItemList
                select _repository.GetAsset(depId)
                into depAsset
                where depAsset is {
                    IsDeleted: false
                }
                select new DependencyDisplayData { Id = depAsset.ID, Name = depAsset.Name });

            return result;
        }

        private bool HasPhysicalFile(AssetMetadata asset) {
            if (asset == null) return false;

            return _repository?.HasAssetFile(asset.ID) ?? false;
        }
    }

    public class AssetDisplayData {
        public IReadOnlyList<DependencyDisplayData> Dependencies;
        public string Description;
        public string DownloadUrl;
        public string Extension;
        public Ulid FolderId;
        public string FolderName;
        public bool HasPhysicalFile;
        public Ulid Id;
        public string ItemId;
        public string ItemUrl;
        public DateTimeOffset ModificationTime;
        public string Name;
        public string ShopName;
        public string ShopUrl;
        public long Size;
        public IReadOnlyList<string> Tags;
    }

    public class FolderDisplayData {
        public int AssetCount;
        public string Description;
        public Ulid Id;
        public bool IsBoothItemFolder;
        public bool IsFolder;
        public string ItemId;
        public string ItemUrl;
        public DateTimeOffset ModificationTime;
        public string Name;
        public Ulid ParentFolderId;
        public string ParentFolderName;
        public string ShopName;
        public string ShopUrl;
        public int SubFolderCount;
        public IReadOnlyList<string> Tags;
    }

    public class LibraryDisplayData {
        public int TotalAssets;
        public long TotalSize;
        public int TotalTags;
    }

    public class DependencyDisplayData {
        public Ulid Id;
        public string Name;
    }
}