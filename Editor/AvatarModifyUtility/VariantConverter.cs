using System.Collections.Generic;
using System.IO;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AvatarModifyUtility {
    public static class VariantConverter {
        public static void CreateVariantWithMaterials(GameObject sourceObject, string variantName) {
            if (sourceObject == null || string.IsNullOrEmpty(variantName)) return;

            var rootFolder = EditorPrefsManager.VariantCreateFolderPath;
            if (!rootFolder.EndsWith("/")) rootFolder += "/";
            if (!Directory.Exists(rootFolder)) Directory.CreateDirectory(rootFolder);

            var targetFolder = Path.Combine(rootFolder, variantName).Replace('\\', '/');
            var materialFolder = Path.Combine(targetFolder, "Materials").Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(targetFolder)) {
                Debug.LogError(I18N.Get("Debug.HierarchyExtension.VariantFolderExists", targetFolder));
                return;
            }

            var parentGuid = AssetDatabase.AssetPathToGUID(Path.GetDirectoryName(targetFolder));
            if (string.IsNullOrEmpty(parentGuid)) {
                Directory.CreateDirectory(targetFolder);
                Directory.CreateDirectory(materialFolder);
                AssetDatabase.Refresh();
            }
            else {
                AssetDatabase.CreateFolder(rootFolder.TrimEnd('/'), variantName);
                AssetDatabase.CreateFolder(targetFolder, "Materials");
            }

            var originalToVariantMap = new Dictionary<Material, Material>();
            var renderers = sourceObject.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers) {
                foreach (var sharedMat in renderer.sharedMaterials) {
                    if (sharedMat == null) continue;
                    if (originalToVariantMap.ContainsKey(sharedMat)) continue;

                    var matName = sharedMat.name;
                    var newMatPath = Path.Combine(materialFolder, matName + ".mat").Replace('\\', '/');
                    
                    newMatPath = AssetDatabase.GenerateUniqueAssetPath(newMatPath);

                    var newMat = new Material(sharedMat);
                    AssetDatabase.CreateAsset(newMat, newMatPath);
                    originalToVariantMap[sharedMat] = newMat;
                }
            }

            var prefabPath = Path.Combine(targetFolder, variantName + ".prefab").Replace('\\', '/');
            if (PrefabUtility.IsPartOfAnyPrefab(sourceObject)) {
                PrefabUtility.SaveAsPrefabAssetAndConnect(sourceObject, prefabPath, InteractionMode.AutomatedAction);
            }
            else {
                PrefabUtility.SaveAsPrefabAssetAndConnect(sourceObject, prefabPath, InteractionMode.AutomatedAction);
            }

            var contentsRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                var contentRenderers = contentsRoot.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in contentRenderers) {
                    var sharedMats = renderer.sharedMaterials;
                    var modified = false;
                    for (var i = 0; i < sharedMats.Length; i++) {
                        if (sharedMats[i] == null || !originalToVariantMap.TryGetValue(sharedMats[i], out var newMat))
                            continue;
                        sharedMats[i] = newMat;
                        modified = true;
                    }

                    if (modified) {
                        renderer.sharedMaterials = sharedMats;
                    }
                }
                
                PrefabUtility.SaveAsPrefabAsset(contentsRoot, prefabPath);
            }
            finally {
                PrefabUtility.UnloadPrefabContents(contentsRoot);
            }

            AssetDatabase.Refresh();
            var createdAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            EditorGUIUtility.PingObject(createdAsset);
            
            Debug.Log(I18N.Get("Debug.HierarchyExtension.VariantCreated", variantName));
        }
    }
}