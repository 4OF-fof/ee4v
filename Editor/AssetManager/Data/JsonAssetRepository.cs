using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.Data {
    public class JsonAssetRepository : IAssetRepository {
        private readonly string _rootDir;
        private readonly string _assetRootDir;
        private readonly string _libraryMetadataPath;
        private readonly string _cacheFilePath;
        
        private readonly JsonSerializerSettings _serializerSettings;
        
        private readonly AssetLibrary _libraryCache;

        public JsonAssetRepository(string contentFolderPath) {
            _rootDir = Path.Combine(contentFolderPath, "AssetManager");
            _assetRootDir = Path.Combine(_rootDir, "Assets");
            _libraryMetadataPath = Path.Combine(_rootDir, "metadata.json");
            _cacheFilePath = Path.Combine(contentFolderPath, "assetManager_cache.json");

            _serializerSettings = new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new AllowedTypesBinder()
            };

            _libraryCache = new AssetLibrary(); 
        }

        public void Initialize() {
            Directory.CreateDirectory(_rootDir);
            Directory.CreateDirectory(_assetRootDir);

            if (File.Exists(_libraryMetadataPath)) {
                return;
            }

            var metadata = new LibraryMetadata();
            SaveLibraryMetadata(metadata);
        }

        public void Load() {
            if (LoadCache()) {
                return;
            }

            if (!File.Exists(_libraryMetadataPath)) {
                Debug.LogError($"[JsonAssetRepository] Metadata file not found at {_libraryMetadataPath}");
                return;
            }

            try {
                var json = File.ReadAllText(_libraryMetadataPath);
                var metadata = JsonConvert.DeserializeObject<LibraryMetadata>(json, _serializerSettings);
                _libraryCache.SetLibrary(metadata);
                
                LoadAllAssetsFromDisk();
                SaveCache();
            }
            catch (Exception e) {
                Debug.LogError($"[JsonAssetRepository] Failed to load library: {e.Message}");
            }
        }

        private void LoadAllAssetsFromDisk() {
            if (!Directory.Exists(_assetRootDir)) return;

            var assetDirs = Directory.GetDirectories(_assetRootDir);
            foreach (var assetDir in assetDirs) {
                var metadataPath = Path.Combine(assetDir, "metadata.json");
                if (!File.Exists(metadataPath)) continue;

                try {
                    var json = File.ReadAllText(metadataPath);
                    var assetMetadata = JsonConvert.DeserializeObject<AssetMetadata>(json, _serializerSettings);
                    if (assetMetadata != null) {
                        _libraryCache.UpsertAsset(assetMetadata);
                    }
                }
                catch (Exception e) {
                    Debug.LogError($"[JsonAssetRepository] Failed to load asset metadata from {metadataPath}: {e.Message}");
                }
            }
        }

        public async Task LoadAndVerifyAsync() {
            var cachedAssets = _libraryCache.Assets.ToDictionary(a => a.ID);

            var result = await Task.Run(() => {
                if (!Directory.Exists(_assetRootDir)) {
                    return (
                        Error: "Assets directory does not exist.",
                        OnDisk: null,
                        MissingInCache: null,
                        MissingOnDisk: null,
                        Modified: null
                    );
                }

                var onDiskAssets = new Dictionary<Ulid, AssetMetadata>();
                var assetDirs = Directory.GetDirectories(_assetRootDir);
                foreach (var assetDir in assetDirs) {
                    var metadataPath = Path.Combine(assetDir, "metadata.json");
                    if (!File.Exists(metadataPath)) continue;

                    try {
                        var json = File.ReadAllText(metadataPath);
                        var assetMetadata = JsonConvert.DeserializeObject<AssetMetadata>(json, _serializerSettings);
                        if (assetMetadata != null) onDiskAssets[assetMetadata.ID] = assetMetadata;
                    }
                    catch { /* ignore */ }
                }

                var missingInCache = onDiskAssets.Keys.Except(cachedAssets.Keys).ToList();
                var missingOnDisk = cachedAssets.Keys.Except(onDiskAssets.Keys).ToList();
                var modified = new List<AssetMetadata>();

                foreach (var kvp in onDiskAssets) {
                    if (cachedAssets.TryGetValue(kvp.Key, out var cachedAsset)) {
                        if (!AreAssetsEqual(cachedAsset, kvp.Value)) {
                            modified.Add(kvp.Value);
                        }
                    }
                }

                return (
                    Error: (string)null,
                    OnDisk: onDiskAssets,
                    MissingInCache: missingInCache,
                    MissingOnDisk: missingOnDisk,
                    Modified: modified
                );
            });

            if (result.Error != null) {
                Debug.LogError($"[JsonAssetRepository] Verification failed: {result.Error}");
                return;
            }

            if (result.MissingInCache is { Count: > 0 }) {
                foreach (var id in result.MissingInCache) _libraryCache.UpsertAsset(result.OnDisk[id]);
            }
            if (result.MissingOnDisk is { Count: > 0 }) {
                foreach (var id in result.MissingOnDisk) _libraryCache.RemoveAsset(id);
            }
            if (result.Modified is { Count: > 0 }) {
                foreach (var asset in result.Modified) _libraryCache.UpdateAsset(asset);
            }

            if (result.MissingInCache is { Count: > 0 } || 
                result.MissingOnDisk is { Count: > 0 } || 
                result.Modified is { Count: > 0 }) {
                SaveCache();
                Debug.Log("[JsonAssetRepository] Cache updated and verified.");
            }
        }

        public AssetMetadata GetAsset(Ulid assetId) {
            return _libraryCache.GetAsset(assetId);
        }

        public IEnumerable<AssetMetadata> GetAllAssets() {
            return _libraryCache.Assets;
        }

        public LibraryMetadata GetLibraryMetadata() {
            return _libraryCache.Libraries;
        }

        public void CreateAssetFromFile(string sourcePath) {
            var fileInfo = new FileInfo(sourcePath);
            var assetMetadata = new AssetMetadata();
            assetMetadata.SetName(Path.GetFileNameWithoutExtension(fileInfo.Name));
            assetMetadata.SetSize(fileInfo.Length);
            assetMetadata.SetExt(fileInfo.Extension);

            var assetDir = Path.Combine(_assetRootDir, assetMetadata.ID.ToString());

            try {
                Directory.CreateDirectory(assetDir);
                var destPath = Path.Combine(assetDir, fileInfo.Name);
                File.Copy(sourcePath, destPath, true);
                
                SaveAsset(assetMetadata);
            }
            catch (Exception e) {
                Debug.LogError($"[JsonAssetRepository] Failed to create asset: {e.Message}");
                if (!Directory.Exists(assetDir)) throw;
                try { Directory.Delete(assetDir, true); } catch { /* ignore */ }
                throw;
            }
        }

        public AssetMetadata CreateEmptyAsset() {
            var assetMetadata = new AssetMetadata();
            var assetDir = Path.Combine(_assetRootDir, assetMetadata.ID.ToString());
            Directory.CreateDirectory(assetDir);
            SaveAsset(assetMetadata);
            return assetMetadata;
        }

        public void SaveAsset(AssetMetadata asset) {
            var assetDir = Path.Combine(_assetRootDir, asset.ID.ToString());
            if (!Directory.Exists(assetDir)) Directory.CreateDirectory(assetDir);

            var json = JsonConvert.SerializeObject(asset, _serializerSettings);
            var filePath = Path.Combine(assetDir, "metadata.json");
            File.WriteAllText(filePath, json);

            _libraryCache.UpsertAsset(asset);
            SaveCache();
        }

        public void RenameAssetFile(Ulid assetId, string newName) {
            var asset = _libraryCache.GetAsset(assetId);
            if (asset == null) return;

            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            var oldFileName = asset.Name + asset.Ext;
            var newFileName = newName + asset.Ext;
            var oldPath = Path.Combine(assetDir, oldFileName);
            var newPath = Path.Combine(assetDir, newFileName);

            if (File.Exists(oldPath)) {
                File.Move(oldPath, newPath);
            }
        }

        public void DeleteAsset(Ulid assetId) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            if (Directory.Exists(assetDir)) {
                Directory.Delete(assetDir, true);
            }
            _libraryCache.RemoveAsset(assetId);
            SaveCache();
        }

        public void SaveLibraryMetadata(LibraryMetadata libraryMetadata) {
            var json = JsonConvert.SerializeObject(libraryMetadata, _serializerSettings);
            File.WriteAllText(_libraryMetadataPath, json);
            
            _libraryCache.SetLibrary(libraryMetadata);
            SaveCache();
        }

        public void SetThumbnail(Ulid assetId, string imagePath) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            if (!Directory.Exists(assetDir)) return;

            try {
                var fileData = File.ReadAllBytes(imagePath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (tex.LoadImage(fileData)) {
                    var finalTex = TextureUtility.FitImage(tex, 512) ?? tex;
                    var pngBytes = finalTex.EncodeToPNG();
                    var destPath = Path.Combine(assetDir, "thumbnail.png");
                    File.WriteAllBytes(destPath, pngBytes);
                    
                    if (finalTex != tex) Object.DestroyImmediate(finalTex);
                    Object.DestroyImmediate(tex);
                }
                else {
                    Object.DestroyImmediate(tex);
                }
            }
            catch (Exception e) {
                Debug.LogError($"[JsonAssetRepository] Failed to set thumbnail: {e.Message}");
            }
        }

        public void RemoveThumbnail(Ulid assetId) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            var thumbPath = Path.Combine(assetDir, "thumbnail.png");
            if (File.Exists(thumbPath)) {
                File.Delete(thumbPath);
            }
        }

        private void SaveCache() {
            try {
                var cacheData = new LibraryCacheSchema {
                    Metadata = _libraryCache.Libraries,
                    Assets = _libraryCache.Assets.ToList()
                };
                
                var json = JsonConvert.SerializeObject(cacheData, _serializerSettings);
                var tempPath = _cacheFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                
                if (File.Exists(_cacheFilePath)) File.Delete(_cacheFilePath);
                File.Move(tempPath, _cacheFilePath);
            }
            catch (Exception e) {
                Debug.LogWarning($"[JsonAssetRepository] Failed to save cache: {e.Message}");
            }
        }

        private bool LoadCache() {
            if (!File.Exists(_cacheFilePath)) return false;

            try {
                var json = File.ReadAllText(_cacheFilePath);
                var cache = JsonConvert.DeserializeObject<LibraryCacheSchema>(json, _serializerSettings);

                if (cache?.Metadata == null) return false;

                _libraryCache.SetLibrary(cache.Metadata);
                if (cache.Assets != null) {
                    foreach (var asset in cache.Assets) {
                        _libraryCache.UpsertAsset(asset);
                    }
                }
                return true;
            }
            catch {
                return false;
            }
        }

        private bool AreAssetsEqual(AssetMetadata a, AssetMetadata b) {
            if (a == null || b == null) return false;
            var tagsA = new HashSet<string>(a.Tags ?? Enumerable.Empty<string>());
            var tagsB = new HashSet<string>(b.Tags ?? Enumerable.Empty<string>());
            
            return a.Name == b.Name &&
                   a.Size == b.Size &&
                   a.Ext == b.Ext &&
                   a.Folder == b.Folder &&
                   tagsA.SetEquals(tagsB);
        }

        private class LibraryCacheSchema {
            public LibraryMetadata Metadata { get; set; }
            public List<AssetMetadata> Assets { get; set; }
        }
    }
}