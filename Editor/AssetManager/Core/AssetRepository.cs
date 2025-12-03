using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.Core {
    public class AssetRepository : IAssetRepository {
        private readonly string _assetRootDir;
        private readonly string _cacheFilePath;
        private readonly string _folderIconDir;

        private readonly AssetLibrary _libraryCache;
        private readonly string _libraryMetadataPath;
        private readonly string _rootDir;

        private readonly MetadataSerializer _serializer;

        public AssetRepository(string contentFolderPath)
            : this(contentFolderPath, new MetadataSerializer()) {
        }

        private AssetRepository(string contentFolderPath, MetadataSerializer serializer) {
            _rootDir = Path.Combine(contentFolderPath, "AssetManager");
            _assetRootDir = Path.Combine(_rootDir, "Assets");
            _libraryMetadataPath = Path.Combine(_rootDir, "metadata.json");
            _cacheFilePath = Path.Combine(contentFolderPath, "assetManager_cache.json");
            _folderIconDir = Path.Combine(_rootDir, "FolderIcon");

            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            _libraryCache = new AssetLibrary();
        }

        public event Action LibraryChanged;
        public event Action<Ulid> AssetChanged;
        public event Action<Ulid> FolderChanged;

        public void Initialize() {
            FileSystemProvider.CreateDirectory(_rootDir);
            FileSystemProvider.CreateDirectory(_assetRootDir);
            FileSystemProvider.CreateDirectory(_folderIconDir);

            if (FileSystemProvider.FileExists(_libraryMetadataPath)) return;

            var metadata = new LibraryMetadata();
            SaveLibraryMetadata(metadata);
        }

        public void Load() {
            if (LoadCache()) return;

            if (!FileSystemProvider.FileExists(_libraryMetadataPath)) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.MetadataFileNotFoundFmt", _libraryMetadataPath));
                return;
            }

            try {
                var json = FileSystemProvider.ReadAllText(_libraryMetadataPath);
                var metadata = _serializer.Deserialize<LibraryMetadata>(json);
                _libraryCache.SetLibrary(metadata);

                LoadAllAssetsFromDisk();
                SaveCache();
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.FailedToLoadLibraryFmt", e.Message));
            }
        }

        public async Task<VerificationResult> LoadAndVerifyAsync() {
            var cachedAssets = _libraryCache.Assets.ToDictionary(a => a.ID);

            var result = await Task.Run(() =>
            {
                if (!FileSystemProvider.DirectoryExists(_assetRootDir))
                    return new VerificationResult { Error = "Assets directory does not exist." };

                var onDiskAssets = new Dictionary<Ulid, AssetMetadata>();
                var assetDirs = FileSystemProvider.GetDirectories(_assetRootDir);
                foreach (var assetDir in assetDirs) {
                    var metadataPath = Path.Combine(assetDir, "metadata.json");
                    if (!FileSystemProvider.FileExists(metadataPath)) continue;

                    try {
                        var json = FileSystemProvider.ReadAllText(metadataPath);
                        var assetMetadata = _serializer.Deserialize<AssetMetadata>(json);
                        if (assetMetadata != null) onDiskAssets[assetMetadata.ID] = assetMetadata;
                    }
                    catch {
                        // ignored
                    }
                }

                var missingInCache = onDiskAssets.Keys.Except(cachedAssets.Keys).ToList();
                var missingOnDisk = cachedAssets.Keys.Except(onDiskAssets.Keys).ToList();
                var modified = new List<AssetMetadata>();

                foreach (var kvp in onDiskAssets)
                    if (cachedAssets.TryGetValue(kvp.Key, out var cachedAsset))
                        if (!AreAssetsEqual(cachedAsset, kvp.Value))
                            modified.Add(kvp.Value);

                return new VerificationResult {
                    OnDisk = onDiskAssets,
                    MissingInCache = missingInCache,
                    MissingOnDisk = missingOnDisk,
                    Modified = modified
                };
            });

            return result;
        }

        public void ApplyVerificationResult(VerificationResult result) {
            if (result == null) return;

            var changed = false;

            if (result.MissingInCache is { Count: > 0 }) {
                foreach (var id in result.MissingInCache)
                    if (result.OnDisk.TryGetValue(id, out var asset))
                        _libraryCache.UpsertAsset(asset);
                changed = true;
            }

            if (result.MissingOnDisk is { Count: > 0 }) {
                foreach (var id in result.MissingOnDisk)
                    _libraryCache.RemoveAsset(id);
                changed = true;
            }

            if (result.Modified is { Count: > 0 }) {
                foreach (var asset in result.Modified)
                    _libraryCache.UpdateAsset(asset);
                changed = true;
            }

            if (changed) {
                SaveCache();
                LibraryChanged?.Invoke();
                Debug.Log(I18N.Get("Debug.AssetManager.Repository.CacheUpdatedAndVerified"));
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
                FileSystemProvider.CreateDirectory(assetDir);
                var destPath = Path.Combine(assetDir, fileInfo.Name);
                FileSystemProvider.CopyFile(sourcePath, destPath, true);

                SaveAsset(assetMetadata);
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.FailedToCreateAssetFmt", e.Message));
                if (!Directory.Exists(assetDir)) throw;
                try {
                    Directory.Delete(assetDir, true);
                }
                catch {
                    // ignored
                }

                throw;
            }
        }

        public void AddFileToAsset(Ulid assetId, string sourcePath) {
            var asset = GetAsset(assetId);
            if (asset == null) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.AssetNotFoundFmt", assetId));
                return;
            }

            var fileInfo = new FileInfo(sourcePath);
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());

            try {
                if (!FileSystemProvider.DirectoryExists(assetDir)) FileSystemProvider.CreateDirectory(assetDir);

                var destPath = Path.Combine(assetDir, fileInfo.Name);
                FileSystemProvider.CopyFile(sourcePath, destPath, true);

                var updatedAsset = new AssetMetadata(asset);
                if (updatedAsset.Size == 0) updatedAsset.SetSize(fileInfo.Length);
                if (string.IsNullOrEmpty(updatedAsset.Ext)) updatedAsset.SetExt(fileInfo.Extension);

                SaveAsset(updatedAsset);
                AssetChanged?.Invoke(assetId);
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.FailedToAddFileToAssetFmt", e.Message));
                throw;
            }
        }

        public bool HasAssetFile(Ulid assetId) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            if (!FileSystemProvider.DirectoryExists(assetDir)) return false;

            var files = Directory.GetFiles(assetDir)
                .Where(f => !f.EndsWith("metadata.json") && !f.EndsWith("thumbnail.png"))
                .ToArray();
            return files.Length > 0;
        }

        public AssetMetadata CreateEmptyAsset() {
            var assetMetadata = new AssetMetadata();
            var assetDir = Path.Combine(_assetRootDir, assetMetadata.ID.ToString());
            FileSystemProvider.CreateDirectory(assetDir);
            SaveAsset(assetMetadata);
            return assetMetadata;
        }

        public void SaveAsset(AssetMetadata asset) {
            if (asset == null) return;
            var assetDir = Path.Combine(_assetRootDir, asset.ID.ToString());

            if (!FileSystemProvider.DirectoryExists(assetDir)) FileSystemProvider.CreateDirectory(assetDir);
            var json = _serializer.Serialize(asset);
            var filePath = Path.Combine(assetDir, "metadata.json");
            FileSystemProvider.WriteAllText(filePath, json);

            _libraryCache.UpsertAsset(asset);
            SaveCache();

            try {
                AssetChanged?.Invoke(asset.ID);
            }
            catch {
                // ignored
            }
        }

        public void SaveAssets(IEnumerable<AssetMetadata> assets) {
            if (assets == null) return;

            var anyChanged = false;
            var changedIds = new List<Ulid>();

            foreach (var asset in assets) {
                if (asset == null) continue;

                var assetDir = Path.Combine(_assetRootDir, asset.ID.ToString());
                try {
                    if (!FileSystemProvider.DirectoryExists(assetDir)) FileSystemProvider.CreateDirectory(assetDir);

                    var json = _serializer.Serialize(asset);
                    var filePath = Path.Combine(assetDir, "metadata.json");
                    FileSystemProvider.WriteAllText(filePath, json);

                    _libraryCache.UpsertAsset(asset);
                    anyChanged = true;
                    changedIds.Add(asset.ID);
                }
                catch (Exception e) {
                    Debug.LogWarning(I18N.Get("Debug.AssetManager.Repository.FailedToWriteMetadataForFmt", asset.ID,
                        e.Message));
                }
            }

            if (!anyChanged) return;

            SaveCache();

            try {
                foreach (var id in changedIds) AssetChanged?.Invoke(id);
            }
            catch {
                // ignored
            }
        }

        public void RenameAssetFile(Ulid assetId, string newName) {
            var asset = _libraryCache.GetAsset(assetId);
            if (asset == null) return;

            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            var oldFileName = asset.Name + asset.Ext;
            var newFileName = newName + asset.Ext;
            var oldPath = Path.Combine(assetDir, oldFileName);
            var newPath = Path.Combine(assetDir, newFileName);

            if (FileSystemProvider.FileExists(oldPath)) FileSystemProvider.MoveFile(oldPath, newPath);
        }

        public void DeleteAsset(Ulid assetId) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            if (FileSystemProvider.DirectoryExists(assetDir)) FileSystemProvider.DeleteDirectory(assetDir, true);
            _libraryCache.RemoveAsset(assetId);
            SaveCache();

            try {
                AssetChanged?.Invoke(assetId);
            }
            catch {
                // ignored
            }
        }

        public void SaveLibraryMetadata(LibraryMetadata libraryMetadata) {
            var json = _serializer.Serialize(libraryMetadata);
            FileSystemProvider.WriteAllText(_libraryMetadataPath, json);

            _libraryCache.SetLibrary(libraryMetadata);
            SaveCache();

            try {
                LibraryChanged?.Invoke();
            }
            catch {
                // ignored
            }
        }

        public void SaveFolder(Ulid folderId, bool structureChanged = false) {
            try {
                var json = _serializer.Serialize(_libraryCache.Libraries);
                FileSystemProvider.WriteAllText(_libraryMetadataPath, json);

                SaveCache();
                FolderChanged?.Invoke(folderId);
                if (structureChanged) LibraryChanged?.Invoke();
            }
            catch {
                // ignored
            }
        }

        public void SetThumbnail(Ulid assetId, string imagePath) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            if (!Directory.Exists(assetDir)) return;

            try {
                var fileData = FileSystemProvider.ReadAllBytes(imagePath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (tex.LoadImage(fileData)) {
                    var finalTex = TextureUtility.FitImage(tex, 512) ?? tex;
                    var pngBytes = finalTex.EncodeToPNG();
                    var destPath = Path.Combine(assetDir, "thumbnail.png");
                    FileSystemProvider.WriteAllBytes(destPath, pngBytes);

                    if (finalTex != tex) Object.DestroyImmediate(finalTex);
                    Object.DestroyImmediate(tex);
                }
                else {
                    Object.DestroyImmediate(tex);
                }
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.FailedToSetThumbnailFmt", e.Message));
            }
            finally {
                try {
                    AssetChanged?.Invoke(assetId);
                }
                catch {
                    // ignored
                }
            }
        }

        public void RemoveThumbnail(Ulid assetId) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            var thumbPath = Path.Combine(assetDir, "thumbnail.png");
            if (FileSystemProvider.FileExists(thumbPath)) FileSystemProvider.DeleteFile(thumbPath);

            try {
                AssetChanged?.Invoke(assetId);
            }
            catch {
                // ignored
            }
        }

        public string GetThumbnailPath(Ulid assetId) {
            return Path.Combine(_assetRootDir, assetId.ToString(), "thumbnail.png");
        }

        public Task<byte[]> GetThumbnailDataAsync(Ulid assetId) {
            var path = GetThumbnailPath(assetId);
            return !FileSystemProvider.FileExists(path)
                ? Task.FromResult<byte[]>(null)
                : Task.Run(() => FileSystemProvider.ReadAllBytes(path));
        }

        public void SetFolderThumbnail(Ulid folderId, string imagePath) {
            if (!FileSystemProvider.DirectoryExists(_folderIconDir)) FileSystemProvider.CreateDirectory(_folderIconDir);

            try {
                var fileData = FileSystemProvider.ReadAllBytes(imagePath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (tex.LoadImage(fileData)) {
                    var finalTex = TextureUtility.FitImage(tex, 512) ?? tex;
                    var pngBytes = finalTex.EncodeToPNG();
                    var destPath = Path.Combine(_folderIconDir, $"{folderId}.png");
                    FileSystemProvider.WriteAllBytes(destPath, pngBytes);

                    if (finalTex != tex) Object.DestroyImmediate(finalTex);
                    Object.DestroyImmediate(tex);
                }
                else {
                    Object.DestroyImmediate(tex);
                }
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.FailedToSetFolderThumbnailFmt", e.Message));
            }
            finally {
                try {
                    FolderChanged?.Invoke(folderId);
                }
                catch {
                    // ignored
                }
            }
        }

        public void RemoveFolderThumbnail(Ulid folderId) {
            var path = GetFolderThumbnailPath(folderId);
            if (!FileSystemProvider.FileExists(path)) return;
            try {
                FileSystemProvider.DeleteFile(path);
            }
            catch (Exception e) {
                Debug.LogError(I18N.Get("Debug.AssetManager.Repository.FailedToDeleteFolderThumbnailFmt", e.Message));
            }
            finally {
                try {
                    FolderChanged?.Invoke(folderId);
                }
                catch (Exception) {
                    // ignored
                }
            }
        }

        public string GetFolderThumbnailPath(Ulid folderId) {
            return Path.Combine(_folderIconDir, $"{folderId}.png");
        }

        public Task<byte[]> GetFolderThumbnailDataAsync(Ulid folderId) {
            var path = GetFolderThumbnailPath(folderId);
            return !FileSystemProvider.FileExists(path)
                ? Task.FromResult<byte[]>(null)
                : Task.Run(() => FileSystemProvider.ReadAllBytes(path));
        }

        public void ImportFiles(Ulid assetId, string sourceRootPath, List<string> relativePaths) {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            var importDir = Path.Combine(assetDir, "Import");

            if (FileSystemProvider.DirectoryExists(importDir)) FileSystemProvider.DeleteDirectory(importDir, true);

            FileSystemProvider.CreateDirectory(importDir);

            foreach (var relPath in relativePaths) {
                var srcFile = Path.Combine(sourceRootPath, relPath);

                var destFile = Path.Combine(importDir, Path.GetFileName(relPath));

                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !FileSystemProvider.DirectoryExists(destDir))
                    FileSystemProvider.CreateDirectory(destDir);

                if (FileSystemProvider.FileExists(srcFile)) FileSystemProvider.CopyFile(srcFile, destFile, true);
            }

            try {
                AssetChanged?.Invoke(assetId);
            }
            catch {
                // ignored
            }
        }

        public bool HasImportItems(Ulid assetId) {
            var importDir = GetImportDirectoryPath(assetId);
            return FileSystemProvider.DirectoryExists(importDir) &&
                Directory.EnumerateFileSystemEntries(importDir).Any();
        }

        public string GetImportDirectoryPath(Ulid assetId) {
            return Path.Combine(_assetRootDir, assetId.ToString(), "Import");
        }

        public List<string> GetAllTags() {
            return _libraryCache.GetAllTags();
        }

        public List<string> GetAssetFiles(Ulid assetId, string searchPattern = "*") {
            var assetDir = Path.Combine(_assetRootDir, assetId.ToString());
            if (!FileSystemProvider.DirectoryExists(assetDir)) return new List<string>();

            try {
                var files = Directory.GetFiles(assetDir, searchPattern, SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith("metadata.json") && !f.EndsWith("thumbnail.png"))
                    .ToList();
                return files;
            }
            catch {
                return new List<string>();
            }
        }

        private void LoadAllAssetsFromDisk() {
            if (!FileSystemProvider.DirectoryExists(_assetRootDir)) return;

            var assetDirs = FileSystemProvider.GetDirectories(_assetRootDir);
            foreach (var assetDir in assetDirs) {
                var metadataPath = Path.Combine(assetDir, "metadata.json");
                if (!FileSystemProvider.FileExists(metadataPath)) continue;

                try {
                    var json = FileSystemProvider.ReadAllText(metadataPath);
                    var assetMetadata = _serializer.Deserialize<AssetMetadata>(json);
                    if (assetMetadata != null) _libraryCache.UpsertAsset(assetMetadata);
                }
                catch (Exception e) {
                    Debug.LogError(
                        I18N.Get("Debug.AssetManager.Repository.FailedToLoadAssetMetadataFromFmt", metadataPath,
                            e.Message));
                }
            }
        }

        private void SaveCache() {
            try {
                var cacheData = new LibraryCacheSchema {
                    Metadata = _libraryCache.Libraries,
                    Assets = _libraryCache.Assets.ToList()
                };

                var json = _serializer.Serialize(cacheData);
                var tempPath = _cacheFilePath + ".tmp";
                FileSystemProvider.WriteAllText(tempPath, json);

                if (FileSystemProvider.FileExists(_cacheFilePath)) FileSystemProvider.DeleteFile(_cacheFilePath);
                FileSystemProvider.MoveFile(tempPath, _cacheFilePath);
            }
            catch (Exception e) {
                Debug.LogWarning(I18N.Get("Debug.AssetManager.Repository.FailedToSaveCacheFmt", e.Message));
            }
        }

        private bool LoadCache() {
            if (!FileSystemProvider.FileExists(_cacheFilePath)) return false;

            try {
                var json = FileSystemProvider.ReadAllText(_cacheFilePath);
                var cache = _serializer.Deserialize<LibraryCacheSchema>(json);

                if (cache?.Metadata == null) return false;

                _libraryCache.SetLibrary(cache.Metadata);
                if (cache.Assets == null) return true;
                foreach (var asset in cache.Assets)
                    _libraryCache.UpsertAsset(asset);

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