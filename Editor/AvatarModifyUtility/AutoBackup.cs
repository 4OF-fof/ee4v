using System;
using System.IO;
using _4OF.ee4v.AssetManager.API;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;
using VRC.SDK3A.Editor;

namespace _4OF.ee4v.AvatarModifyUtility {
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
            if (!SettingSingleton.I.enableAutoBackup) return;
            var avatarId = target as string ?? "";
            Debug.Log(I18N.Get("Debug.AvatarModifyUtility.UploadSuccess", avatarId));

            if (_currentlyBuildingAvatar != null) {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(_currentlyBuildingAvatar);

                if (prefabAsset != null) {
                    Debug.Log(I18N.Get("Debug.AvatarModifyUtility.TargetPrefab", prefabAsset.name));
                    BackupToAssetManager(prefabAsset, avatarId);
                }

                _currentlyBuildingAvatar = null;
            }
            else {
                Debug.LogWarning(I18N.Get("Debug.AvatarModifyUtility.BackupFailed"));
            }
        }

        private static void OnUploadError(object sender, object target) {
            Debug.LogWarning(I18N.Get("Debug.AvatarModifyUtility.UploadError"));
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

                AssetManagerAPI.ImportBackupPackage(
                    tempPath,
                    avatarId,
                    prefabAsset.name
                );
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AvatarModifyUtility.ExportImportFailed", e.Message));
            }
            finally {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}