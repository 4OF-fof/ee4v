using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Services {
    public class AssetService {
        private static AssetDatabase.ImportPackageCallback _currentImportPackageCompletedHandler;
        private static AssetDatabase.ImportPackageCallback _currentImportPackageCancelledHandler;
        private static AssetDatabase.ImportPackageFailedCallback _currentImportPackageFailedHandler;
        private readonly FolderService _folderService;

        private readonly Queue<Ulid> _importQueue = new();
        private readonly IAssetRepository _repository;
        private string _currentDestFolder;

        private Queue<string> _internalPackageQueue = new();
        private bool _isImporting;
        private Action _onInternalBatchComplete;

        public AssetService(IAssetRepository repository, FolderService folderService) {
            _repository = repository;
            _folderService = folderService;
        }

        public void SaveAsset(AssetMetadata asset) {
            if (asset == null) return;

            try {
                var lib = _repository.GetLibraryMetadata();
                if (lib != null && asset.BoothData != null && !string.IsNullOrEmpty(asset.BoothData.ItemId)) {
                    var identifier = asset.BoothData.ItemId;

                    var folderId = _folderService?.EnsureBoothItemFolder(asset.BoothData.ShopDomain ?? string.Empty,
                        asset.BoothData.ShopDomain, identifier, identifier) ?? Ulid.Empty;

                    if (folderId != Ulid.Empty) {
                        if (asset.Folder != folderId) asset.SetFolder(folderId);

                        var siblings = _repository.GetAllAssets()
                            .Where(a => a.ID != asset.ID && a.BoothData?.ItemId == identifier && a.Folder != folderId)
                            .ToList();

                        if (siblings.Count > 0) {
                            var siblingsToSave = new List<AssetMetadata>();
                            foreach (var copy in siblings.Select(sibling => new AssetMetadata(sibling))) {
                                copy.SetFolder(folderId);
                                siblingsToSave.Add(copy);
                            }

                            _repository.SaveAssets(siblingsToSave);
                        }
                    }
                }
            }
            catch {
                // ignored
            }

            _repository.SaveAsset(asset);
        }

        public void AddFileToAsset(Ulid assetId, string path) {
            _repository.AddFileToAsset(assetId, path);

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;
            asset.UnityData.AssetGuidList.Clear();
            _repository.SaveAsset(asset);
        }

        public void DeleteAsset(Ulid assetId) {
            _repository.DeleteAsset(assetId);
        }

        public void RemoveAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDeleted(true);
            newAsset.SetFolder(Ulid.Empty);
            SaveAsset(newAsset);
        }

        public void RestoreAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDeleted(false);
            SaveAsset(newAsset);
        }

        public bool SetAssetName(Ulid assetId, string newName) {
            if (!AssetValidationService.IsValidAssetName(newName)) {
                Debug.LogError("Invalid asset name: cannot set an empty or invalid name.");
                return false;
            }

            var asset = _repository.GetAsset(assetId);
            if (asset == null) {
                Debug.LogError($"Asset not found: {assetId}");
                return false;
            }

            _repository.RenameAssetFile(assetId, newName);

            var newAsset = new AssetMetadata(asset);
            newAsset.SetName(newName);
            SaveAsset(newAsset);
            return true;
        }

        public void SetDescription(Ulid assetId, string newDescription) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDescription(newDescription);
            SaveAsset(newAsset);
        }

        public void SetFolder(Ulid assetId, Ulid newFolder) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetFolder(newFolder);
            SaveAsset(newAsset);
        }

        public void SetFolder(IEnumerable<Ulid> assetIds, Ulid newFolder) {
            var assetsToSave = new List<AssetMetadata>();
            foreach (var assetId in assetIds) {
                var asset = _repository.GetAsset(assetId);
                if (asset == null) continue;

                var newAsset = new AssetMetadata(asset);
                newAsset.SetFolder(newFolder);
                assetsToSave.Add(newAsset);
            }

            if (assetsToSave.Count > 0) _repository.SaveAssets(assetsToSave);
        }

        public void AddTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.AddTag(tag);
            SaveAsset(newAsset);
        }

        public void RemoveTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.RemoveTag(tag);
            SaveAsset(newAsset);
        }

        public void RenameTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(oldTag) || string.IsNullOrEmpty(newTag) || oldTag == newTag) return;

            var assets = _repository.GetAllAssets().ToList();
            var assetsToSave = new List<AssetMetadata>();
            foreach (var asset in assets.Where(a => a.Tags.Contains(oldTag))) {
                var newAsset = new AssetMetadata(asset);
                newAsset.RemoveTag(oldTag);
                newAsset.AddTag(newTag);
                assetsToSave.Add(newAsset);
            }

            if (assetsToSave.Count > 0) _repository.SaveAssets(assetsToSave);

            var lib = _repository.GetLibraryMetadata();
            if (lib == null) return;
            var modifiedFolderIds = new List<Ulid>();
            foreach (var folder in lib.FolderList) RenameFolderTag(folder, oldTag, newTag, modifiedFolderIds);
            if (modifiedFolderIds.Count <= 0) return;
            foreach (var fid in modifiedFolderIds) _repository.SaveFolder(fid);
        }

        public void DeleteTag(string tag) {
            if (string.IsNullOrEmpty(tag)) return;

            var assets = _repository.GetAllAssets().ToList();
            var assetsToSave = new List<AssetMetadata>();
            foreach (var asset in assets.Where(a => a.Tags.Contains(tag))) {
                var newAsset = new AssetMetadata(asset);
                newAsset.RemoveTag(tag);
                assetsToSave.Add(newAsset);
            }

            if (assetsToSave.Count > 0) _repository.SaveAssets(assetsToSave);

            var lib = _repository.GetLibraryMetadata();
            if (lib == null) return;
            var modifiedFolderIds = new List<Ulid>();
            foreach (var folder in lib.FolderList) DeleteFolderTag(folder, tag, modifiedFolderIds);
            if (modifiedFolderIds.Count <= 0) return;
            foreach (var fid in modifiedFolderIds) _repository.SaveFolder(fid);
        }

        private static bool RenameFolderTag(BaseFolder folder, string oldTag, string newTag, List<Ulid> modifiedIds) {
            var modified = false;
            if (folder.Tags.Contains(oldTag)) {
                folder.RemoveTag(oldTag);
                folder.AddTag(newTag);
                modified = true;
                modifiedIds?.Add(folder.ID);
            }

            if (folder is not Folder f || f.Children == null) return modified;
            foreach (var child in f.Children)
                if (RenameFolderTag(child, oldTag, newTag, modifiedIds))
                    modified = true;
            return modified;
        }

        private static bool DeleteFolderTag(BaseFolder folder, string tag, List<Ulid> modifiedIds) {
            var modified = false;
            if (folder.Tags.Contains(tag)) {
                folder.RemoveTag(tag);
                modified = true;
                modifiedIds?.Add(folder.ID);
            }

            if (folder is not Folder f || f.Children == null) return modified;
            foreach (var child in f.Children)
                if (DeleteFolderTag(child, tag, modifiedIds))
                    modified = true;
            return modified;
        }

        public void ImportFilesFromZip(Ulid assetId, string tempRootPath, List<string> relativePaths) {
            if (relativePaths == null || relativePaths.Count == 0) return;

            _repository.ImportFiles(assetId, tempRootPath, relativePaths);
        }

        public void ImportAssetList(IEnumerable<Ulid> assetIds, string destFolder = "Assets") {
            if (_isImporting) {
                Debug.LogWarning(I18N.Get("Debug.AssetManager.Import.AlreadyImporting"));
                return;
            }

            var assetIdList = assetIds.ToList();
            if (assetIdList.Count == 0) return;

            _currentDestFolder = destFolder;
            _importQueue.Clear();

            var allDependencies = new HashSet<Ulid>();
            var targetIds = new HashSet<Ulid>(assetIdList);

            foreach (var assetId in assetIdList) CollectDependencies(assetId, allDependencies);

            foreach (var depId in allDependencies.Where(depId => !targetIds.Contains(depId)))
                _importQueue.Enqueue(depId);

            foreach (var assetId in assetIdList) _importQueue.Enqueue(assetId);

            _isImporting = true;
            ProcessImportQueue();
        }

        private void CollectDependencies(Ulid assetId, HashSet<Ulid> visited) {
            if (!visited.Add(assetId)) return;

            var asset = _repository.GetAsset(assetId);

            if (asset?.UnityData?.DependenceItemList == null) return;
            foreach (var depId in asset.UnityData.DependenceItemList) CollectDependencies(depId, visited);
        }

        private void ProcessImportQueue() {
            if (_importQueue.Count == 0) {
                _isImporting = false;
                Debug.Log(I18N.Get("Debug.AssetManager.Import.Completed"));
                return;
            }

            var nextAssetId = _importQueue.Dequeue();
            ImportAssetInternal(nextAssetId, _currentDestFolder,
                () => { EditorApplication.delayCall += ProcessImportQueue; });
        }

        private void ImportAssetInternal(Ulid assetId, string destFolder, Action onComplete) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) {
                onComplete?.Invoke();
                return;
            }

            Debug.Log(I18N.Get("Debug.AssetManager.Import.ImportingAssetFmt", asset.Name, asset.ID));

            if (asset.Ext.Equals(".unitypackage", StringComparison.OrdinalIgnoreCase)) {
                var files = _repository.GetAssetFiles(assetId, "*.unitypackage");
                if (files.Count > 0) {
                    RegisterPackageEvents(onComplete);
                    AssetDatabase.ImportPackage(files[0], true);
                }
                else {
                    onComplete?.Invoke();
                }

                return;
            }

            var targetFolder = Path.Combine(destFolder, asset.Name);

            if (asset.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
                if (_repository.HasImportItems(assetId)) {
                    var importDir = _repository.GetImportDirectoryPath(assetId);

                    var packages = Directory.GetFiles(importDir, "*.unitypackage", SearchOption.AllDirectories);
                    _internalPackageQueue = new Queue<string>(packages);

                    _onInternalBatchComplete = () =>
                    {
                        var delayedBackups = ImportDirectoryContent(importDir, targetFolder);

                        AssetDatabase.Refresh();

                        foreach (var (destMeta, storedMeta) in delayedBackups)
                            if (File.Exists(destMeta))
                                try {
                                    File.Copy(destMeta, storedMeta, true);
                                    Debug.Log($"Backed up meta file: {Path.GetFileName(destMeta)}");
                                }
                                catch (Exception e) {
                                    Debug.LogWarning($"Failed to backup meta file {destMeta}: {e.Message}");
                                }

                        onComplete?.Invoke();
                    };

                    ProcessInternalPackageQueue();
                }
                else {
                    onComplete?.Invoke();
                }

                return;
            }

            ImportSingleFile(asset, targetFolder);
            AssetDatabase.Refresh();
            onComplete?.Invoke();
        }

        private void ProcessInternalPackageQueue() {
            if (_internalPackageQueue.Count == 0) {
                _onInternalBatchComplete?.Invoke();
                _onInternalBatchComplete = null;
                return;
            }

            var pkgPath = _internalPackageQueue.Dequeue();
            RegisterPackageEvents(() => EditorApplication.delayCall += ProcessInternalPackageQueue);
            AssetDatabase.ImportPackage(pkgPath, true);
        }

        private static void RegisterPackageEvents(Action onComplete) {
            UnregisterPackageEvents();

            _currentImportPackageCompletedHandler = OnCompleted;
            _currentImportPackageCancelledHandler = OnCancelled;
            _currentImportPackageFailedHandler = OnFailed;

            AssetDatabase.importPackageCompleted += _currentImportPackageCompletedHandler;
            AssetDatabase.importPackageCancelled += _currentImportPackageCancelledHandler;
            AssetDatabase.importPackageFailed += _currentImportPackageFailedHandler;
            return;

            void OnCompleted(string name) {
                UnregisterPackageEvents();
                onComplete?.Invoke();
            }

            void OnFailed(string name, string error) {
                UnregisterPackageEvents();
                Debug.LogError($"Import failed: {name}, Error: {error}");
                onComplete?.Invoke();
            }

            void OnCancelled(string name) {
                UnregisterPackageEvents();
                Debug.LogWarning($"Import cancelled: {name}");
                onComplete?.Invoke();
            }
        }

        private static void UnregisterPackageEvents() {
            if (_currentImportPackageCompletedHandler != null)
                AssetDatabase.importPackageCompleted -= _currentImportPackageCompletedHandler;

            if (_currentImportPackageCancelledHandler != null)
                AssetDatabase.importPackageCancelled -= _currentImportPackageCancelledHandler;

            if (_currentImportPackageFailedHandler != null)
                AssetDatabase.importPackageFailed -= _currentImportPackageFailedHandler;

            _currentImportPackageCompletedHandler = null;
            _currentImportPackageCancelledHandler = null;
            _currentImportPackageFailedHandler = null;
        }

        private void ImportSingleFile(AssetMetadata asset, string destFolder) {
            var assetFiles = _repository.GetAssetFiles(asset.ID);
            var mainFile = assetFiles.FirstOrDefault(f =>
                !f.EndsWith("metadata.json") &&
                !f.EndsWith("thumbnail.png") &&
                !f.Contains(Path.DirectorySeparatorChar + "Import" + Path.DirectorySeparatorChar));

            if (string.IsNullOrEmpty(mainFile) || !File.Exists(mainFile)) {
                Debug.LogError($"Asset file not found for {asset.Name}");
                return;
            }

            if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

            var fileName = Path.GetFileName(mainFile);
            var destPath = Path.Combine(destFolder, fileName);

            var repoImportDir = _repository.GetImportDirectoryPath(asset.ID);
            if (!Directory.Exists(repoImportDir)) Directory.CreateDirectory(repoImportDir);

            var storedMetaPath = Path.Combine(repoImportDir, fileName + ".meta");

            CopyAndManageMeta(mainFile, destPath, storedMetaPath);
        }

        private static List<(string dest, string stored)> ImportDirectoryContent(string sourceDir, string destRootDir) {
            var delayedMetaBackups = new List<(string dest, string stored)>();

            var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            var filesToProcess = (from file in allFiles
                where !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                where !file.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase)
                where !Path.GetFileName(file).StartsWith(".")
                select file).ToList();

            if (filesToProcess.Count == 0) return delayedMetaBackups;

            try {
                AssetDatabase.StartAssetEditing();

                foreach (var sourcePath in filesToProcess) {
                    var relPath = Path.GetRelativePath(sourceDir, sourcePath);
                    var destPath = Path.Combine(destRootDir, relPath);
                    var destDir = Path.GetDirectoryName(destPath);

                    if (!Directory.Exists(destDir) && destDir != null)
                        Directory.CreateDirectory(destDir);

                    var storedMetaPath = sourcePath + ".meta";
                    var destMetaPath = destPath + ".meta";

                    File.Copy(sourcePath, destPath, true);

                    if (File.Exists(storedMetaPath))
                        try {
                            File.Copy(storedMetaPath, destMetaPath, true);
                        }
                        catch (Exception e) {
                            Debug.LogWarning($"Failed to copy meta file {storedMetaPath} to {destMetaPath}: {e.Message}");
                        }
                    else
                        delayedMetaBackups.Add((destMetaPath, storedMetaPath));
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log(I18N.Get("Debug.AssetManager.Import.ImportedFilesFromImportDirectoryFmt", filesToProcess.Count));
            return delayedMetaBackups;
        }

        private static void CopyAndManageMeta(string sourceFile, string destFile, string storedMetaPath) {
            File.Copy(sourceFile, destFile, true);
            var destMetaPath = destFile + ".meta";

            if (File.Exists(storedMetaPath)) {
                File.Copy(storedMetaPath, destMetaPath, true);
                AssetDatabase.Refresh();
                Debug.Log(I18N.Get("Debug.AssetManager.Import.ImportedFileWithRestoredGuidFmt",
                    Path.GetFileName(destFile)));
            }
            else {
                AssetDatabase.Refresh();
                if (!File.Exists(destMetaPath)) return;
                File.Copy(destMetaPath, storedMetaPath, true);
                Debug.Log(I18N.Get("Debug.AssetManager.Import.ImportedFileAndBackedUpMetaFmt",
                    Path.GetFileName(destFile)));
            }
        }

        public void UpdateAssetGuids(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            asset.UnityData.AssetGuidList.Clear();

            if (asset.Ext.Equals(".unitypackage", StringComparison.OrdinalIgnoreCase)) {
                var packageFiles = _repository.GetAssetFiles(assetId, "*.unitypackage");
                if (packageFiles.Count > 0) {
                    var guids = ExtractGuidsFromPackage(packageFiles[0]);
                    foreach (var guid in guids)
                        if (Guid.TryParse(guid, out var g) && !asset.UnityData.AssetGuidList.Contains(g))
                            asset.UnityData.AddAssetGuid(g);
                }
            }

            if (_repository.HasImportItems(assetId)) {
                var importDir = _repository.GetImportDirectoryPath(assetId);

                var allFiles = Directory.GetFiles(importDir, "*", SearchOption.AllDirectories);

                foreach (var file in allFiles)
                    if (file.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase)) {
                        var guids = ExtractGuidsFromPackage(file);
                        foreach (var guid in guids)
                            if (Guid.TryParse(guid, out var g) && !asset.UnityData.AssetGuidList.Contains(g))
                                asset.UnityData.AddAssetGuid(g);
                    }
                    else if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) {
                        var guid = ExtractGuidFromMeta(file);
                        if (!string.IsNullOrEmpty(guid) && Guid.TryParse(guid, out var g) &&
                            !asset.UnityData.AssetGuidList.Contains(g))
                            asset.UnityData.AddAssetGuid(g);
                    }
            }

            _repository.SaveAsset(asset);
            Debug.Log($"[ee4v] Updated GUIDs for asset '{asset.Name}'. Count: {asset.UnityData.AssetGuidList.Count}");
        }

        private static List<string> ExtractGuidsFromPackage(string packagePath) {
            var guids = new List<string>();
            try {
                using var fs = File.OpenRead(packagePath);
                using var gzip = new GZipStream(fs, CompressionMode.Decompress);
                using var tar = new BinaryReader(gzip);

                var buffer = new byte[512];
                while (true) {
                    var bytesRead = tar.Read(buffer, 0, 512);
                    if (bytesRead < 512) break;

                    var name = Encoding.ASCII.GetString(buffer, 0, 100).TrimEnd('\0');
                    if (string.IsNullOrEmpty(name)) break;

                    var sizeStr = Encoding.ASCII.GetString(buffer, 124, 12).TrimEnd('\0');
                    var size = Convert.ToInt64(sizeStr, 8);

                    if (name.EndsWith("/pathname")) {
                        var guid = name.Split('/')[0];
                        if (IsValidGuid(guid)) guids.Add(guid);
                    }

                    var chunks = (size + 511) / 512;
                    for (var i = 0; i < chunks; i++) _ = tar.Read(buffer, 0, 512);
                }
            }
            catch (Exception e) {
                Debug.LogWarning($"[ee4v] Failed to extract GUIDs from package '{packagePath}': {e.Message}");
            }

            return guids;
        }

        private static string ExtractGuidFromMeta(string metaPath) {
            try {
                foreach (var line in File.ReadLines(metaPath)) {
                    if (!line.StartsWith("guid:")) continue;
                    var parts = line.Split(' ');
                    if (parts.Length >= 2 && IsValidGuid(parts[1])) return parts[1];
                }
            }
            catch {
                // ignore
            }

            return null;
        }

        private static bool IsValidGuid(string guid) {
            return !string.IsNullOrEmpty(guid) && guid.Length == 32 && guid.All(IsHex);
        }

        private static bool IsHex(char c) {
            return c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
        }
    }
}