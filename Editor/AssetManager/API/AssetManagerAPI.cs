using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
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

        public static Dictionary<string, string> GetAssetsAssociatedWithGuid(string guid) {
            var result = new Dictionary<string, string>();
            var repository = AssetManagerContainer.Repository;

            if (repository == null || !Guid.TryParse(guid, out var targetGuid)) return result;

            var assets = repository.GetAllAssets();
            foreach (var asset in assets)
                if (asset.UnityData != null && asset.UnityData.AssetGuidList.Contains(targetGuid))
                    result[asset.ID.ToString()] = asset.Name;

            return result;
        }

        public static Texture2D GetAssetThumbnail(string ulidString) {
            if (!Ulid.TryParse(ulidString, out var ulid)) return null;
            var service = AssetManagerContainer.TextureService;
            if (service == null) return null;

            if (service.TryGetCachedAssetThumbnail(ulid, out var texture)) return texture;

            _ = LoadThumbnailAndRepaint(service, ulid);
            return null;
        }

        private static async Task LoadThumbnailAndRepaint(TextureService service, Ulid ulid) {
            await service.GetAssetThumbnailAsync(ulid);
            EditorApplication.RepaintProjectWindow();
        }
    }
}