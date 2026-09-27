using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class UnityAssetVariantWorkspace : IAssetVariantWorkspace
    {
        private readonly IAssetManager _manager;
        private readonly GitAssetVariantRepository _repository;
        private readonly string _projectRoot;
        private readonly Dictionary<string, (long Length, long ModifiedAt, string Hash)> _fileHashes =
            new Dictionary<string, (long, long, string)>(StringComparer.Ordinal);

        internal UnityAssetVariantWorkspace(IAssetManager manager, GitAssetVariantRepository repository)
        {
            _manager = manager;
            _repository = repository;
            _projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
        }

        public AssetVariantCapture Capture(AssetVariantSaveRequest request)
        {
            return Capture(request, true);
        }

        public AssetVariantSnapshot Inspect(string rootAssetPath)
        {
            return Capture(new AssetVariantSaveRequest { RootAssetPath = rootAssetPath }, false).Snapshot;
        }

        private AssetVariantCapture Capture(AssetVariantSaveRequest request, bool saveVersion)
        {
            GitAssetVariantRepository.ValidateAssetPath(request.RootAssetPath);
            var record = DerivedAssetCatalog.Read(request.RootAssetPath);
            if (record == null || !request.RootAssetPath.StartsWith(
                    DerivedAssetCatalog.VariantRoot + "/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("An ee4v Variant Prefab is required.");
            }
            if (string.IsNullOrEmpty(record.ParentItemId))
            {
                throw new InvalidOperationException("A Variant must belong to an AssetManager asset.");
            }
            _manager.GetItem(record.ParentItemId);
            if (saveVersion) { AssetDatabase.SaveAssets(); }
            var sourcePath = AssetDatabase.GUIDToAssetPath(record.SourceGuid);
            if (!string.IsNullOrEmpty(record.SourceGuid) && !AssetExists(sourcePath))
            {
                throw new InvalidOperationException("The Variant's source Prefab is missing.");
            }
            var rootFolder = Path.GetDirectoryName(request.RootAssetPath).Replace('\\', '/');
            var paths = AssetDatabase.GetDependencies(request.RootAssetPath, true)
                .Append(sourcePath)
                .Concat(AssetDatabase.FindAssets(string.Empty, new[] { rootFolder })
                    .Select(AssetDatabase.GUIDToAssetPath))
                .Append(rootFolder)
                .Where(path => !string.IsNullOrEmpty(path) &&
                    path.StartsWith("Assets/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var associations = _manager.GetImportedAssetAssociations()
                .GroupBy(association => association.AssetGuid)
                .ToDictionary(group => group.Key,
                    group => group.OrderByDescending(association => association.ImportedAt).First(),
                    StringComparer.Ordinal);
            var owned = new List<string>();
            var dependencies = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (path == request.RootAssetPath || path == rootFolder ||
                    !associations.TryGetValue(guid, out var association))
                {
                    owned.Add(path);
                    continue;
                }
                if (!dependencies.TryGetValue(association.FileId, out var guids))
                {
                    guids = new List<string>();
                    dependencies.Add(association.FileId, guids);
                }
                guids.Add(guid);
            }
            var dependencyFiles = dependencies.ToDictionary(pair => pair.Key,
                pair => _manager.GetFile(pair.Key), StringComparer.Ordinal);
            var orderedDependencies = OrderDependencies(dependencyFiles.Keys.ToArray());
            var dependencyRecords = orderedDependencies.Select(fileId =>
            {
                var file = dependencyFiles[fileId];
                if (file.IsArchived || string.IsNullOrEmpty(file.ItemId))
                {
                    throw new InvalidOperationException("A dependency File is archived or unassigned: " + file.FileName);
                }
                return new AssetVariantDependency
                {
                    FileId = file.Id, SourceType = file.SourceType, SourceId = file.SourceId,
                    AssetGuids = dependencies[fileId].Distinct().OrderBy(guid => guid).ToArray(),
                    TargetPaths = ResolveTargetPaths(file)
                };
            }).ToArray();
            var staging = saveVersion ? _repository.NewStagingPath() : null;
            try
            {
                var assets = owned.Select(path => ReadOwnedAsset(path, staging)).ToArray();
                byte[] preview = null;
                if (saveVersion)
                {
                    preview = AssetVariantPreviewRenderer.Render(
                        AssetDatabase.LoadAssetAtPath<GameObject>(request.RootAssetPath));
                    File.WriteAllBytes(Path.Combine(staging, "preview.png"), preview);
                }
                var id = AssetDatabase.AssetPathToGUID(request.RootAssetPath);
                var snapshot = new AssetVariantSnapshot
                {
                    Variant = new AssetVariant
                    {
                        Id = id, SourcePrefabGuid = record.SourceGuid,
                        Name = record.Name, Description = record.Description,
                        ParentItemId = record.ParentItemId, RootAssetPath = request.RootAssetPath
                    },
                    Revision = new AssetVariantRevision
                    {
                        VariantId = id, ParentRevisionId = ReadBaseRevision(id),
                        Memo = request.Memo ?? string.Empty
                    },
                    Assets = assets, Dependencies = dependencyRecords,
                    PreviewHash = preview == null ? null : HashBytes(preview),
                    UnityVersion = UnityEngine.Application.unityVersion,
                    PackagesManifest = ReadProjectFile("Packages/manifest.json"),
                    PackagesLock = ReadProjectFile("Packages/packages-lock.json")
                };
                snapshot.ContentHash = HashBytes(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    snapshot.Variant.Id, snapshot.Variant.SourcePrefabGuid,
                    snapshot.Variant.Name, snapshot.Variant.Description,
                    snapshot.Variant.ParentItemId, snapshot.Variant.RootAssetPath, snapshot.Assets,
                    Dependencies = snapshot.Dependencies.Select(dependency => new
                    {
                        dependency.SourceType, dependency.SourceId,
                        dependency.TargetPaths, dependency.AssetGuids
                    }),
                    snapshot.UnityVersion, snapshot.PackagesManifest, snapshot.PackagesLock
                })));
                return new AssetVariantCapture { Snapshot = snapshot, StagingPath = staging };
            }
            catch
            {
                if (staging != null) { _repository.DeleteStaging(staging); }
                throw;
            }
        }

        private AssetVariantOwnedAsset ReadOwnedAsset(string path, string staging)
        {
            var source = ProjectAssetPath(path);
            var folder = AssetDatabase.IsValidFolder(path);
            var destination = source;
            if (staging != null)
            {
                destination = Path.Combine(staging, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (folder) { Directory.CreateDirectory(destination); }
                else { File.Copy(source, destination); }
                File.Copy(source + ".meta", destination + ".meta");
            }
            else if (!folder && AssetDatabase.IsMainAssetAtPathLoaded(path))
            {
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
            }
            return new AssetVariantOwnedAsset
            {
                Path = path, Guid = AssetDatabase.AssetPathToGUID(path), IsFolder = folder,
                Hash = folder ? null : staging == null ? GetFileHash(destination) : HashFile(destination),
                MetaHash = staging == null ? GetFileHash(destination + ".meta") : HashFile(destination + ".meta")
            };
        }

        private string GetFileHash(string path)
        {
            var file = new FileInfo(path);
            var modifiedAt = file.LastWriteTimeUtc.Ticks;
            if (_fileHashes.TryGetValue(path, out var cached) &&
                cached.Length == file.Length && cached.ModifiedAt == modifiedAt)
            {
                return cached.Hash;
            }
            var hash = HashFile(path);
            _fileHashes[path] = (file.Length, modifiedAt, hash);
            return hash;
        }

        public bool HasAssets(AssetVariantDependency dependency) =>
            dependency.AssetGuids.All(guid => AssetExists(AssetDatabase.GUIDToAssetPath(guid)));

        private bool AssetExists(string path) => !string.IsNullOrEmpty(path) &&
            (File.Exists(Path.Combine(_projectRoot, path)) || Directory.Exists(Path.Combine(_projectRoot, path)));

        public void ValidateRestore(AssetVariantSnapshot snapshot)
        {
            if (snapshot.UnityVersion != UnityEngine.Application.unityVersion ||
                snapshot.PackagesManifest != ReadProjectFile("Packages/manifest.json") ||
                snapshot.PackagesLock != ReadProjectFile("Packages/packages-lock.json"))
            {
                throw new InvalidOperationException(
                    "The saved Variant requires the same Unity version and package configuration.");
            }
            var imported = new HashSet<string>(_manager.GetImportedAssetAssociations()
                .Select(association => association.AssetGuid), StringComparer.Ordinal);
            foreach (var asset in snapshot.Assets)
            {
                var destination = ProjectAssetPath(asset.Path);
                var currentPath = AssetDatabase.GUIDToAssetPath(asset.Guid);
                if (!AssetExists(currentPath)) { currentPath = string.Empty; }
                if (!string.IsNullOrEmpty(currentPath) && currentPath != asset.Path)
                {
                    throw new InvalidOperationException("The saved GUID already exists at another path: " + currentPath);
                }
                var existingGuid = AssetExists(asset.Path)
                    ? AssetDatabase.AssetPathToGUID(asset.Path) : string.Empty;
                if (!string.IsNullOrEmpty(existingGuid) && existingGuid != asset.Guid)
                {
                    throw new InvalidOperationException("Another asset occupies the saved path: " + asset.Path);
                }
                if (imported.Contains(asset.Guid))
                {
                    throw new InvalidOperationException("An imported source asset cannot be overwritten: " + asset.Path);
                }
                if (File.Exists(destination) && string.IsNullOrEmpty(existingGuid))
                {
                    throw new InvalidOperationException("An unimported file occupies the saved path: " + asset.Path);
                }
            }
        }

        public string Restore(AssetVariantSnapshot snapshot, string stagingPath)
        {
            foreach (var asset in snapshot.Assets)
            {
                var source = Path.Combine(stagingPath, asset.Path);
                if (HashFile(source + ".meta") != asset.MetaHash ||
                    (!asset.IsFolder && HashFile(source) != asset.Hash))
                {
                    throw new InvalidDataException("The saved Asset does not match its manifest: " + asset.Path);
                }
            }
            var writes = snapshot.Assets.SelectMany(asset => asset.IsFolder
                    ? new[] { asset.Path + ".meta" }
                    : new[] { asset.Path, asset.Path + ".meta" })
                .ToArray();
            var rootFolder = Path.GetDirectoryName(snapshot.Variant.RootAssetPath).Replace('\\', '/');
            var existing = Directory.Exists(ProjectAssetPath(rootFolder))
                ? Directory.GetFiles(ProjectAssetPath(rootFolder), "*", SearchOption.AllDirectories)
                    .Select(path => path.Substring(_projectRoot.Length + 1).Replace('\\', '/')).ToArray()
                : Array.Empty<string>();
            var protectedGuids = new HashSet<string>(_manager.GetImportedAssetAssociations()
                .Select(association => association.AssetGuid), StringComparer.Ordinal);
            var deletions = existing.Where(path => !writes.Contains(path, StringComparer.Ordinal) &&
                !protectedGuids.Contains(AssetDatabase.AssetPathToGUID(AssetPathForFile(path)))).ToArray();
            var targets = writes.Concat(deletions).Distinct(StringComparer.Ordinal).ToArray();
            var backup = Path.Combine(stagingPath, "backup");
            foreach (var path in targets)
            {
                var destination = ProjectAssetPath(AssetPathForFile(path)) +
                    (path.EndsWith(".meta", StringComparison.Ordinal) ? ".meta" : string.Empty);
                if (File.Exists(destination))
                {
                    var saved = Path.Combine(backup, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved));
                    File.Copy(destination, saved);
                }
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in deletions)
                {
                    var destination = Path.Combine(_projectRoot, path);
                    if (File.Exists(destination)) { File.Delete(destination); }
                }
                foreach (var asset in snapshot.Assets)
                {
                    var destination = ProjectAssetPath(asset.Path);
                    Directory.CreateDirectory(asset.IsFolder ? destination : Path.GetDirectoryName(destination));
                    if (!asset.IsFolder) { File.Copy(Path.Combine(stagingPath, asset.Path), destination, true); }
                    File.Copy(Path.Combine(stagingPath, asset.Path + ".meta"), destination + ".meta", true);
                }
                foreach (var directory in Directory.GetDirectories(ProjectAssetPath(rootFolder), "*",
                             SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }
            }
            catch
            {
                foreach (var path in targets)
                {
                    var saved = Path.Combine(backup, path);
                    var destination = Path.Combine(_projectRoot, path);
                    if (File.Exists(saved))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(saved, destination, true);
                    }
                    else if (File.Exists(destination)) { File.Delete(destination); }
                }
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            return snapshot.Variant.RootAssetPath;
        }

        public void SetBaseRevision(string variantId, string revisionId)
        {
            var path = BaseRevisionPath(variantId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, revisionId, new UTF8Encoding(false));
        }

        private string ReadBaseRevision(string variantId)
        {
            var path = BaseRevisionPath(variantId);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }

        private string BaseRevisionPath(string variantId) =>
            Path.Combine(_projectRoot, "Library", "ee4v", "VariantWorkingCopies", variantId + ".revision");

        private string[] ResolveTargetPaths(AssetFile file)
        {
            if (!string.Equals(file.Extension?.TrimStart('.'), "zip", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { string.Empty };
            }
            var targets = _manager.GetItemTargets(file.ItemId)
                .Where(target => target.FileId == file.Id).Select(target => target.TargetPath).ToArray();
            if (targets.Length == 1) { return targets; }
            var packages = _manager.AnalyzeFile(file.Id).Entries
                .Where(entry => entry.Kind == AssetFileContentEntryKind.File &&
                    entry.Path.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Path).ToArray();
            if (packages.Length == 1) { return packages; }
            throw new InvalidOperationException(
                "The imported target cannot be identified in this ZIP. Configure one Item Target: " + file.FileName);
        }

        private string[] OrderDependencies(string[] fileIds)
        {
            var remaining = new HashSet<string>(fileIds, StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            Action<string> visit = null;
            visit = id =>
            {
                if (!remaining.Contains(id)) { return; }
                if (!visiting.Add(id)) { throw new InvalidOperationException("Variant dependency cycle."); }
                foreach (var dependency in _manager.GetFileDependencies(id)
                             .OrderBy(dependency => dependency.DependencyFileId))
                {
                    visit(dependency.DependencyFileId);
                }
                visiting.Remove(id);
                remaining.Remove(id);
                result.Add(id);
            };
            foreach (var id in fileIds.OrderBy(id => id)) { visit(id); }
            return result.ToArray();
        }

        private string ProjectAssetPath(string path)
        {
            GitAssetVariantRepository.ValidateAssetPath(path);
            var full = Path.GetFullPath(Path.Combine(_projectRoot, path));
            var assetsRoot = Path.GetFullPath(UnityEngine.Application.dataPath) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The asset path is outside Assets.");
            }
            var current = full;
            while (current.Length > _projectRoot.Length)
            {
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Linked Asset paths cannot be saved or restored: " + path);
                }
                current = Path.GetDirectoryName(current);
            }
            return full;
        }

        private static string AssetPathForFile(string path) =>
            path.EndsWith(".meta", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path;

        private string ReadProjectFile(string path)
        {
            var full = Path.Combine(_projectRoot, path);
            return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string HashBytes(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
