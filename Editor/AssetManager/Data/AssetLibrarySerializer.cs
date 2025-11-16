using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.Data {
    public static class AssetLibrarySerializer {
        private static readonly string RootDir = Path.Combine(EditorPrefsManager.ContentFolderPath, "AssetManager");

        private static readonly string CacheFilePath =
            Path.Combine(EditorPrefsManager.ContentFolderPath, "assetManager_cache.json");

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
            SaveCache();
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
            SaveCache();
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

        public static void SaveCache() {
            var directory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var cache = new LibraryCache {
                Metadata = AssetLibrary.Instance.Libraries,
                Assets = AssetLibrary.Instance.Assets.ToList()
            };
            var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
            var tempPath = CacheFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(CacheFilePath)) File.Delete(CacheFilePath);
            File.Move(tempPath, CacheFilePath);
        }

        public static bool LoadCache() {
            if (!File.Exists(CacheFilePath)) {
                Debug.Log("Cache file does not exist. Skipping cache load.");
                return false;
            }

            try {
                var json = File.ReadAllText(CacheFilePath);
                var cache = JsonConvert.DeserializeObject<LibraryCache>(json);
                if (cache?.Metadata == null) {
                    Debug.LogWarning("Cache file is invalid or empty.");
                    return false;
                }

                AssetLibrary.Instance.SetLibrary(cache.Metadata);
                if (cache.Assets != null)
                    foreach (var asset in cache.Assets)
                        AssetLibrary.Instance.UpsertAsset(asset);

                Debug.Log($"Cache loaded: {cache.Assets?.Count ?? 0} assets.");
                return true;
            }
            catch (Exception e) {
                Debug.LogError($"Failed to load cache: {e.Message}");
                return false;
            }
        }

        public static async Task LoadAndVerifyAsync() {
            var cachedAssets = AssetLibrary.Instance.Assets.ToDictionary(a => a.ID);
            var result = await Task.Run(() =>
            {
                var assetRootDir = Path.Combine(RootDir, "Assets");
                if (!Directory.Exists(assetRootDir))
                    return new VerificationResult {
                        Error = "Assets directory does not exist. Cannot verify assets."
                    };

                var onDiskAssets = new Dictionary<Ulid, AssetMetadata>();
                var assetDirs = Directory.GetDirectories(assetRootDir);
                foreach (var assetDir in assetDirs) {
                    var metadataPath = Path.Combine(assetDir, "metadata.json");
                    if (!File.Exists(metadataPath)) continue;

                    try {
                        var json = File.ReadAllText(metadataPath);
                        var assetMetadata = JsonConvert.DeserializeObject<AssetMetadata>(json);
                        if (assetMetadata != null) onDiskAssets[assetMetadata.ID] = assetMetadata;
                    }
                    catch (Exception e) {
                        Debug.LogError($"Failed to load asset metadata from {metadataPath}: {e.Message}");
                    }
                }

                var missingInCache = onDiskAssets.Keys.Except(cachedAssets.Keys).ToList();
                var missingOnDisk = cachedAssets.Keys.Except(onDiskAssets.Keys).ToList();
                var modified = new List<AssetMetadata>();

                foreach (var (key, onDiskAsset) in onDiskAssets) {
                    if (!cachedAssets.TryGetValue(key, out var cachedAsset)) continue;
                    if (!AreAssetsEqual(cachedAsset, onDiskAsset)) modified.Add(onDiskAsset);
                }

                return new VerificationResult {
                    OnDiskAssets = onDiskAssets,
                    MissingInCache = missingInCache,
                    MissingOnDisk = missingOnDisk,
                    ModifiedAssets = modified
                };
            });

            if (!string.IsNullOrEmpty(result.Error)) {
                Debug.LogError(result.Error);
                return;
            }

            if (result.MissingInCache.Count > 0) {
                Debug.Log($"Found {result.MissingInCache.Count} assets on disk that were not in cache. Adding them.");
                foreach (var id in result.MissingInCache) AssetLibrary.Instance.UpsertAsset(result.OnDiskAssets[id]);
            }

            if (result.MissingOnDisk.Count > 0) {
                Debug.Log(
                    $"Found {result.MissingOnDisk.Count} assets in cache that were deleted from disk. Removing them.");
                foreach (var id in result.MissingOnDisk) AssetLibrary.Instance.RemoveAsset(id);
            }

            if (result.ModifiedAssets.Count > 0) {
                Debug.Log($"Updated {result.ModifiedAssets.Count} modified assets.");
                foreach (var asset in result.ModifiedAssets) AssetLibrary.Instance.UpdateAsset(asset);
            }

            if (result.MissingInCache.Count > 0 || result.MissingOnDisk.Count > 0 || result.ModifiedAssets.Count > 0) {
                SaveCache();
                Debug.Log("Cache updated with verified data.");
            }
            else {
                Debug.Log("Cache is up to date. No changes detected.");
            }
        }

        private static bool AreAssetsEqual(AssetMetadata a, AssetMetadata b) {
            if (a == null || b == null) return false;
            var tagsHashSet = new HashSet<string>(a.Tags);
            return a.Name == b.Name &&
                a.Size == b.Size &&
                a.Ext == b.Ext &&
                a.Folder == b.Folder &&
                tagsHashSet.SetEquals(b.Tags);
        }

        [Serializable]
        private class LibraryCache {
            public LibraryMetadata Metadata { get; set; }
            public List<AssetMetadata> Assets { get; set; }
        }

        private class VerificationResult {
            public string Error { get; set; }
            public Dictionary<Ulid, AssetMetadata> OnDiskAssets { get; set; }
            public List<Ulid> MissingInCache { get; set; }
            public List<Ulid> MissingOnDisk { get; set; }
            public List<AssetMetadata> ModifiedAssets { get; set; }
        }
    }
}