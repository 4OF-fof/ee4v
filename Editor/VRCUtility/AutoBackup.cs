using System;
using _4OF.ee4v.Core.Service;
using UnityEditor;
using UnityEngine;
using _4OF.ee4v.Core.i18n;
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
            if (target is GameObject avatarGo) {
                _currentlyBuildingAvatar = avatarGo;
            }
        }

        private static void OnUploadSuccess(object sender, object target) {
            var avatarId = target as string ?? "";
            Debug.Log(I18N.Get("Debug.AutoBackup.UploadSuccess", avatarId));

            if (_currentlyBuildingAvatar != null) {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(_currentlyBuildingAvatar);

                if (prefabAsset != null) {
                    Debug.Log(I18N.Get("Debug.AutoBackup.TargetPrefab", prefabAsset.name));
                    FileIO.ExportUnityPackage(prefabAsset, avatarId);
                }

                _currentlyBuildingAvatar = null;
            } else {
                Debug.LogWarning(I18N.Get("Debug.AutoBackup.BackupFailed"));
            }
        }
        
        private static void OnUploadError(object sender, object target) {
            _currentlyBuildingAvatar = null;
        }
    }
}