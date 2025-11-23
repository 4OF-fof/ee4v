using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    public class FolderService {
        private readonly AssetService _assetService;
        private readonly IAssetRepository _repository;

        public FolderService(IAssetRepository repository, AssetService assetService) {
            _repository = repository;
            _assetService = assetService;
        }

        public bool CreateFolder(Ulid parentFolderId, string name, string description = null) {
            if (string.IsNullOrWhiteSpace(name)) {
                Debug.LogError("Folder name cannot be empty");
                return false;
            }

            var libraries = _repository.GetLibraryMetadata();
            if (libraries == null) {
                Debug.LogError("Library metadata not available: cannot create folder.");
                return false;
            }

            var folder = new Folder();
            folder.SetName(name);
            folder.SetDescription(description ?? string.Empty);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(folder);
            }
            else {
                var parent = libraries.GetFolder(parentFolderId);
                if (parent is not Folder parentFolder) {
                    Debug.LogError("Cannot create folder: Parent is not a standard folder.");
                    return false;
                }

                parentFolder.AddChild(folder);
            }

            try {
                _repository.SaveLibraryMetadata(libraries);
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to save library metadata when creating folder: {ex.Message}");
                return false;
            }

            return true;
        }

        public void MoveFolder(Ulid folderId, Ulid parentFolderId) {
            if (folderId == default) return;
            var libraries = _repository.GetLibraryMetadata();

            var folderBase = libraries?.GetFolder(folderId);
            if (folderBase == null) return;

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                switch (folderBase) {
                    case BoothItemFolder boothItem:
                        libraries.RemoveFolder(folderId);
                        libraries.AddFolder(boothItem);
                        break;
                    case Folder f:
                        libraries.RemoveFolder(folderId);
                        libraries.AddFolder(f);
                        break;
                }

                _repository.SaveLibraryMetadata(libraries);
                return;
            }

            var newParentBase = libraries.GetFolder(parentFolderId);
            if (newParentBase is not Folder newParentFolder) {
                Debug.LogError("Cannot move: Target parent is not a valid folder.");
                return;
            }

            if (folderBase is Folder movingFolder && IsDescendant(movingFolder, parentFolderId)) {
                Debug.LogError("Cannot move a folder into its own descendant.");
                return;
            }

            libraries.RemoveFolder(folderId);
            newParentFolder.AddChild(folderBase);
            _repository.SaveLibraryMetadata(libraries);
        }

        public bool UpdateFolder(Folder newFolder) {
            if (newFolder == null || !AssetValidationService.IsValidAssetName(newFolder.Name)) {
                Debug.LogError("Failed to update folder: invalid input or name.");
                return false;
            }

            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(newFolder.ID) as Folder;
            if (folder == null) {
                Debug.LogError($"Folder not found: {newFolder.ID}");
                return false;
            }

            folder.SetName(newFolder.Name);
            folder.SetDescription(newFolder.Description);
            _repository.SaveLibraryMetadata(libraries);
            return true;
        }

        public bool UpdateBoothItemFolder(BoothItemFolder newFolder) {
            if (newFolder == null || !AssetValidationService.IsValidAssetName(newFolder.Name)) {
                Debug.LogError("Failed to update booth item folder: invalid input or name.");
                return false;
            }

            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(newFolder.ID) as BoothItemFolder;
            if (folder == null) {
                Debug.LogError($"Booth item folder not found: {newFolder.ID}");
                return false;
            }

            folder.SetName(newFolder.Name);
            folder.SetDescription(newFolder.Description);
            folder.SetShopName(newFolder.ShopName);

            _repository.SaveLibraryMetadata(libraries);
            return true;
        }

        public bool SetFolderName(Ulid folderId, string newName) {
            if (!AssetValidationService.IsValidAssetName(newName)) {
                Debug.LogError("Invalid folder name: cannot set an empty or invalid name.");
                return false;
            }

            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);

            if (folder == null) {
                Debug.LogError($"Folder not found: {folderId}");
                return false;
            }

            folder.SetName(newName);
            _repository.SaveLibraryMetadata(libraries);
            return true;
        }

        public void SetFolderDescription(Ulid folderId, string description) {
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);

            if (folder == null) return;
            folder.SetDescription(description);
            _repository.SaveLibraryMetadata(libraries);
        }

        public void AddTag(Ulid folderId, string tag) {
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);
            if (folder == null) return;

            folder.AddTag(tag);
            _repository.SaveLibraryMetadata(libraries);
        }

        public void RemoveTag(Ulid folderId, string tag) {
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);
            if (folder == null) return;

            folder.RemoveTag(tag);
            _repository.SaveLibraryMetadata(libraries);
        }

        public void DeleteFolder(Ulid folderId) {
            var libraries = _repository.GetLibraryMetadata();
            var targetFolder = libraries?.GetFolder(folderId);
            if (targetFolder == null) return;

            var allDescendantIds = GetSelfAndDescendants(targetFolder);
            var allAssets = _repository.GetAllAssets().ToList();

            foreach (var asset in allAssets)
                if (allDescendantIds.Contains(asset.Folder))
                    _assetService.RemoveAsset(asset.ID);

            libraries.RemoveFolder(folderId);
            _repository.SaveLibraryMetadata(libraries);
        }

        public void SetFolderThumbnail(Ulid folderId, string path) {
            if (string.IsNullOrEmpty(path)) return;
            _repository.SetFolderThumbnail(folderId, path);
        }

        public void RemoveFolderThumbnail(Ulid folderId) {
            if (folderId == Ulid.Empty) return;
            _repository.RemoveFolderThumbnail(folderId);
        }

        private static bool IsDescendant(Folder folder, Ulid targetId) {
            if (folder.ID == targetId) return true;
            foreach (var child in folder.Children) {
                if (child.ID == targetId) return true;
                if (child is Folder childFolder && IsDescendant(childFolder, targetId)) return true;
            }

            return false;
        }

        private static HashSet<Ulid> GetSelfAndDescendants(BaseFolder root) {
            var set = new HashSet<Ulid> { root.ID };
            if (root is not Folder f) return set;
            foreach (var child in f.Children) set.UnionWith(GetSelfAndDescendants(child));
            return set;
        }

        public List<BaseFolder> GetRootFolders() {
            var libraries = _repository.GetLibraryMetadata();
            return libraries?.FolderList.Where(f => f is not BoothItemFolder).ToList() ?? new List<BaseFolder>();
        }

        public void ReorderFolder(Ulid parentFolderId, Ulid folderId, int newIndex) {
            if (folderId == default) return;

            var libraries = _repository.GetLibraryMetadata();

            var folderBase = libraries?.GetFolder(folderId);
            if (folderBase == null) return;

            if (parentFolderId == Ulid.Empty) {
                var currentRoots = libraries.FolderList;
                var fromIndex = -1;
                for (var i = 0; i < currentRoots.Count; i++) {
                    if (currentRoots[i].ID != folderId) continue;
                    fromIndex = i;
                    break;
                }

                libraries.RemoveFolder(folderId);

                if (fromIndex >= 0 && fromIndex < newIndex) newIndex--;

                libraries.InsertRootFolderAt(newIndex, folderBase);
                _repository.SaveLibraryMetadata(libraries);
                return;
            }

            var newParentBase = libraries.GetFolder(parentFolderId);
            if (newParentBase is not Folder newParentFolder) {
                Debug.LogError("Cannot reorder: Target parent is not a valid folder.");
                return;
            }

            if (folderBase is Folder movingFolder && IsDescendant(movingFolder, parentFolderId)) {
                Debug.LogError("Cannot reorder a folder into its own descendant.");
                return;
            }

            if (newParentFolder.ReorderFolder(folderId, newIndex)) {
                _repository.SaveLibraryMetadata(libraries);
                return;
            }

            libraries.RemoveFolder(folderId);
            newParentFolder.InsertChildAt(newIndex, folderBase);
            _repository.SaveLibraryMetadata(libraries);
        }
    }
}