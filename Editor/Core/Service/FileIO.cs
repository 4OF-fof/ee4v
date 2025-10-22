﻿using System;
using System.IO;
using _4OF.ee4v.Core.Data;
using UnityEditor;
using UnityEngine;
using _4OF.ee4v.Core.i18n;

namespace _4OF.ee4v.Core.Service {
    public static class FileIO {
        public static void ExportUnityPackage(GameObject prefabAsset, string avatarId) {
            var prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            
            if (string.IsNullOrEmpty(prefabPath)) {
                Debug.LogError(I18N.Get("Debug.Core.PrefabPathNotFound"));
                return;
            }

            var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            var assetsOnlyDependencies = Array.FindAll(dependencies, path => path.StartsWith("Assets/"));

            var backupFolder = Path.Combine(EditorPrefsManager.ContentFolderPath, "AutoBackup", avatarId);
            
            if (!Directory.Exists(backupFolder)) {
                try {
                    Directory.CreateDirectory(backupFolder);
                    Debug.Log(I18N.Get("Debug.Core.CreatedBackupFolder", backupFolder));
                } catch (Exception e) {
                    Debug.LogError(I18N.Get("Debug.Core.FailedToCreateBackupFolder", e.Message));
                    return;
                }
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{prefabAsset.name}_{timestamp}.unitypackage";
            var exportPath = Path.Combine(backupFolder, fileName);

            try {
                AssetDatabase.ExportPackage(assetsOnlyDependencies, exportPath, ExportPackageOptions.Default);
                Debug.Log(I18N.Get("Debug.Core.ExportedUnityPackage", exportPath, assetsOnlyDependencies.Length));
            } catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.Core.FailedToExportUnityPackage", e.Message));
            }
        }
    }
}