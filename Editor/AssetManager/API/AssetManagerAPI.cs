using System;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.API {
    public static class AssetManagerAPI {
        public static void ImportBackupPackage(string packagePath, string avatarId, string avatarName,
            string description = null) {
            if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath)) {
                Debug.LogError(I18N.Get("Debug.AssetManager.API.BackupFileNotFoundFmt", packagePath));
                return;
            }

            var repository = AssetManagerContainer.Repository;
            var folderService = AssetManagerContainer.FolderService;
            var assetService = AssetManagerContainer.AssetService;

            if (repository == null) {
                Debug.LogError(I18N.Get("Debug.AssetManager.API.RepositoryNotInitialized"));
                return;
            }

            var folderId = folderService.EnsureBackupFolder(avatarId, avatarName);
            if (folderId == Ulid.Empty) {
                Debug.LogError(I18N.Get("Debug.AssetManager.API.FailedToEnsureBackupFolder"));
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
                    Debug.Log(I18N.Get("Debug.AssetManager.API.BackupImportedFmt", fileName));
                }
                else {
                    Debug.LogError(I18N.Get("Debug.AssetManager.API.FailedToRetrieveCreatedAssetMetadata"));
                }
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AssetManager.API.ExceptionDuringBackupImportFmt", e.Message));
            }
        }
    }
}