using System;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using VRC.SDK3A.Editor;

namespace _4OF.ee4v.VRCUtility {
    public static class AutoBackup {
        private static GameObject _currentlyBuildingAvatar;

        [InitializeOnLoadMethod]
        private static void Initialize() {
            VRCSdkControlPanel.OnSdkPanelEnable += OnSdkPanelEnable;
        }

        private static void OnSdkPanelEnable(object sender, EventArgs e) {
            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var avatarBuilder)) return;

            avatarBuilder.OnSdkBuildStart -= OnBuildStart;
            avatarBuilder.OnSdkUploadSuccess -= OnUploadSuccess;
            avatarBuilder.OnSdkUploadError -= OnUploadError;

            avatarBuilder.OnSdkBuildStart += OnBuildStart;
            avatarBuilder.OnSdkUploadSuccess += OnUploadSuccess;
            avatarBuilder.OnSdkUploadError += OnUploadError;
        }

        private static void OnBuildStart(object sender, object target) {
            if (target is GameObject avatarGo) _currentlyBuildingAvatar = avatarGo;
        }

        private static void OnUploadSuccess(object sender, object target) {
            var avatarId = target as string ?? "";
            Debug.Log(I18N.Get("Debug.VRCUtility.UploadSuccess", avatarId));

            if (_currentlyBuildingAvatar != null) {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(_currentlyBuildingAvatar);

                if (prefabAsset != null) {
                    Debug.Log(I18N.Get("Debug.VRCUtility.TargetPrefab", prefabAsset.name));
                    BackupToAssetManager(prefabAsset, avatarId);
                }

                _currentlyBuildingAvatar = null;
            }
            else {
                Debug.LogWarning(I18N.Get("Debug.VRCUtility.BackupFailed"));
            }
        }

        private static void OnUploadError(object sender, object target) {
            _currentlyBuildingAvatar = null;
        }

        private static void BackupToAssetManager(GameObject prefabAsset, string avatarId) {
            var prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabPath)) return;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{prefabAsset.name}_{timestamp}.unitypackage";
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            var assetsOnlyDependencies = Array.FindAll(dependencies, path => path.StartsWith("Assets/"));

            try {
                AssetDatabase.ExportPackage(assetsOnlyDependencies, tempPath, ExportPackageOptions.Default);
            }
            catch (Exception e) {
                Debug.LogError($"Failed to export temporary package: {e.Message}");
                return;
            }

            var repository = AssetManagerContainer.Repository;
            var folderService = AssetManagerContainer.FolderService;
            var assetService = AssetManagerContainer.AssetService;

            if (repository == null) {
                Debug.LogError("AssetManager Repository is not initialized.");
                return;
            }

            var avatarName = prefabAsset.name;
            var folderId = folderService.EnsureBackupFolder(avatarId, avatarName);

            if (folderId == Ulid.Empty) {
                Debug.LogError("Failed to get or create backup folder.");
                return;
            }

            try {
                repository.CreateAssetFromFile(tempPath);

                var assets = repository.GetAllAssets();
                var createdAsset = assets.OrderByDescending(a => a.ModificationTime).FirstOrDefault();

                if (createdAsset == null) return;
                var newMeta = new AssetMetadata(createdAsset);
                newMeta.SetName(Path.GetFileNameWithoutExtension(fileName));
                newMeta.SetFolder(folderId);
                newMeta.SetDescription($"Auto backup for upload {timestamp}");

                assetService.SaveAsset(newMeta);
                Debug.Log($"[ee4v] Backup saved to AssetManager: {fileName}");
            }
            catch (Exception e) {
                Debug.LogError($"Failed to import backup to AssetManager: {e.Message}");
            }
            finally {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}