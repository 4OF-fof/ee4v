using System.IO;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AvatarModifyUtility {
    [InitializeOnLoad]
    public static class VariantWatcher {
        static VariantWatcher() {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private static void OnHierarchyChanged() {
            if (Application.isPlaying) return;

            var targets = Object.FindObjectsOfType<VariantAutoUpdater>();
            if (targets.Length == 0) return;

            foreach (var target in targets) UpdateMaterials(target.gameObject);
        }

        private static void UpdateMaterials(GameObject rootObject) {
            var variantFullPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rootObject);

            if (string.IsNullOrEmpty(variantFullPath)) return;

            var variantDirectory = Path.GetDirectoryName(variantFullPath);
            if (variantDirectory == null) return;
            var materialsDirectory = Path.Combine(variantDirectory, "Materials");

            if (!Directory.Exists(materialsDirectory)) Directory.CreateDirectory(materialsDirectory);

            var renderers = rootObject.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers) {
                var sharedMats = renderer.sharedMaterials;
                var modifiedMatArray = false;

                for (var i = 0; i < sharedMats.Length; i++) {
                    var sourceMat = sharedMats[i];
                    if (sourceMat == null) continue;

                    var sourcePath = AssetDatabase.GetAssetPath(sourceMat);

                    if (string.IsNullOrEmpty(sourcePath) ||
                        sourcePath.Replace('\\', '/').StartsWith(materialsDirectory.Replace('\\', '/')))
                        continue;

                    var newMatName = sourceMat.name + ".mat";
                    var newMatPath = Path.Combine(materialsDirectory, newMatName).Replace('\\', '/');

                    var variantMat = AssetDatabase.LoadAssetAtPath<Material>(newMatPath);

                    if (variantMat == null) {
                        variantMat = new Material(sourceMat);
                        AssetDatabase.CreateAsset(variantMat, newMatPath);
                    }

                    if (variantMat == null || sharedMats[i] == variantMat) continue;
                    sharedMats[i] = variantMat;
                    modifiedMatArray = true;
                }

                if (!modifiedMatArray) continue;
                renderer.sharedMaterials = sharedMats;
                EditorUtility.SetDirty(renderer);
            }
        }
    }
}