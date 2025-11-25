using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Utility;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    public class AssetService {
        private readonly FolderService _folderService;
        private readonly IAssetRepository _repository;

        public AssetService(IAssetRepository repository, FolderService folderService) {
            _repository = repository;
            _folderService = folderService;
        }

        public void SaveAsset(AssetMetadata asset) {
            if (asset == null) return;

            try {
                var lib = _repository.GetLibraryMetadata();
                if (lib != null && asset.BoothData != null && !string.IsNullOrEmpty(asset.BoothData.ItemId)) {
                    var identifier = asset.BoothData.ItemId;
                    var folderName = asset.BoothData.FileName ?? asset.Name ?? identifier ?? "Booth Item";
                    var folderDesc = asset.BoothData.FileName ?? string.Empty;
                    var folderId = _folderService?.EnsureBoothItemFolder(asset.BoothData.ShopDomain ?? string.Empty,
                        null, identifier, folderName, folderDesc) ?? Ulid.Empty;

                    if (folderId != Ulid.Empty && asset.Folder == Ulid.Empty) asset.SetFolder(folderId);
                }
            }
            catch {
            }

            _repository.SaveAsset(asset);
        }

        public void CreateAsset(string path) {
            _repository.CreateAssetFromFile(path);
        }

        public void AddFileToAsset(Ulid assetId, string path) {
            _repository.AddFileToAsset(assetId, path);
        }

        public void DeleteAsset(Ulid assetId) {
            _repository.DeleteAsset(assetId);
        }

        public void RemoveAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDeleted(true);
            SaveAsset(newAsset);
        }

        public void RestoreAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDeleted(false);
            SaveAsset(newAsset);
        }

        public void UpdateAsset(AssetMetadata newAsset) {
            if (!AssetValidationService.IsValidAssetName(newAsset.Name)) return;
            var oldAsset = _repository.GetAsset(newAsset.ID);
            if (oldAsset == null) return;

            if (oldAsset.Name != newAsset.Name) _repository.RenameAssetFile(newAsset.ID, newAsset.Name);
            SaveAsset(newAsset);
        }

        public bool SetAssetName(Ulid assetId, string newName) {
            if (!AssetValidationService.IsValidAssetName(newName)) {
                Debug.LogError("Invalid asset name: cannot set an empty or invalid name.");
                return false;
            }

            var asset = _repository.GetAsset(assetId);
            if (asset == null) {
                Debug.LogError($"Asset not found: {assetId}");
                return false;
            }

            _repository.RenameAssetFile(assetId, newName);

            var newAsset = new AssetMetadata(asset);
            newAsset.SetName(newName);
            SaveAsset(newAsset);
            return true;
        }

        public void SetDescription(Ulid assetId, string newDescription) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDescription(newDescription);
            SaveAsset(newAsset);
        }

        public void SetFolder(Ulid assetId, Ulid newFolder) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetFolder(newFolder);
            SaveAsset(newAsset);
        }

        public void AddTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.AddTag(tag);
            SaveAsset(newAsset);
        }

        public void RemoveTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.RemoveTag(tag);
            SaveAsset(newAsset);
        }

        public void RenameTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(oldTag) || string.IsNullOrEmpty(newTag) || oldTag == newTag) return;

            var assets = _repository.GetAllAssets().ToList();
            var assetsToSave = new List<AssetMetadata>();
            foreach (var asset in assets.Where(a => a.Tags.Contains(oldTag))) {
                var newAsset = new AssetMetadata(asset);
                newAsset.RemoveTag(oldTag);
                newAsset.AddTag(newTag);
                assetsToSave.Add(newAsset);
            }

            if (assetsToSave.Count > 0) _repository.SaveAssets(assetsToSave);

            var lib = _repository.GetLibraryMetadata();
            if (lib == null) return;
            var modifiedFolderIds = new List<Ulid>();
            foreach (var folder in lib.FolderList) RenameFolderTag(folder, oldTag, newTag, modifiedFolderIds);
            if (modifiedFolderIds.Count <= 0) return;
            foreach (var fid in modifiedFolderIds) _repository.SaveFolder(fid);
        }

        public void DeleteTag(string tag) {
            if (string.IsNullOrEmpty(tag)) return;

            var assets = _repository.GetAllAssets().ToList();
            var assetsToSave = new List<AssetMetadata>();
            foreach (var asset in assets.Where(a => a.Tags.Contains(tag))) {
                var newAsset = new AssetMetadata(asset);
                newAsset.RemoveTag(tag);
                assetsToSave.Add(newAsset);
            }

            if (assetsToSave.Count > 0) _repository.SaveAssets(assetsToSave);

            var lib = _repository.GetLibraryMetadata();
            if (lib == null) return;
            var modifiedFolderIds = new List<Ulid>();
            foreach (var folder in lib.FolderList) DeleteFolderTag(folder, tag, modifiedFolderIds);
            if (modifiedFolderIds.Count <= 0) return;
            foreach (var fid in modifiedFolderIds) _repository.SaveFolder(fid);
        }

        private static bool RenameFolderTag(BaseFolder folder, string oldTag, string newTag, List<Ulid> modifiedIds) {
            var modified = false;
            if (folder.Tags.Contains(oldTag)) {
                folder.RemoveTag(oldTag);
                folder.AddTag(newTag);
                modified = true;
                modifiedIds?.Add(folder.ID);
            }

            if (folder is not Folder f || f.Children == null) return modified;
            foreach (var child in f.Children)
                if (RenameFolderTag(child, oldTag, newTag, modifiedIds))
                    modified = true;
            return modified;
        }

        private static bool DeleteFolderTag(BaseFolder folder, string tag, List<Ulid> modifiedIds) {
            var modified = false;
            if (folder.Tags.Contains(tag)) {
                folder.RemoveTag(tag);
                modified = true;
                modifiedIds?.Add(folder.ID);
            }

            if (folder is not Folder f || f.Children == null) return modified;
            foreach (var child in f.Children)
                if (DeleteFolderTag(child, tag, modifiedIds))
                    modified = true;
            return modified;
        }

        public void SetBoothShopDomain(Ulid assetId, string shopURL) {
            if (BoothUtility.ClassifyBoothUrl(shopURL) != BoothUtility.BoothUrlType.ShopUrl) return;

            var regex = new Regex(@"https?://([^\.]+)\.booth\.pm", RegexOptions.IgnoreCase);
            var match = regex.Match(shopURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.BoothData.SetShopDomain(match.Groups[1].Value);
            SaveAsset(newAsset);
        }

        public void SetBoothItemId(Ulid assetId, string itemURL) {
            if (BoothUtility.ClassifyBoothUrl(itemURL) != BoothUtility.BoothUrlType.ItemUrl) return;

            var regex = new Regex(@"items/(\d+)");
            var match = regex.Match(itemURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.BoothData.SetItemID(match.Groups[1].Value);
            SaveAsset(newAsset);
        }

        public void SetBoothDownloadId(Ulid assetId, string downloadURL) {
            if (BoothUtility.ClassifyBoothUrl(downloadURL) != BoothUtility.BoothUrlType.DownloadUrl) return;

            var regex = new Regex(@"downloadables/(\d+)", RegexOptions.IgnoreCase);
            var match = regex.Match(downloadURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.BoothData.SetDownloadID(match.Groups[1].Value);
            SaveAsset(newAsset);
        }

        public void AddAssetGuid(Ulid assetId, Guid guid) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.AddAssetGuid(guid);
            SaveAsset(newAsset);
        }

        public void RemoveAssetGuid(Ulid assetId, Guid guid) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.RemoveAssetGuid(guid);
            SaveAsset(newAsset);
        }

        public void AddDependenceItem(Ulid assetId, Ulid dependenceItemId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.AddDependenceItem(dependenceItemId);
            SaveAsset(newAsset);
        }

        public void RemoveDependenceItem(Ulid assetId, Ulid dependenceItemId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.RemoveDependenceItem(dependenceItemId);
            SaveAsset(newAsset);
        }

        public void ImportFilesFromZip(Ulid assetId, string tempRootPath, List<string> relativePaths) {
            if (relativePaths == null || relativePaths.Count == 0) return;

            _repository.ImportFiles(assetId, tempRootPath, relativePaths);
        }

        public void ImportAsset(Ulid assetId, string destFolder = "Assets") {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            if (asset.Ext.Equals(".unitypackage", StringComparison.OrdinalIgnoreCase)) {
                var files = _repository.GetAssetFiles(assetId, "*.unitypackage");
                if (files.Count > 0) {
                    AssetDatabase.ImportPackage(files[0], true);
                }
                return;
            }

            if (asset.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
                if (!_repository.HasImportItems(assetId)) return;
                var importDir = _repository.GetImportDirectoryPath(assetId);
                var packages = Directory.GetFiles(importDir, "*.unitypackage", SearchOption.AllDirectories);
                if (packages.Length > 0) {
                    AssetDatabase.ImportPackage(packages[0], true);
                }
                else {
                    Debug.LogWarning("No unitypackage found in the import folder. (Direct file copy for zip content is not yet fully supported via Import button)");
                }
                return;
            }

            var assetFiles = _repository.GetAssetFiles(assetId);
            var mainFile = assetFiles.FirstOrDefault(f => 
                !f.EndsWith("metadata.json") && 
                !f.EndsWith("thumbnail.png") &&
                !f.Contains(Path.DirectorySeparatorChar + "Import" + Path.DirectorySeparatorChar));

            if (string.IsNullOrEmpty(mainFile) || !File.Exists(mainFile)) {
                Debug.LogError($"Asset file not found for {asset.Name}");
                return;
            }

            var fileName = Path.GetFileName(mainFile);
            var destPath = Path.Combine(destFolder, fileName);
            var metaFileName = fileName + ".meta";
            
            var repoImportDir = _repository.GetImportDirectoryPath(assetId);
            var storedMetaPath = Path.Combine(repoImportDir, metaFileName);

            if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
            if (!Directory.Exists(repoImportDir)) Directory.CreateDirectory(repoImportDir);

            if (File.Exists(storedMetaPath)) {
                var destMetaPath = destPath + ".meta";
                try {
                    File.Copy(mainFile, destPath, true);
                    File.Copy(storedMetaPath, destMetaPath, true);
                    
                    Debug.Log($"Imported '{fileName}' with restored Meta (GUID preserved).");
                    
                    AssetDatabase.Refresh();
                }
                catch (Exception ex) {
                    Debug.LogError($"Failed to import asset with meta: {ex.Message}");
                }
            }
            else {
                try {
                    File.Copy(mainFile, destPath, true);
                    AssetDatabase.Refresh();

                    var generatedMetaPath = destPath + ".meta";
                    if (File.Exists(generatedMetaPath)) {
                        File.Copy(generatedMetaPath, storedMetaPath, true);
                        Debug.Log($"Imported '{fileName}' and backed up Meta file to repository.");
                    }
                    else {
                        Debug.LogWarning($"Imported '{fileName}' but could not find generated Meta file for backup.");
                    }
                }
                catch (Exception ex) {
                    Debug.LogError($"Failed to import asset: {ex.Message}");
                }
            }
        }
    }
}