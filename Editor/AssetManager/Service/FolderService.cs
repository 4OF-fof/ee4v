using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    public class FolderService {
        private readonly IAssetRepository _repository;

        public FolderService(IAssetRepository repository) {
            _repository = repository;
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
                    case BackupFolder backupFolder: // 追加: BackupFolder対応
                        libraries.RemoveFolder(folderId);
                        libraries.AddFolder(backupFolder);
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
            _repository.SaveFolder(newFolder.ID);
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

            _repository.SaveFolder(newFolder.ID);
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
            _repository.SaveFolder(folderId);
            return true;
        }

        public void SetFolderDescription(Ulid folderId, string description) {
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);

            if (folder == null) return;
            folder.SetDescription(description);
            _repository.SaveFolder(folderId);
        }

        public void AddTag(Ulid folderId, string tag) {
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);
            if (folder == null) return;

            folder.AddTag(tag);
            _repository.SaveFolder(folderId);
        }

        public void RemoveTag(Ulid folderId, string tag) {
            var libraries = _repository.GetLibraryMetadata();
            var folder = libraries?.GetFolder(folderId);
            if (folder == null) return;

            folder.RemoveTag(tag);
            _repository.SaveFolder(folderId);
        }

        public void DeleteFolder(Ulid folderId) {
            var libraries = _repository.GetLibraryMetadata();
            var targetFolder = libraries?.GetFolder(folderId);
            if (targetFolder == null) return;

            var allDescendantIds = GetSelfAndDescendants(targetFolder);
            var allAssets = _repository.GetAllAssets().ToList();

            var assetsToMarkDeleted = new List<AssetMetadata>();
            foreach (var asset in allAssets.Where(asset => allDescendantIds.Contains(asset.Folder))) {
                var newAsset = new AssetMetadata(asset);
                newAsset.SetDeleted(true);
                assetsToMarkDeleted.Add(newAsset);
            }

            if (assetsToMarkDeleted.Count > 0) _repository.SaveAssets(assetsToMarkDeleted);

            foreach (var fid in allDescendantIds) {
                try {
                    _repository.RemoveFolderThumbnail(fid);
                }
                catch (Exception e) {
                    Debug.LogWarning($"Failed to remove folder thumbnail for {fid}: {e.Message}");
                }

                try {
                    AssetManagerContainer.TextureService?.RemoveFolderFromCache(fid);
                }
                catch {
                    // ignored
                }
            }

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
            // 修正: BoothItemFolderに加え、BackupFolderも通常のフォルダリストから除外する
            return libraries?.FolderList
                .Where(f => f is not BoothItemFolder && f is not BackupFolder)
                .ToList() ?? new List<BaseFolder>();
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

        public Ulid EnsureBoothItemFolder(string shopDomain, string shopName, string identifier,
            string folderName, string folderDescription = null, Ulid parentFolderId = default) {
            var libraries = _repository.GetLibraryMetadata();
            if (libraries == null) return Ulid.Empty;

            foreach (var root in libraries.FolderList) {
                var found = FindBoothItemFolderRecursive(root, shopDomain ?? string.Empty, identifier);
                if (found == null) continue;
                if (!string.IsNullOrEmpty(shopName) && found.ShopName != shopName) found.SetShopName(shopName);
                if (!string.IsNullOrEmpty(folderDescription) && found.Description != folderDescription)
                    found.SetDescription(folderDescription);

                return found.ID;
            }

            var newFolder = new BoothItemFolder();
            var preferredName = !string.IsNullOrEmpty(folderName)
                ? folderName
                : !string.IsNullOrEmpty(identifier)
                    ? identifier
                    : "Booth Item";
            newFolder.SetName(preferredName);
            newFolder.SetDescription(folderDescription ?? shopName ?? string.Empty);
            newFolder.SetShopDomain(shopDomain);
            newFolder.SetShopName(shopName);
            if (!string.IsNullOrEmpty(identifier) && identifier.All(char.IsDigit)) newFolder.SetItemId(identifier);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(newFolder);
            }
            else {
                var parentBase = libraries.GetFolder(parentFolderId);
                if (parentBase is BoothItemFolder || parentBase is not Folder parentFolder) return Ulid.Empty;

                parentFolder.AddChild(newFolder);
            }

            try {
                _repository.SaveLibraryMetadata(libraries);
            }
            catch (Exception e) {
                Debug.LogWarning($"Failed saving library metadata while ensuring booth folder: {e.Message}");
                return Ulid.Empty;
            }

            return newFolder.ID;
        }

        private static BoothItemFolder FindBoothItemFolderRecursive(BaseFolder root, string shopDomain,
            string identifier) {
            switch (root) {
                case null:
                    break;
                case BoothItemFolder bf when !string.IsNullOrEmpty(shopDomain) &&
                    !string.IsNullOrEmpty(bf.ShopDomain) &&
                    bf.ShopDomain != shopDomain:
                    break;
                case BoothItemFolder bf: {
                    if (!string.IsNullOrEmpty(identifier))
                        if ((!string.IsNullOrEmpty(bf.ItemId) && bf.ItemId == identifier) || bf.Name == identifier)
                            return bf;

                    break;
                }
                case Folder { Children: not null } f: {
                    foreach (var c in f.Children) {
                        var found = FindBoothItemFolderRecursive(c, shopDomain, identifier);
                        if (found != null) return found;
                    }

                    break;
                }
            }

            return null;
        }

        public Ulid EnsureBackupFolder(string avatarId, string avatarName) {
            var libraries = _repository.GetLibraryMetadata();
            if (libraries == null) return Ulid.Empty;

            foreach (var root in libraries.FolderList) {
                var found = FindBackupFolderRecursive(root, avatarId);
                if (found == null) continue;
                if (string.IsNullOrEmpty(avatarName) || found.Name == avatarName) return found.ID;
                found.SetName(avatarName);
                _repository.SaveFolder(found.ID);
                return found.ID;
            }

            var newFolder = new BackupFolder();
            newFolder.SetName(!string.IsNullOrEmpty(avatarName) ? avatarName : avatarId);
            newFolder.SetAvatarId(avatarId);
            newFolder.SetDescription($"Backup for {avatarId}");

            libraries.AddFolder(newFolder);

            try {
                _repository.SaveLibraryMetadata(libraries);
            }
            catch (Exception e) {
                Debug.LogWarning($"Failed saving library metadata while ensuring backup folder: {e.Message}");
                return Ulid.Empty;
            }

            return newFolder.ID;
        }

        private static BackupFolder FindBackupFolderRecursive(BaseFolder root, string avatarId) {
            return root switch {
                BackupFolder bf when bf.AvatarId == avatarId => bf,
                Folder { Children: not null } f => f.Children.Select(c => FindBackupFolderRecursive(c, avatarId))
                    .FirstOrDefault(found => found != null),
                _ => null
            };
        }
    }
}