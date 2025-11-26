using System;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.API {
    public static class AssetManagerAPI {
        public static void ImportBackupPackage(string packagePath, string avatarId, string avatarName,
            string description = null) {
            if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath)) {
                Debug.LogError($"[AssetManagerAPI] Backup file not found: {packagePath}");
                return;
            }

            var repository = AssetManagerContainer.Repository;
            var folderService = AssetManagerContainer.FolderService;
            var assetService = AssetManagerContainer.AssetService;

            if (repository == null) {
                Debug.LogError("[AssetManagerAPI] Repository is not initialized.");
                return;
            }

            var folderId = folderService.EnsureBackupFolder(avatarId, avatarName);
            if (folderId == Ulid.Empty) {
                Debug.LogError("[AssetManagerAPI] Failed to ensure backup folder.");
                return;
            }

            try {
                repository.CreateAssetFromFile(packagePath);

                var assets = repository.GetAllAssets();
                var createdAsset = assets.OrderByDescending(a => a.ModificationTime).FirstOrDefault();

                if (createdAsset != null) {
                    var fileName = Path.GetFileNameWithoutExtension(packagePath);

                    var newMeta = new AssetMetadata(createdAsset);
                    newMeta.SetName(fileName);
                    newMeta.SetFolder(folderId);
                    if (!string.IsNullOrEmpty(description)) newMeta.SetDescription(description);

                    assetService.SaveAsset(newMeta);
                    Debug.Log($"[AssetManagerAPI] Backup imported successfully: {fileName}");
                }
                else {
                    Debug.LogError("[AssetManagerAPI] Failed to retrieve created asset metadata.");
                }
            }
            catch (Exception e) {
                Debug.LogError($"[AssetManagerAPI] Exception during backup import: {e.Message}");
            }
        }
    }
}