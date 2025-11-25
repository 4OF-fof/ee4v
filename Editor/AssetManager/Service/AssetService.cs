using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Utility;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    public class AssetService {
        private readonly IAssetRepository _repository;

        public AssetService(IAssetRepository repository) {
            _repository = repository;
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
            _repository.SaveAsset(newAsset);
        }

        public void RestoreAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDeleted(false);
            _repository.SaveAsset(newAsset);
        }

        public void UpdateAsset(AssetMetadata newAsset) {
            if (!AssetValidationService.IsValidAssetName(newAsset.Name)) return;
            var oldAsset = _repository.GetAsset(newAsset.ID);
            if (oldAsset == null) return;

            if (oldAsset.Name != newAsset.Name) _repository.RenameAssetFile(newAsset.ID, newAsset.Name);
            _repository.SaveAsset(newAsset);
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
            _repository.SaveAsset(newAsset);
            return true;
        }

        public void SetDescription(Ulid assetId, string newDescription) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDescription(newDescription);
            _repository.SaveAsset(newAsset);
        }

        public void SetFolder(Ulid assetId, Ulid newFolder) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetFolder(newFolder);
            _repository.SaveAsset(newAsset);
        }

        public void AddTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.AddTag(tag);
            _repository.SaveAsset(newAsset);
        }

        public void RemoveTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.RemoveTag(tag);
            _repository.SaveAsset(newAsset);
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
            _repository.SaveAsset(newAsset);
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
            _repository.SaveAsset(newAsset);
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
            _repository.SaveAsset(newAsset);
        }

        public void AddAssetGuid(Ulid assetId, Guid guid) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.AddAssetGuid(guid);
            _repository.SaveAsset(newAsset);
        }

        public void RemoveAssetGuid(Ulid assetId, Guid guid) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.RemoveAssetGuid(guid);
            _repository.SaveAsset(newAsset);
        }

        public void AddDependenceItem(Ulid assetId, Ulid dependenceItemId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.AddDependenceItem(dependenceItemId);
            _repository.SaveAsset(newAsset);
        }

        public void RemoveDependenceItem(Ulid assetId, Ulid dependenceItemId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.RemoveDependenceItem(dependenceItemId);
            _repository.SaveAsset(newAsset);
        }
    }
}