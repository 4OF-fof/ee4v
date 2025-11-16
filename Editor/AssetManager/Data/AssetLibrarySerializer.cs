using System;
using System.IO;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.Data {
    public static class AssetLibrarySerializer {
        private static readonly string RootDir = Path.Combine(EditorPrefsManager.ContentFolderPath, "AssetManager");

        public static void Initialize() {
            Directory.CreateDirectory(RootDir);
            var assetDir = Path.Combine(RootDir, "Assets");
            Directory.CreateDirectory(assetDir);
            var filePath = Path.Combine(RootDir, "metadata.json");
            if (File.Exists(filePath)) {
                Debug.LogWarning("Metadata file already exists. Initialization skipped.");
                return;
            }

            var metadata = new LibraryMetadata();
            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static void LoadLibrary() {
            var filePath = Path.Combine(RootDir, "metadata.json");
            if (!File.Exists(filePath)) {
                Debug.LogError("Metadata file does not exist. Cannot load library.");
                return;
            }

            var json = File.ReadAllText(filePath);
            var metadata = JsonConvert.DeserializeObject<LibraryMetadata>(json);
            AssetLibrary.Instance.SetLibrary(metadata);
        }

        public static void SaveLibrary() {
            var metadata = AssetLibrary.Instance.Libraries;
            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            var filePath = Path.Combine(RootDir, "metadata.json");
            File.WriteAllText(filePath, json);
        }

        public static void LoadAsset(Ulid assetId) {
            var filePath = Path.Combine(RootDir, "Assets", assetId.ToString(), "metadata.json");
            if (!File.Exists(filePath)) {
                Debug.LogError("Metadata file does not exist. Cannot load asset.");
                return;
            }

            var json = File.ReadAllText(filePath);
            var assetMetadata = JsonConvert.DeserializeObject<AssetMetadata>(json);
            AssetLibrary.Instance.UpsertAsset(assetMetadata);
        }

        public static void LoadAllAssets() {
            var assetRootDir = Path.Combine(RootDir, "Assets");
            if (!Directory.Exists(assetRootDir)) {
                Debug.LogError("Assets directory does not exist. Cannot load assets.");
                return;
            }

            var assetDirs = Directory.GetDirectories(assetRootDir);
            foreach (var assetDir in assetDirs) {
                var metadataPath = Path.Combine(assetDir, "metadata.json");
                if (!File.Exists(metadataPath)) continue;

                var json = File.ReadAllText(metadataPath);
                var assetMetadata = JsonConvert.DeserializeObject<AssetMetadata>(json);
                AssetLibrary.Instance.UpsertAsset(assetMetadata);
            }
        }

        public static void SaveAsset(AssetMetadata assetMetadata) {
            var assetDir = Path.Combine(RootDir, "Assets", assetMetadata.ID.ToString());
            Directory.CreateDirectory(assetDir);

            var json = JsonConvert.SerializeObject(assetMetadata, Formatting.Indented);
            var filePath = Path.Combine(assetDir, "metadata.json");
            File.WriteAllText(filePath, json);
        }

        public static void AddAsset(string path) {
            var fileInfo = new FileInfo(path);
            var assetMetadata = new AssetMetadata();
            assetMetadata.SetName(Path.GetFileNameWithoutExtension(fileInfo.Name));
            assetMetadata.SetSize(fileInfo.Length);
            assetMetadata.SetExt(fileInfo.Extension);
            var assetDir = Path.Combine(RootDir, "Assets", assetMetadata.ID.ToString());

            try {
                Directory.CreateDirectory(assetDir);
                var destPath = Path.Combine(assetDir, fileInfo.Name);
                File.Copy(path, destPath, true);
                SaveAsset(assetMetadata);
                AssetLibrary.Instance.AddAsset(assetMetadata);
            }
            catch (Exception e) {
                Debug.LogError($"Failed to add asset. Rolling back... Error: {e.Message}");
                try {
                    if (Directory.Exists(assetDir)) Directory.Delete(assetDir, true);
                }
                catch (Exception deleteEx) {
                    Debug.LogError(
                        $"Critical: Failed to rollback directory {assetDir}. Manual cleanup required. Error: {deleteEx.Message}");
                }

                throw;
            }
        }

        public static void DeleteAsset(Ulid assetId) {
            var assetDir = Path.Combine(RootDir, "Assets", assetId.ToString());
            if (Directory.Exists(assetDir)) Directory.Delete(assetDir, true);
            AssetLibrary.Instance.RemoveAsset(assetId);
        }

        public static void RenameAsset(Ulid assetId, string newName) {
            var assetDir = Path.Combine(RootDir, "Assets", assetId.ToString());
            var assetMetadata = AssetLibrary.Instance.GetAsset(assetId);

            var oldFileName = assetMetadata.Name + assetMetadata.Ext;
            var newFileName = newName + assetMetadata.Ext;
            var oldPath = Path.Combine(assetDir, oldFileName);
            var newPath = Path.Combine(assetDir, newFileName);

            if (File.Exists(oldPath)) File.Move(oldPath, newPath);
        }

        public static void SetThumbnail(Ulid assetId, string imagePath) {
            var assetDir = Path.Combine(RootDir, "Assets", assetId.ToString());
            if (!Directory.Exists(assetDir)) {
                Debug.LogError($"Asset directory does not exist: {assetId}");
                return;
            }

            var fileData = File.ReadAllBytes(imagePath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!tex.LoadImage(fileData)) {
                Debug.LogError($"Failed to load image data from: {imagePath}");
                Object.DestroyImmediate(tex);
                return;
            }

            var maybeResized = TextureUtility.FitImage(tex, 1024);
            var finalTex = maybeResized ?? tex;
            if (!ReferenceEquals(finalTex, tex) && tex) Object.DestroyImmediate(tex);

            var pngBytes = finalTex.EncodeToPNG();
            var destPath = Path.Combine(assetDir, "thumbnail.png");
            File.WriteAllBytes(destPath, pngBytes);
            Object.DestroyImmediate(finalTex);
        }

        public static void RemoveThumbnail(Ulid assetId) {
            var assetDir = Path.Combine(RootDir, "Assets", assetId.ToString());
            var thumbPath = Path.Combine(assetDir, "thumbnail.png");
            File.Delete(thumbPath);
        }
    }
}