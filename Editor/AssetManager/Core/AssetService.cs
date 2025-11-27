using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Booth;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Core {
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
                    var folderName = asset.BoothData.FileName ?? asset.Name ?? identifier ?? I18N.Get("UI.AssetManager.Default.BoothItem");
                    var folderDesc = asset.BoothData.FileName ?? string.Empty;
                    var folderId = _folderService?.EnsureBoothItemFolder(asset.BoothData.ShopDomain ?? string.Empty,
                        null, identifier, folderName, folderDesc) ?? Ulid.Empty;

                    if (folderId != Ulid.Empty && asset.Folder == Ulid.Empty) asset.SetFolder(folderId);
                }
            }
            catch {
                // ignored
            }

            _repository.SaveAsset(asset);
        }

        public void CreateAsset(string path) {
            _repository.CreateAssetFromFile(path);
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
            SaveAsset(newAsset);
        }

        public void RestoreAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.SetDeleted(false);
            SaveAsset(newAsset);
        }

        public void UpdateAsset(AssetMetadata newAsset) {
            if (!AssetValidationService.IsValidAssetName(newAsset.Name)) return;
            var oldAsset = _repository.GetAsset(newAsset.ID);
            if (oldAsset == null) return;

            if (oldAsset.Name != newAsset.Name) _repository.RenameAssetFile(newAsset.ID, newAsset.Name);
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

        public void SetBoothShopDomain(Ulid assetId, string shopURL) {
            if (BoothUtility.ClassifyBoothUrl(shopURL) != BoothUtility.BoothUrlType.ShopUrl) return;

            var regex = new Regex(@"https?://([^\.]+)\.booth\.pm", RegexOptions.IgnoreCase);
            var match = regex.Match(shopURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.BoothData.SetShopDomain(match.Groups[1].Value);
            SaveAsset(newAsset);
        }

        public void SetBoothItemId(Ulid assetId, string itemURL) {
            if (BoothUtility.ClassifyBoothUrl(itemURL) != BoothUtility.BoothUrlType.ItemUrl) return;

            var regex = new Regex(@"items/(\d+)");
            var match = regex.Match(itemURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.BoothData.SetItemID(match.Groups[1].Value);
            SaveAsset(newAsset);
        }

        public void SetBoothDownloadId(Ulid assetId, string downloadURL) {
            if (BoothUtility.ClassifyBoothUrl(downloadURL) != BoothUtility.BoothUrlType.DownloadUrl) return;

            var regex = new Regex(@"downloadables/(\d+)", RegexOptions.IgnoreCase);
            var match = regex.Match(downloadURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.BoothData.SetDownloadID(match.Groups[1].Value);
            SaveAsset(newAsset);
        }

        public void AddAssetGuid(Ulid assetId, Guid guid) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.AddAssetGuid(guid);
            SaveAsset(newAsset);
        }

        public void RemoveAssetGuid(Ulid assetId, Guid guid) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.RemoveAssetGuid(guid);
            SaveAsset(newAsset);
        }

        public void AddDependenceItem(Ulid assetId, Ulid dependenceItemId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.AddDependenceItem(dependenceItemId);
            SaveAsset(newAsset);
        }

        public void RemoveDependenceItem(Ulid assetId, Ulid dependenceItemId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            var newAsset = new AssetMetadata(asset);
            newAsset.UnityData.RemoveDependenceItem(dependenceItemId);
            SaveAsset(newAsset);
        }

        public void ImportFilesFromZip(Ulid assetId, string tempRootPath, List<string> relativePaths) {
            if (relativePaths == null || relativePaths.Count == 0) return;

            _repository.ImportFiles(assetId, tempRootPath, relativePaths);
        }

        public void ImportAsset(Ulid assetId, string destFolder = "Assets") {
            if (_isImporting) {
                Debug.LogWarning(I18N.Get("Debug.AssetManager.Import.AlreadyImporting"));
                return;
            }

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            AssetImportTracker.StartTracking(assetId, _repository);

            _currentDestFolder = destFolder;
            _importQueue.Clear();

            var dependencies = new HashSet<Ulid>();
            CollectDependencies(assetId, dependencies);

            foreach (var depId in dependencies.Where(depId => depId != assetId)) _importQueue.Enqueue(depId);
            _importQueue.Enqueue(assetId);

            _isImporting = true;
            ProcessImportQueue();
        }

        public void ImportAssetList(IEnumerable<Ulid> assetIds, string destFolder = "Assets") {
            if (_isImporting) {
                Debug.LogWarning(I18N.Get("Debug.AssetManager.Import.AlreadyImporting"));
                return;
            }

            var assetIdList = assetIds.ToList();
            if (assetIdList.Count == 0) return;

            AssetImportTracker.StartTracking(assetIdList[0], _repository);

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
                AssetImportTracker.StopTracking();
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

            if (asset.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
                if (_repository.HasImportItems(assetId)) {
                    var importDir = _repository.GetImportDirectoryPath(assetId);

                    var packages = Directory.GetFiles(importDir, "*.unitypackage", SearchOption.AllDirectories);
                    _internalPackageQueue = new Queue<string>(packages);

                    _onInternalBatchComplete = () =>
                    {
                        ImportDirectoryContent(importDir, destFolder);
                        AssetDatabase.Refresh();
                        onComplete?.Invoke();
                    };

                    ProcessInternalPackageQueue();
                }
                else {
                    onComplete?.Invoke();
                }

                return;
            }

            ImportSingleFile(asset, destFolder);
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

        private static void ImportDirectoryContent(string sourceDir, string destRootDir) {
            var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            var filesToProcess = (from file in allFiles
                where !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                where !file.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase)
                where !Path.GetFileName(file).StartsWith(".")
                select file).ToList();

            if (filesToProcess.Count == 0) return;

            var delayedMetaBackups = new List<(string dest, string stored)>();

            foreach (var sourcePath in filesToProcess) {
                var relPath = Path.GetRelativePath(sourceDir, sourcePath);
                var destPath = Path.Combine(destRootDir, relPath);
                var destDir = Path.GetDirectoryName(destPath);

                if (!Directory.Exists(destDir))
                    if (destDir != null)
                        Directory.CreateDirectory(destDir);

                var storedMetaPath = sourcePath + ".meta";
                var destMetaPath = destPath + ".meta";

                File.Copy(sourcePath, destPath, true);

                if (File.Exists(storedMetaPath))
                    File.Copy(storedMetaPath, destMetaPath, true);
                else
                    delayedMetaBackups.Add((destMetaPath, storedMetaPath));
            }

            foreach (var (destMeta, storedMeta) in delayedMetaBackups)
                if (File.Exists(destMeta))
                    File.Copy(destMeta, storedMeta, true);

            Debug.Log(I18N.Get("Debug.AssetManager.Import.ImportedFilesFromImportDirectoryFmt", filesToProcess.Count));
        }

        private static void CopyAndManageMeta(string sourceFile, string destFile, string storedMetaPath) {
            File.Copy(sourceFile, destFile, true);
            var destMetaPath = destFile + ".meta";

            if (File.Exists(storedMetaPath)) {
                File.Copy(storedMetaPath, destMetaPath, true);
                AssetDatabase.Refresh();
                Debug.Log(I18N.Get("Debug.AssetManager.Import.ImportedFileWithRestoredGuidFmt", Path.GetFileName(destFile)));
            }
            else {
                AssetDatabase.Refresh();
                if (!File.Exists(destMetaPath)) return;
                File.Copy(destMetaPath, storedMetaPath, true);
                Debug.Log(I18N.Get("Debug.AssetManager.Import.ImportedFileAndBackedUpMetaFmt", Path.GetFileName(destFile)));
            }
        }
    }

    public class AssetImportTracker : AssetPostprocessor {
        private static Ulid _targetAssetId = Ulid.Empty;
        private static IAssetRepository _repository;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths) {
            if (_targetAssetId == Ulid.Empty || _repository == null) return;

            var asset = _repository.GetAsset(_targetAssetId);
            if (asset == null) return;

            var changed = false;
            foreach (var path in importedAssets.Concat(movedAssets)) {
                var guidStr = AssetDatabase.AssetPathToGUID(path);
                if (!Guid.TryParse(guidStr, out var guid) || asset.UnityData.AssetGuidList.Contains(guid)) continue;
                asset.UnityData.AddAssetGuid(guid);
                changed = true;
            }

            if (changed) _repository.SaveAsset(asset);
        }

        public static void StartTracking(Ulid assetId, IAssetRepository repository) {
            _targetAssetId = assetId;
            _repository = repository;
        }

        public static void StopTracking() {
            _targetAssetId = Ulid.Empty;
            _repository = null;
        }
    }
}