using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetLibrary;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.Persistence;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Ee4v
{
    internal sealed class Ee4vExpressionLibrary : IExpressionLibrary
    {
        private readonly string _root;
        private readonly string _projectKey;
        private readonly SqliteAssetManagerStore _store;
        private readonly IAssetManager _manager;
        private readonly AssetTargetImporter _importer;
        private readonly Dictionary<string, Task<string>> _imports = new Dictionary<string, Task<string>>();
        private readonly Dictionary<string, CachedEntry> _reads = new Dictionary<string, CachedEntry>();
        private readonly Dictionary<string, (string Stamp, string Hash)> _previewHashes = new Dictionary<string, (string, string)>();
        private readonly Dictionary<string, string> _previewPaths = new Dictionary<string, string>();
        private IReadOnlyList<ExpressionLibraryEntry> _entries;
        private Dictionary<string, ExpressionProjectLocation> _projectLocations;

        private sealed class CachedEntry
        {
            internal ExpressionLibraryEntry Entry;
            internal Dictionary<string, string> Stamps;
        }

        internal Ee4vExpressionLibrary(string databasePath, SqliteAssetManagerStore store, IAssetManager manager)
        {
            var library = Path.GetDirectoryName(Path.GetFullPath(databasePath));
            _root = Path.Combine(library, "Assets", "FaceExpression");
            _projectKey = Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(UnityEngine.Application.dataPath).ToUpperInvariant()));
            _store = store;
            _manager = manager;
            _importer = new AssetTargetImporter(library);
        }

        public event Action Changed;
        public IReadOnlyList<ExpressionLibraryEntry> List(string primaryFbxGuid = null)
        {
            var entries = _entries ?? _store.GetExpressions();
            return string.IsNullOrEmpty(primaryFbxGuid) ? entries : entries.Where(entry => entry.PrimaryFbxGuid == primaryFbxGuid).ToArray();
        }
        public ExpressionLibraryEntry FindByRootGuid(string guid) => List().FirstOrDefault(entry => entry.RootGuid == guid);
        public string GetWorkingRevision(string guid) => _store.GetExpressionImportRevision(_projectKey, guid);
        public IReadOnlyList<ExpressionProjectLocation> GetProjectLocations()
        {
            if (_projectLocations == null)
                _projectLocations = _store.GetExpressionProjectLocations(_projectKey).ToDictionary(value => value.RootGuid);
            return _projectLocations.Values.ToArray();
        }

        public void Synchronize()
        {
            RejectLinks(_root);
            var entries = new List<ExpressionLibraryEntry>();
            if (Directory.Exists(_root))
                foreach (var directory in Directory.GetDirectories(_root).OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
                    if (!File.Exists(Path.Combine(directory, "metadata.json")) &&
                        !List().Any(entry => entry.Id == Path.GetFileName(directory))) continue;
                    entries.Add(Read(Path.GetFileName(directory)));
                }
            if (entries.Select(entry => entry.RootGuid).Distinct().Count() != entries.Count)
                throw new InvalidDataException("Duplicate expression Clip GUIDs exist in the shared library.");
            // Validate the complete source snapshot before changing the index or its visible cache.
            var changed = _entries == null || _entries.Count != entries.Count ||
                !_entries.OrderBy(entry => entry.Id).Select(JsonConvert.SerializeObject)
                    .SequenceEqual(entries.OrderBy(entry => entry.Id).Select(JsonConvert.SerializeObject));
            if (changed)
            {
                _store.ReplaceExpressions(entries);
                CacheEntries(entries);
            }
            var locationsChanged = SynchronizeProjectLocations(entries);
            if (changed || locationsChanged) NotifyChanged();
        }

        private bool SynchronizeProjectLocations(IEnumerable<ExpressionLibraryEntry> entries)
        {
            GetProjectLocations();
            var updates = new List<ExpressionProjectLocation>();
            var ids = new HashSet<string>();
            foreach (var entry in entries)
            {
                ids.Add(entry.Id);
                _projectLocations.TryGetValue(entry.RootGuid, out var previous);
                var path = AssetDatabase.GUIDToAssetPath(entry.RootGuid);
                var tracked = previous?.ExpressionId == entry.Id && !string.IsNullOrEmpty(previous.OriginalFolder) &&
                    (previous.OriginalFolder == "Assets" || previous.OriginalFolder.StartsWith("Assets/", StringComparison.Ordinal));
                if (tracked && previous.AssetPath == path) continue;
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) continue;
                var absolute = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), path);
                if (!File.Exists(absolute) || !File.Exists(absolute + ".meta") ||
                    AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets) != entry.RootGuid) continue;
                var directory = Path.GetDirectoryName(path).Replace('\\', '/');
                updates.Add(new ExpressionProjectLocation { ExpressionId = entry.Id, RootGuid = entry.RootGuid,
                    AssetPath = path, OriginalFolder = tracked ? previous.OriginalFolder : directory });
            }
            if (updates.Count > 0) _store.SaveExpressionProjectLocations(_projectKey, updates);
            foreach (var location in updates) _projectLocations[location.RootGuid] = location;
            foreach (var guid in _projectLocations.Where(pair => !ids.Contains(pair.Value.ExpressionId)).Select(pair => pair.Key).ToArray())
                _projectLocations.Remove(guid);
            return updates.Count > 0;
        }

        public ExpressionLibraryEntry Publish(ExpressionPublishRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrEmpty(request.PrimaryFbxGuid) || request.Targets == null)
                throw new ArgumentException("Expression name and FBX targets are required.");
            var path = request.AssetPath;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) || AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null)
                throw new ArgumentException("A saved AnimationClip in Assets is required.");
            ValidateFolder(request.Folder ?? string.Empty);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (_manager.GetImportedAssetAssociations(new[] { guid }).Count > 0)
                throw new InvalidOperationException("Copy the imported source Clip before saving it as an editable shared expression.");
            var lockPath = SafePath(_root, ".locks/" + guid);
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath));
            using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Synchronize();
                var previous = FindByRootGuid(guid);
                if (previous != null && previous.Revision != request.ExpectedRevision)
                    throw new InvalidOperationException("The shared expression changed. Reload or save a copy before publishing.");
                var id = previous?.Id ?? Guid.NewGuid().ToString("N");
                var entryDirectory = SafePath(_root, id);
                if (previous != null && Read(id, true).Revision != request.ExpectedRevision)
                    throw new InvalidOperationException("The shared expression changed while preparing the save.");
                Directory.CreateDirectory(entryDirectory);
                var stage = Path.Combine(entryDirectory, ".tmp-" + Guid.NewGuid().ToString("N"));
                string createdContent = null;
                Directory.CreateDirectory(stage);
                try
                {
                    var assets = new List<ExpressionAsset>();
                    var dependencies = new List<ExpressionDependency>();
                    var associations = _manager.GetImportedAssetAssociations()
                        .GroupBy(value => value.AssetGuid).ToDictionary(group => group.Key,
                            group => group.OrderByDescending(value => value.ImportedAt).First());
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var visiting = new HashSet<string>(StringComparer.Ordinal);
                    void Capture(string assetPath, string dependentGuid = null)
                    {
                        var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                        if (visiting.Contains(assetGuid)) throw new InvalidDataException("Expression references contain a cycle.");
                        if (assetPath != path && associations.TryGetValue(assetGuid, out var association))
                        {
                            var file = _manager.GetFile(association.FileId);
                            var targets = file.Extension == "zip"
                                ? _manager.GetItemTargets(file.ItemId).Where(target => target.FileId == file.Id).Select(target => target.TargetPath).ToArray()
                                : new[] { string.Empty };
                            if (file.IsArchived || string.IsNullOrEmpty(file.ItemId) || targets.Length != 1)
                                throw new InvalidOperationException("The referenced source Clip has no unambiguous import target.");
                            dependencies.Add(new ExpressionDependency { DependentGuid = dependentGuid, AssetGuid = assetGuid,
                                SourceType = file.SourceType.ToString(), SourceId = file.SourceId, TargetPaths = targets });
                            return;
                        }
                        if (dependentGuid != null)
                            dependencies.Add(new ExpressionDependency { DependentGuid = dependentGuid, AssetGuid = assetGuid });
                        if (!visited.Add(assetGuid)) return;
                        visiting.Add(assetGuid);
                        if (!assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("An unregistered referenced Clip must be a standalone .anim asset.");
                        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath)) AssetDatabase.SaveAssetIfDirty(asset);
                        var source = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), assetPath);
                        RejectLinks(source);
                        if (!File.Exists(source + ".meta") || AssetIdentity.ReadGuid(File.ReadAllText(source + ".meta")) != assetGuid)
                            throw new InvalidDataException("The expression Clip has no valid Unity metadata.");
                        var relative = assetPath == path ? Path.GetFileName(assetPath) : "Dependencies/" + assetGuid + "/" + Path.GetFileName(assetPath);
                        var destination = SafePath(stage, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(source, destination);
                        File.Copy(source + ".meta", destination + ".meta");
                        assets.Add(new ExpressionAsset { Guid = assetGuid, Path = relative,
                            Hash = AssetIdentity.HashFile(destination), MetaHash = AssetIdentity.HashFile(destination + ".meta") });
                        foreach (var reference in AssetDatabase.GetDependencies(assetPath, false)
                                     .Where(reference => reference != assetPath && AssetDatabase.LoadAssetAtPath<AnimationClip>(reference) != null))
                            Capture(reference, assetGuid);
                        visiting.Remove(assetGuid);
                    }
                    Capture(path);
                    var entry = new ExpressionLibraryEntry { Id = id, Name = request.Name, Folder = request.Folder ?? string.Empty,
                        RootGuid = guid, PrimaryFbxGuid = request.PrimaryFbxGuid,
                        Targets = request.Targets.OrderBy(target => target.RendererPath, StringComparer.Ordinal)
                            .ThenBy(target => target.ShapeName, StringComparer.Ordinal).ToArray(),
                        Assets = assets.OrderBy(asset => asset.Guid, StringComparer.Ordinal).ToArray(),
                        Dependencies = dependencies.OrderBy(value => value.DependentGuid, StringComparer.Ordinal)
                            .ThenBy(value => value.AssetGuid, StringComparer.Ordinal).ToArray() };
                    entry.Revision = ContentRevision(entry);
                    entry.ContentDirectory = "content/" + entry.Revision.Substring(0, 16);
                    if (request.Preview?.Length > 0)
                    {
                        File.WriteAllBytes(Path.Combine(stage, "preview.png"), request.Preview);
                        entry.PreviewHash = Hash(request.Preview);
                    }
                    var content = SafePath(entryDirectory, entry.ContentDirectory);
                    if (!Directory.Exists(content))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(content));
                        Directory.CreateDirectory(content);
                        createdContent = content;
                        foreach (var source in Directory.GetFiles(stage, "*", SearchOption.AllDirectories))
                        {
                            var destination = SafePath(content, source.Substring(stage.Length + 1).Replace('\\', '/'));
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            File.Copy(source, destination);
                        }
                    }
                    else
                    {
                        foreach (var asset in entry.Assets)
                        {
                            var existing = SafePath(content, asset.Path);
                            if (!File.Exists(existing) || AssetIdentity.HashFile(existing) != asset.Hash ||
                                !File.Exists(existing + ".meta") || AssetIdentity.HashFile(existing + ".meta") != asset.MetaHash)
                                throw new InvalidDataException("The expression content directory conflicts with the new save.");
                        }
                        entry.PreviewHash = previous?.Revision == entry.Revision ? previous.PreviewHash : null;
                    }
                    if (previous?.Revision != entry.Revision) WriteMetadata(entryDirectory, entry);
                    else entry = previous;
                    createdContent = null;
                    _store.SaveExpression(entry);
                    CacheEntries(List().Where(value => value.Id != entry.Id).Append(entry));
                    _reads.Remove(id);
                    var hashes = entry.Assets.ToDictionary(asset => asset.Guid,
                        asset => AssetIdentity.SnapshotHash(Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath),
                            AssetDatabase.GUIDToAssetPath(asset.Guid))));
                    _store.RecordExpressionImports(_projectKey, entry, hashes);
                    SynchronizeProjectLocations(List());
                    NotifyChanged();
                    return entry;
                }
                finally
                {
                    if (Directory.Exists(stage)) Directory.Delete(stage, true);
                    if (createdContent != null && Directory.Exists(createdContent)) Directory.Delete(createdContent, true);
                    if (previous == null && Directory.Exists(entryDirectory) &&
                        !File.Exists(Path.Combine(entryDirectory, "metadata.json"))) Directory.Delete(entryDirectory, true);
                }
            }
        }

        public async Task Delete(string id, string expectedRevision, CancellationToken cancellationToken = default)
        {
            using (await AssetTargetImporter.LockImports(cancellationToken))
            {
                var entry = Read(id, true);
                var lockPath = SafePath(_root, ".locks/" + entry.RootGuid);
                Directory.CreateDirectory(Path.GetDirectoryName(lockPath));
                using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    entry = Read(id, true);
                    if (entry.Revision != expectedRevision)
                        throw new InvalidOperationException("The selected expression changed. Reopen the library before deleting it.");
                    var assetPath = AssetDatabase.GUIDToAssetPath(entry.RootGuid);
                    string absolute = null;
                    byte[] body = null, meta = null;
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                            !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Only standalone project expression Clips can be deleted.");
                        var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), assetPath));
                        RejectLinks(candidate);
                        if (File.Exists(candidate) && File.Exists(candidate + ".meta") &&
                            AssetIdentity.ReadGuid(File.ReadAllText(candidate + ".meta")) == entry.RootGuid)
                        {
                            absolute = candidate;
                            body = File.ReadAllBytes(absolute);
                            meta = File.ReadAllBytes(absolute + ".meta");
                        }
                    }
                    var directory = SafePath(_root, id);
                    var removed = SafePath(_root, ".deleted/" + id + "-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(Path.GetDirectoryName(removed));
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.Move(directory, removed);
                    try
                    {
                        if (absolute != null && !AssetDatabase.DeleteAsset(assetPath))
                            throw new IOException("The project expression Clip could not be deleted.");
                        _store.DeleteExpression(id);
                        CacheEntries(List().Where(value => value.Id != id));
                        _projectLocations?.Remove(entry.RootGuid);
                        _reads.Remove(id);
                    }
                    catch
                    {
                        Directory.Move(removed, directory);
                        if (absolute != null)
                        {
                            File.WriteAllBytes(absolute, body);
                            File.WriteAllBytes(absolute + ".meta", meta);
                            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        }
                        throw;
                    }
                    try { Directory.Delete(removed, true); }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    { Debug.LogWarning("The deleted expression's temporary files could not be removed: " + exception.Message); }
                    NotifyChanged();
                }
            }
        }

        public Task<string> EnsureImported(string id, string expectedRevision, string destinationFolder,
            CancellationToken cancellationToken = default)
        {
            var key = id + ":" + expectedRevision;
            if (_imports.TryGetValue(key, out var pending)) return pending;
            var task = Import(id, expectedRevision, destinationFolder, cancellationToken);
            _imports[key] = task;
            return RemovePending(key, task);
        }

        private async Task<string> RemovePending(string key, Task<string> task)
        {
            try { return await task; }
            finally { _imports.Remove(key); }
        }

        private async Task<string> Import(string id, string expectedRevision, string destinationFolder, CancellationToken cancellationToken)
        {
            var entry = Read(id, true);
            if (entry.Revision != expectedRevision) throw new InvalidOperationException("The selected expression changed. Reload the library.");
            foreach (var dependency in entry.Dependencies.Where(value => !string.IsNullOrEmpty(value.SourceId)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(dependency.AssetGuid))) continue;
                var files = _manager.SearchItems(new AssetItemQuery { IncludeArchived = true }).Items.SelectMany(item => item.Files);
                var file = files.FirstOrDefault(value => value.SourceId == dependency.SourceId && value.SourceType.ToString() == dependency.SourceType);
                if (file == null || file.IsArchived || string.IsNullOrEmpty(file.ItemId))
                    throw new InvalidOperationException("A referenced source Clip is unavailable. Synchronize its datasource first.");
                var result = await _manager.ImportFileEntries(file.Id, dependency.TargetPaths, cancellationToken);
                if (result.State != AssetImportState.Success) throw new InvalidOperationException("A referenced source Clip could not be imported.");
            }
            var destinations = await _importer.ImportExpressionAssets(entry, SafePath(SafePath(_root, id), entry.ContentDirectory),
                destinationFolder, guid => _store.GetExpressionImportHash(_projectKey, id, guid), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Read(id, true).Revision != expectedRevision) throw new InvalidOperationException("The shared expression changed during import.");
            foreach (var dependency in entry.Dependencies)
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(dependency.AssetGuid)))
                    throw new InvalidDataException("An expression reference is missing after import.");
            var rootPath = destinations[entry.RootGuid];
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(rootPath) == null) throw new InvalidDataException("The imported expression is not an AnimationClip.");
            _store.RecordExpressionImports(_projectKey, entry, destinations.ToDictionary(pair => pair.Key,
                pair => AssetIdentity.SnapshotHash(Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), pair.Value))));
            SynchronizeProjectLocations(List());
            return rootPath;
        }

        public string GetPreviewPath(string id)
        {
            var entry = List().FirstOrDefault(value => value.Id == id);
            if (entry == null || string.IsNullOrEmpty(entry.PreviewHash)) return null;
            var key = id + ":" + entry.ContentDirectory;
            if (!_previewPaths.TryGetValue(key, out var path))
                _previewPaths[key] = path = SafePath(_root, id + "/" + entry.ContentDirectory + "/preview.png");
            else
            {
                RejectLinks(_root);
                foreach (var directory in Ancestors(path).TakeWhile(parent =>
                             !string.Equals(parent, _root, StringComparison.OrdinalIgnoreCase))) FileStamp(directory);
            }
            var stamp = FileStamp(path);
            if (stamp == null) return null;
            if (!_previewHashes.TryGetValue(path, out var cached) || cached.Stamp != stamp)
                _previewHashes[path] = cached = (stamp, AssetIdentity.HashFile(path));
            return cached.Hash == entry.PreviewHash ? path : null;
        }

        private ExpressionLibraryEntry Read(string id, bool verifyContent = false)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid expression ID.");
            if (!verifyContent && _reads.TryGetValue(id, out var cached) && cached.Stamps.All(pair => FileStamp(pair.Key) == pair.Value))
                return cached.Entry;
            var directory = SafePath(_root, id);
            var entry = JsonConvert.DeserializeObject<ExpressionLibraryEntry>(File.ReadAllText(SafePath(directory, "metadata.json")));
            if (entry == null || entry.SchemaVersion != 1 || entry.Id != id || string.IsNullOrWhiteSpace(entry.Name) ||
                string.IsNullOrEmpty(entry.PrimaryFbxGuid) || entry.Targets == null ||
                entry.Assets == null || entry.Assets.Length == 0 || entry.Dependencies == null ||
                entry.Assets.Any(asset => asset == null) || entry.Targets.Any(target => target == null) ||
                entry.Dependencies.Any(dependency => dependency == null) ||
                entry.Assets.Select(asset => asset.Guid).Distinct().Count() != entry.Assets.Length ||
                entry.Assets.Count(asset => asset.Guid == entry.RootGuid) != 1 ||
                entry.Revision != ContentRevision(entry) || entry.ContentDirectory != "content/" + entry.Revision.Substring(0, 16))
                throw new InvalidDataException("Invalid expression metadata: " + id);
            ValidateFolder(entry.Folder);
            foreach (var asset in entry.Assets)
            {
                var source = SafePath(SafePath(directory, entry.ContentDirectory), asset.Path);
                if (!File.Exists(source) || AssetIdentity.HashFile(source) != asset.Hash ||
                    !File.Exists(source + ".meta") || AssetIdentity.HashFile(source + ".meta") != asset.MetaHash ||
                    AssetIdentity.ReadGuid(File.ReadAllText(source + ".meta")) != asset.Guid)
                    throw new InvalidDataException("Missing or invalid expression asset: " + asset.Path);
            }
            var files = entry.Assets.SelectMany(asset => new[] {
                SafePath(SafePath(directory, entry.ContentDirectory), asset.Path),
                SafePath(SafePath(directory, entry.ContentDirectory), asset.Path) + ".meta" })
                .Append(SafePath(directory, "metadata.json"));
            var paths = files.ToArray();
            var directories = paths.SelectMany(file => Ancestors(file).TakeWhile(parent =>
                !string.Equals(parent, _root, StringComparison.OrdinalIgnoreCase))).Distinct();
            _reads[id] = new CachedEntry { Entry = entry, Stamps = paths.Concat(directories).ToDictionary(file => file, FileStamp) };
            return entry;
        }

        private static string FileStamp(string path)
        {
            FileSystemInfo info = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
            if (!info.Exists) return null;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked expression paths are not supported: " + path);
            return (info is FileInfo file ? file.Length : 0) + ":" + info.LastWriteTimeUtc.Ticks + ":" + info.CreationTimeUtc.Ticks;
        }

        private static IEnumerable<string> Ancestors(string path)
        {
            for (var parent = Path.GetDirectoryName(path); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                yield return parent;
        }

        private void CacheEntries(IEnumerable<ExpressionLibraryEntry> entries)
        {
            _entries = entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Id).ToArray();
            var ids = _entries.Select(entry => entry.Id).ToHashSet();
            foreach (var id in _reads.Keys.Where(id => !ids.Contains(id)).ToArray()) _reads.Remove(id);
            var previewKeys = _entries.Select(entry => entry.Id + ":" + entry.ContentDirectory).ToHashSet();
            foreach (var key in _previewPaths.Keys.Where(key => !previewKeys.Contains(key)).ToArray()) _previewPaths.Remove(key);
            var paths = _previewPaths.Values.ToHashSet();
            foreach (var path in _previewHashes.Keys.Where(path => !paths.Contains(path)).ToArray()) _previewHashes.Remove(path);
        }

        private static string ContentRevision(ExpressionLibraryEntry entry) => Hash(Encoding.UTF8.GetBytes(
            JsonConvert.SerializeObject(new { entry.SchemaVersion, entry.Id, entry.Name, entry.Folder,
                entry.RootGuid, entry.PrimaryFbxGuid, entry.Targets, entry.Assets, entry.Dependencies })));

        private static void WriteMetadata(string directory, ExpressionLibraryEntry entry)
        {
            var path = Path.Combine(directory, "metadata.json");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(entry, Formatting.Indented));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private void NotifyChanged()
        {
            foreach (Action handler in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { handler(); } catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ValidateFolder(string folder)
        {
            if (folder == null || (folder.Length > 0 && folder.Split('/').Any(part =>
                    string.IsNullOrWhiteSpace(part) || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)))
                throw new InvalidDataException("Invalid expression folder.");
        }

        private static string SafePath(string root, string relative)
        {
            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) ||
                relative.Replace('\\', '/').Split('/').Any(part => part == "." || part == ".." || part.Length == 0))
                throw new InvalidDataException("Invalid shared expression path.");
            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The expression path is outside its library.");
            RejectLinks(path);
            return path;
        }

        private static void RejectLinks(string path)
        {
            for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked expression paths are not supported: " + current);
        }

        private static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
