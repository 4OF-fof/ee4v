using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Utility;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    public static class AssetLibraryService {
        public static async void LoadAssetLibrary() {
            var cacheLoaded = AssetLibrarySerializer.LoadCache();

            if (!cacheLoaded) {
                AssetLibrarySerializer.LoadLibrary();
                AssetLibrarySerializer.LoadAllAssets();
                AssetLibrarySerializer.SaveCache();
            }
            else {
                await AssetLibrarySerializer.LoadAndVerifyAsync();
            }
        }

        public static void RefreshAssetLibrary() {
            AssetLibrary.Instance.UnloadAssetLibrary();
            LoadAssetLibrary();
        }

        public static void CreateAsset(string path) {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                Debug.LogError($"Invalid path: {path}");
                return;
            }

            try {
                AssetLibrarySerializer.CreateAsset(path);
            }
            catch (Exception e) {
                Debug.LogError($"Failed to add asset from path: {e.Message}");
            }
        }

        public static void DeleteAsset(Ulid assetId) {
            var asset = AssetLibrary.Instance.GetAsset(assetId);
            if (asset == null) {
                Debug.LogError($"Asset with ID {assetId} does not exist.");
                return;
            }

            AssetLibrary.Instance.RemoveAsset(assetId);
            AssetLibrarySerializer.DeleteAsset(assetId);
        }

        public static void UpdateAsset(AssetMetadata newAsset) {
            if (!IsValidAssetName(newAsset.Name)) return;
            if (AssetLibrary.Instance.GetAsset(newAsset.ID) == null) {
                Debug.LogError($"Asset with ID {newAsset.ID} does not exist.");
                return;
            }

            var oldAsset = AssetLibrary.Instance.GetAsset(newAsset.ID);
            if (oldAsset.Name != newAsset.Name) AssetLibrarySerializer.RenameAsset(newAsset.ID, newAsset.Name);
            AssetLibrary.Instance.UpdateAsset(newAsset);
            AssetLibrarySerializer.SaveAsset(newAsset);
        }

        public static void RenameAsset(Ulid assetId, string newName) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetName(newName);
            UpdateAsset(asset);
        }

        public static void SetDescription(Ulid assetId, string newDescription) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetDescription(newDescription);
            UpdateAsset(asset);
        }

        public static void SetBoothShopDomain(Ulid assetId, string shopURL) {
            if (BoothUtility.ClassifyBoothUrl(shopURL) != BoothUtility.BoothUrlType.ShopUrl) {
                Debug.LogError("Invalid Booth shop URL.");
                return;
            }

            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            var regex = new Regex(@"https?://([^\.]+)\.booth\.pm", RegexOptions.IgnoreCase);
            var match = regex.Match(shopURL);
            if (!match.Success) return;
            var shopName = match.Groups[1].Value;
            asset.BoothData.SetShopDomain(shopName);
            UpdateAsset(asset);
        }

        public static void SetBoothItemId(Ulid assetId, string itemURL) {
            if (BoothUtility.ClassifyBoothUrl(itemURL) != BoothUtility.BoothUrlType.ItemUrl) {
                Debug.LogError("Invalid Booth item URL.");
                return;
            }

            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            var regex = new Regex(@"items/(\d+)");
            var match = regex.Match(itemURL);

            if (!match.Success) return;
            var itemId = match.Groups[1].Value;
            asset.BoothData.SetItemID(itemId);
            UpdateAsset(asset);
        }

        public static void SetBoothDownloadId(Ulid assetId, string downloadURL) {
            if (BoothUtility.ClassifyBoothUrl(downloadURL) != BoothUtility.BoothUrlType.DownloadUrl) {
                Debug.LogError("Invalid Booth download URL.");
                return;
            }

            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            var regex = new Regex(@"downloadables/(\d+)", RegexOptions.IgnoreCase);
            var match = regex.Match(downloadURL);

            if (!match.Success) return;
            var downloadId = match.Groups[1].Value;
            asset.BoothData.SetDownloadID(downloadId);
            UpdateAsset(asset);
        }

        public static void SetFolder(Ulid assetId, Ulid newFolder) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetFolder(newFolder);
            UpdateAsset(asset);
        }

        public static void AddTag(Ulid assetId, string tag) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.AddTag(tag);
            UpdateAsset(asset);
        }

        public static void RemoveTag(Ulid assetId, string tag) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.RemoveTag(tag);
            UpdateAsset(asset);
        }

        public static void RemoveAsset(Ulid assetId) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetDeleted(true);
            UpdateAsset(asset);
        }

        public static void RestoreAsset(Ulid assetId) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetDeleted(false);
            UpdateAsset(asset);
        }

        public static void CreateFolder(Ulid parentFolderId, string name, string description = null) {
            if (string.IsNullOrWhiteSpace(name)) {
                Debug.LogError("Folder name cannot be empty");
                return;
            }
            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }
            
            var folder = new Folder();
            folder.SetName(name);
            folder.SetDescription(description ?? string.Empty);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(folder);
                AssetLibrarySerializer.SaveLibrary();
                return;
            }

            var parent = libraries.GetFolder(parentFolderId);
            if (parent is BoothItemFolder) {
                Debug.LogError("Cannot create folder under Booth Item Folder.");
                return;
            }

            if (parent is not Folder parentFolder) {
                Debug.LogError($"Parent folder {parentFolderId} not found.");
                return;
            }

            parentFolder.AddChild(folder);

            AssetLibrarySerializer.SaveLibrary();
        }

        public static void MoveFolder(Ulid folderId, Ulid parentFolderId) {
            if (folderId == default) return;
            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folderBase = libraries.GetFolder(folderId);
            switch (folderBase) {
                case null:
                    Debug.LogError($"Folder {folderId} not found.");
                    return;
                case BoothItemFolder boothItemFolder when parentFolderId == default || parentFolderId == Ulid.Empty:
                    libraries.RemoveFolder(folderId);
                    libraries.AddFolder(boothItemFolder);
                    AssetLibrarySerializer.SaveLibrary();
                    return;
                case BoothItemFolder boothItemFolder: {
                    var parentBaseForBooth = libraries.GetFolder(parentFolderId);
                    switch (parentBaseForBooth) {
                        case null:
                            Debug.LogError($"Parent folder {parentFolderId} not found.");
                            return;
                        case BoothItemFolder:
                            Debug.LogError("Cannot move under another BoothItemFolder.");
                            return;
                    }

                    if (parentBaseForBooth is not Folder parentFolderForBooth) {
                        Debug.LogError($"Parent folder {parentFolderId} not found.");
                        return;
                    }
                    if (parentFolderId == folderId) {
                        Debug.LogError("Cannot move folder into itself.");
                        return;
                    }
                    libraries.RemoveFolder(folderId);
                    parentFolderForBooth.AddChild(boothItemFolder);
                    AssetLibrarySerializer.SaveLibrary();
                    return;
                }
            }

            if (folderBase is not Folder folder) {
                Debug.LogError("Unknown folder type, cannot move.");
                return;
            }
            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
            } else {
                var parentBase = libraries.GetFolder(parentFolderId);
                if (parentBase is BoothItemFolder) {
                    Debug.LogError("Cannot move folder under Booth Item Folder.");
                    return;
                }

                if (parentBase is not Folder) {
                    Debug.LogError($"Parent folder {parentFolderId} not found.");
                    return;
                }

                if (parentFolderId == folderId) {
                    Debug.LogError("Cannot move folder into itself.");
                    return;
                }

                var related = GetRelatedFolder(folder);
                if (related.Contains(parentFolderId)) {
                    Debug.LogError("Cannot move a folder into one of its own descendants.");
                    return;
                }
            }

            libraries.RemoveFolder(folderId);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(folder);
            }
            else {
                var parent = libraries.GetFolder(parentFolderId);
                if (parent is Folder parentFolder) parentFolder.AddChild(folder);
            }

            AssetLibrarySerializer.SaveLibrary();
        }

        public static void UpdateFolder(Folder newFolder) {
            if (newFolder == null) return;
            if (!IsValidAssetName(newFolder.Name)) return;

            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folder = libraries.GetFolder(newFolder.ID);
            switch (folder) {
                case null:
                    Debug.LogError($"Folder {newFolder.ID} not found.");
                    return;
                case BoothItemFolder:
                    Debug.LogError("Cannot update Booth Item Folder via this method.");
                    return;
            }

            if (folder.Name != newFolder.Name) folder.SetName(newFolder.Name);
            if (folder.Description != newFolder.Description) folder.SetDescription(newFolder.Description);
            AssetLibrarySerializer.SaveLibrary();
        }

        public static void UpdateBoothItemFolder(BoothItemFolder newFolder) {
            if (newFolder == null) return;
            if (!IsValidAssetName(newFolder.Name)) return;

            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folder = libraries.GetFolder(newFolder.ID);
            if (folder == null) {
                Debug.LogError($"Folder {newFolder.ID} not found.");
                return;
            }

            if (folder is not BoothItemFolder) {
                Debug.LogError("Target is not a Booth Item Folder.");
                return;
            }

            if (folder.Name != newFolder.Name) folder.SetName(newFolder.Name);
            if (folder.Description != newFolder.Description) folder.SetDescription(newFolder.Description);
            AssetLibrarySerializer.SaveLibrary();
        }

        public static void RenameFolder(Ulid folderId, string newName) {
            var existing = AssetLibrary.Instance.Libraries?.GetFolder(folderId);
            if (existing == null) return;
            if (!IsValidAssetName(newName)) return;
            switch (existing) {
                case Folder existingFolder: {
                    var folder = new Folder(existingFolder);
                    folder.SetName(newName);
                    UpdateFolder(folder);
                    break;
                }
                case BoothItemFolder existingBooth: {
                    var updated = new BoothItemFolder(existingBooth);
                    updated.SetName(newName);
                    UpdateBoothItemFolder(updated);
                    break;
                }
            }
        }

        public static void SetFolderDescription(Ulid folderId, string description) {
            var existing = AssetLibrary.Instance.Libraries?.GetFolder(folderId);
            switch (existing) {
                case null:
                    return;
                case Folder existingFolder: {
                    var folder = new Folder(existingFolder);
                    folder.SetDescription(description);
                    UpdateFolder(folder);
                    break;
                }
                case BoothItemFolder existingBooth: {
                    var updated = new BoothItemFolder(existingBooth);
                    updated.SetDescription(description);
                    UpdateBoothItemFolder(updated);
                    break;
                }
            }
        }

        public static void DeleteFolder(Ulid folderId) {
            if (folderId == default) return;
            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return;
            }

            var folder = libraries.GetFolder(folderId);
            if (folder == null) {
                Debug.LogError($"Folder {folderId} not found.");
                return;
            }

            List<Ulid> folderIds;
            if (folder is Folder folderWithChildren) {
                folderIds = GetRelatedFolder(folderWithChildren);
            }
            else {
                folderIds = new List<Ulid> { folder.ID };
            }
            foreach (var updatedAsset in from target in folderIds
                     select AssetLibrary.Instance.GetAssetsByFolder(target)
                     into assetsInFolder
                     where assetsInFolder != null && assetsInFolder.Count != 0
                     from asset in assetsInFolder
                     select new AssetMetadata(asset)) { 
                RemoveAsset(updatedAsset.ID);
            }

            libraries.RemoveFolder(folderId);
            AssetLibrarySerializer.SaveLibrary();
        }

        private static List<Ulid> GetRelatedFolder(BaseFolder root) {
            var result = new List<Ulid>();
            if (root == null) return result;
            result.Add(root.ID);
            if (root is not Folder f || f.Children == null) return result;
            foreach (var child in f.Children) result.AddRange(GetRelatedFolder(child));
            return result;
        }

        private static bool IsValidAssetName(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                Debug.LogError("Asset name cannot be empty or whitespace.");
                return false;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var found = name.IndexOfAny(invalidChars);
            if (found < 0) return true;
            Debug.LogError($"Asset name '{name}' contains invalid filename character '{name[found]}'.");
            return false;
        }
    }
}