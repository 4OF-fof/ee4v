using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.EditorIntegration;
using UnityEditor;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class AssetTargetImporter : IAssetTargetImporter
    {
        private readonly string _assetsDirectory;
        private readonly Action _refresh;
        private readonly Action<string, Action<bool>> _importPackage;
        private readonly Func<string, string> _assetGuid;
        private readonly ImportedAssetLibrary _library;
        private static readonly SemaphoreSlim SharedImportGate = new SemaphoreSlim(1, 1);

        private sealed class ImportLease : IDisposable
        {
            public void Dispose() => SharedImportGate.Release();
        }

        private static async Task<ImportLease> LockImports(CancellationToken cancellationToken)
        {
            await SharedImportGate.WaitAsync(cancellationToken);
            return new ImportLease();
        }

        internal AssetTargetImporter(string libraryPath)
            : this(AssetImportApi.AssetsDirectory, () => AssetImportApi.Refresh(true),
                (path, completed) => AssetImportApi.ImportPackage(path, false, completed), ResolveAssetGuid)
        {
            _library = new ImportedAssetLibrary(libraryPath);
        }

        internal AssetTargetImporter()
            : this(
                AssetImportApi.AssetsDirectory,
                () => AssetImportApi.Refresh(),
                (path, completed) => AssetImportApi.ImportPackage(
                    path,
                    false,
                    completed),
                ResolveAssetGuid)
        {
        }

        internal AssetTargetImporter(
            string assetsDirectory,
            Action refresh)
            : this(
                assetsDirectory,
                refresh,
                (path, completed) => AssetImportApi.ImportPackage(
                    path,
                    false,
                    completed),
                ResolveAssetGuid)
        {
        }

        internal AssetTargetImporter(
            string assetsDirectory,
            Action refresh,
            Action<string, Action<bool>> importPackage)
            : this(
                assetsDirectory,
                refresh,
                importPackage,
                ResolveAssetGuid)
        {
        }

        internal AssetTargetImporter(
            string assetsDirectory,
            Action refresh,
            Action<string, Action<bool>> importPackage,
            Func<string, string> assetGuid)
        {
            _assetsDirectory = assetsDirectory ??
                throw new ArgumentNullException(nameof(assetsDirectory));
            _refresh = refresh ??
                throw new ArgumentNullException(nameof(refresh));
            _importPackage = importPackage ??
                throw new ArgumentNullException(nameof(importPackage));
            _assetGuid = assetGuid ??
                throw new ArgumentNullException(nameof(assetGuid));
        }

        public Task<AssetImportResult> Import(
            AssetItem item,
            AssetFile file,
            IReadOnlyList<string> targetPaths,
            CancellationToken cancellationToken)
        {
            if (_library != null) return ImportShared(item, file, targetPaths, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(file.SourcePath) ||
                !File.Exists(file.SourcePath))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The source file was not found.");
            }

            var destinationRoot = Path.Combine(
                _assetsDirectory,
                SanitizeName(item.Name, "Item"),
                SanitizeName(
                    Path.GetFileNameWithoutExtension(file.FileName),
                    "File"));
            var copiedPaths = new List<string>();
            var importedGuids = new HashSet<string>(StringComparer.Ordinal);
            var pendingPackages = 0;
            var scheduling = true;
            var packagesSucceeded = true;
            var completion = new TaskCompletionSource<AssetImportResult>();
            var cancellation = cancellationToken.Register(() =>
                completion.TrySetResult(new AssetImportResult(
                    AssetImportState.Canceled,
                    new[] { file.Id },
                    Array.Empty<string>(),
                    "Asset import was canceled.")));
            Action completeIfReady = () =>
            {
                if (!scheduling && pendingPackages == 0)
                {
                    completion.TrySetResult(new AssetImportResult(
                        packagesSucceeded
                            ? AssetImportState.Success
                            : AssetImportState.Failed,
                        new[] { file.Id },
                        packagesSucceeded
                            ? importedGuids.ToArray()
                            : Array.Empty<string>(),
                        packagesSucceeded
                            ? string.Empty
                            : "UnityPackage import failed."));
                }
            };
            Action<bool> packageCompleted = succeeded =>
            {
                packagesSucceeded &= succeeded;
                pendingPackages--;
                completeIfReady();
            };
            for (var i = 0; i < targetPaths.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var targetPath = targetPaths[i];
                if (targetPath.Length == 0)
                {
                    if (IsUnityPackage(file.FileName))
                    {
                        pendingPackages++;
                    }

                    var copiedPath = ImportFile(
                        file.SourcePath,
                        file.FileName,
                        destinationRoot,
                        packageCompleted,
                        importedGuids);
                    if (copiedPath != null)
                    {
                        copiedPaths.Add(copiedPath);
                    }
                    continue;
                }

                if (IsUnityPackage(targetPath))
                {
                    pendingPackages++;
                }

                var extractedPath = ImportArchiveEntry(
                    file.SourcePath,
                    targetPath,
                    destinationRoot,
                    packageCompleted,
                    importedGuids);
                if (extractedPath != null)
                {
                    copiedPaths.Add(extractedPath);
                }
            }

            if (copiedPaths.Count > 0)
            {
                _refresh();
                AddResolvedGuid(destinationRoot, importedGuids);
                for (var i = 0; i < copiedPaths.Count; i++)
                {
                    var importedPath = copiedPaths[i];
                    if (string.Equals(
                            Path.GetExtension(importedPath),
                            ".meta",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        importedPath = importedPath.Substring(
                            0,
                            importedPath.Length - ".meta".Length);
                    }

                    AddResolvedGuid(importedPath, importedGuids);
                }
            }

            scheduling = false;
            completeIfReady();
            return completion.Task.ContinueWith(
                task =>
                {
                    cancellation.Dispose();
                    return task.Result;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private async Task<AssetImportResult> ImportShared(AssetItem item, AssetFile file,
            IReadOnlyList<string> targetPaths, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(file.SourcePath) || !File.Exists(file.SourcePath))
                throw new FileNotFoundException("The source file was not found.", file.SourcePath);
            var sourceHash = ImportedAssetLibrary.SourceHash(file);
            var imported = new HashSet<string>(StringComparer.Ordinal);
            var destinationRoot = Path.GetFullPath(Path.Combine(_assetsDirectory, SanitizeName(item.Name, "Item"),
                SanitizeName(Path.GetFileNameWithoutExtension(file.FileName), "File")));
            using (await LockImports(cancellationToken))
            using (_library.Lock(file))
            {
                foreach (var target in targetPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var temporary = Path.Combine(Path.GetTempPath(), "ee4v-import-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(temporary);
                    try
                    {
                        var name = target.Length == 0 ? file.FileName : target;
                        var source = PrepareSource(file, target, temporary);
                        var package = IsUnityPackage(name);
                        if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Import the Asset together with its metadata, rather than a standalone .meta.");
                        if (!package && File.Exists(source + ".meta") &&
                            AssetIdentity.ReadGuid(File.ReadAllText(source + ".meta")).Length == 0)
                            throw new InvalidDataException("The source Asset metadata has no valid GUID: " + name);
                        var revision = _library.Revision(file, sourceHash, target);
                        var saved = _library.Read(revision);
                        var primary = saved.SingleOrDefault(asset => asset.IsSourceAsset);
                        if (primary != null && (package || primary.IsFolder || AssetIdentity.HashFile(source) != primary.Hash))
                            throw new InvalidDataException("The source Asset does not match its shared metadata: " + name);
                        var before = EnumerateAssetPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        string primaryDestination = null;
                        var packageEntries = package ? UnityPackageReader.ReadEntries(source).ToArray() : Array.Empty<AssetFileContentEntry>();
                        var packagePaths = new HashSet<string>(packageEntries.Select(entry => ProjectPath(entry.Path)), StringComparer.OrdinalIgnoreCase);
                        if (package)
                        {
                            var payload = Path.Combine(temporary, "payload");
                            UnityPackageReader.ExtractContents(source, payload);
                            foreach (var entry in packageEntries)
                            {
                                var destination = ProjectPath(entry.Path);
                                ValidateSafePath(destination);
                                var original = Path.Combine(payload, entry.Path);
                                if (File.Exists(destination) || Directory.Exists(destination) || File.Exists(destination + ".meta"))
                                {
                                    if (!File.Exists(destination + ".meta") ||
                                        AssetIdentity.ReadGuid(File.ReadAllText(destination + ".meta")) != entry.AssetGuid ||
                                        !AssetIdentity.Equivalent(original, destination, entry.Kind == AssetFileContentEntryKind.Directory))
                                        throw new InvalidOperationException("Another or modified asset occupies the import path: " + entry.Path);
                                }
                                var otherPath = ExistingGuidPath(entry.AssetGuid);
                                if (!string.IsNullOrEmpty(otherPath) && !SamePath(otherPath, destination))
                                    throw new InvalidOperationException("The package GUID already exists at another path: " + otherPath);
                            }
                        }
                        // Reuse existing identities, and stage canonical generated files before Unity imports
                        // their source models. Package payload GUIDs/settings are always supplied by the package.
                        var cachedDestinations = saved.ToDictionary(asset => asset,
                            asset => asset.IsImportRoot ? destinationRoot :
                                asset.IsProjectPath ? ProjectPath(asset.Path) :
                                DestinationPath(destinationRoot, asset.Path));
                        foreach (var asset in saved)
                        {
                            var destination = cachedDestinations[asset];
                            var canonical = ImportedAssetLibrary.ContentPath(revision, asset);
                            ValidateSafePath(destination);
                            var existing = ExistingGuidPath(asset.Guid);
                            if (!string.IsNullOrEmpty(existing) && !SamePath(existing, destination))
                            {
                                if (!MatchesSavedAsset(asset, canonical, existing))
                                    throw new InvalidOperationException("The imported GUID belongs to another or modified asset: " + existing);
                                cachedDestinations[asset] = destination = existing;
                            }
                            if (File.Exists(destination) || Directory.Exists(destination) || File.Exists(destination + ".meta"))
                            {
                                if (!MatchesSavedAsset(asset, canonical, destination))
                                    throw new InvalidOperationException("Another or modified asset occupies the import path: " + destination);
                                var localGuid = AssetIdentity.ReadGuid(File.ReadAllText(destination + ".meta"));
                                if (localGuid.Length == 0) throw new InvalidDataException("Invalid existing Asset metadata: " + destination);
                                if (localGuid != asset.Guid)
                                    throw new InvalidOperationException("Another asset GUID occupies the import path: " + destination);
                            }
                        }
                        foreach (var asset in saved)
                        {
                            var destination = cachedDestinations[asset];
                            touched.Add(destination);
                            if (File.Exists(destination) || Directory.Exists(destination)) continue;
                            var canonical = ImportedAssetLibrary.ContentPath(revision, asset);
                            CopyAsset(asset.IsSourceAsset ? source : canonical, destination, asset.IsFolder, canonical + ".meta");
                        }
                        if (package)
                        {
                            var completed = new TaskCompletionSource<bool>();
                            _importPackage(source, succeeded => completed.TrySetResult(succeeded));
                            // Once handed to Unity, await completion even after cancellation. This keeps
                            // temporary package bytes and the shared lock alive until Unity finishes.
                            if (!await completed.Task)
                                return new AssetImportResult(AssetImportState.Failed, new[] { file.Id }, Array.Empty<string>(), "UnityPackage import failed.");
                            touched.UnionWith(packagePaths);
                        }
                        else
                        {
                            var destination = DestinationPath(destinationRoot, name);
                            if (primary != null) destination = cachedDestinations[primary];
                            ValidateSafePath(destination);
                            if (File.Exists(destination) || File.Exists(destination + ".meta"))
                            {
                                if (!File.Exists(destination) || AssetIdentity.HashFile(source) != AssetIdentity.HashFile(destination) ||
                                    (File.Exists(source + ".meta") && !AssetIdentity.Equivalent(source, destination)))
                                    throw new InvalidOperationException("Another or modified asset occupies the import path: " + destination);
                                // Never replace an existing .meta, including on the first import into a legacy project.
                                if (File.Exists(source + ".meta") && File.Exists(destination + ".meta"))
                                {
                                    var originalGuid = AssetIdentity.ReadGuid(File.ReadAllText(source + ".meta"));
                                    var localGuid = AssetIdentity.ReadGuid(File.ReadAllText(destination + ".meta"));
                                    if (originalGuid != localGuid)
                                        throw new InvalidOperationException("Another asset GUID occupies the import path: " + destination);
                                }
                            }
                            else
                            {
                                if (File.Exists(source + ".meta"))
                                {
                                    var originalGuid = AssetIdentity.ReadGuid(File.ReadAllText(source + ".meta"));
                                    var existing = ExistingGuidPath(originalGuid);
                                    if (!string.IsNullOrEmpty(existing))
                                    {
                                        if (!AssetIdentity.Equivalent(source, existing))
                                            throw new InvalidOperationException("The source GUID belongs to a modified asset: " + existing);
                                        destination = existing;
                                    }
                                    else CopyAsset(source, destination, false);
                                }
                                else CopyAsset(source, destination, false);
                            }
                            touched.Add(destination);
                            primaryDestination = destination;
                        }
                        _refresh();
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ImportedAssetLibrary.SourceHash(file) != sourceHash)
                            throw new InvalidOperationException("The source changed while importing: " + file.FileName);
                        if (primaryDestination != null && (!File.Exists(primaryDestination + ".meta") ||
                            string.IsNullOrEmpty(_assetGuid(primaryDestination))))
                            throw new InvalidDataException("Unity did not import the source Asset: " + name);
                        foreach (var entry in packageEntries)
                        {
                            var destination = ProjectPath(entry.Path);
                            if (!File.Exists(destination + ".meta") ||
                                AssetIdentity.ReadGuid(File.ReadAllText(destination + ".meta")) != entry.AssetGuid)
                                throw new InvalidDataException("The package Asset GUID was not preserved: " + entry.Path);
                            imported.Add(entry.AssetGuid);
                        }
                        var after = EnumerateAssetPaths().ToArray();
                        touched.UnionWith(after.Where(path => !before.Contains(path)));
                        // Previously generated external materials may already exist in legacy projects.
                        // Limit this discovery to the model's own Materials directory, not all dependencies.
                        foreach (var model in touched.Where(path => Path.GetExtension(path).Equals(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray())
                        {
                            var modelPath = UnityPath(model);
                            if (string.IsNullOrEmpty(modelPath)) continue;
                            var materials = Path.GetDirectoryName(model).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "Materials" + Path.DirectorySeparatorChar;
                            foreach (var dependency in AssetDatabase.GetDependencies(modelPath, true))
                            {
                                if (!dependency.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                                var full = ProjectPath(dependency);
                                if (full.StartsWith(materials, StringComparison.OrdinalIgnoreCase) &&
                                    full.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) touched.Add(full);
                            }
                        }
                        if (!package && Directory.Exists(destinationRoot)) touched.Add(destinationRoot);
                        var assetSources = new Dictionary<ImportedAssetLibrary.Asset, string>();
                        // Package payload already owns its metadata. Persist only assets outside it.
                        var assets = touched.Where(path => !packagePaths.Contains(path) &&
                                File.Exists(path + ".meta") && (File.Exists(path) || Directory.Exists(path)))
                            .Select(path =>
                            {
                                ValidateSafePath(path);
                                var folder = Directory.Exists(path);
                                var guid = AssetIdentity.ReadGuid(File.ReadAllText(path + ".meta"));
                                if (guid.Length == 0) throw new InvalidDataException("Unity did not generate valid Asset metadata: " + path);
                                imported.Add(guid);
                                var projectPath = package || !path.StartsWith(destinationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                                var relative = projectPath
                                    ? UnityPath(path) : path.Substring(destinationRoot.Length + 1).Replace('\\', '/');
                                // Raw import root is stored separately from its child assets.
                                if (SamePath(path, destinationRoot)) relative = "__root";
                                var asset = new ImportedAssetLibrary.Asset { Path = relative, Guid = guid, IsFolder = folder,
                                    IsProjectPath = projectPath && !SamePath(path, destinationRoot), IsImportRoot = SamePath(path, destinationRoot),
                                    IsSourceAsset = primaryDestination != null && SamePath(path, primaryDestination),
                                    Hash = folder ? null : AssetIdentity.HashFile(path),
                                    MetaHash = AssetIdentity.HashFile(path + ".meta") };
                                assetSources.Add(asset, path);
                                return asset;
                            }).OrderBy(asset => asset.Path, StringComparer.Ordinal).ToArray();
                        // Map generated paths outside a raw import root as project paths as well.
                        _library.Save(revision, assets, asset => assetSources[asset]);
                    }
                    finally { TryDeleteDirectory(temporary); }
                }
            }
            return new AssetImportResult(AssetImportState.Success, new[] { file.Id }, imported.OrderBy(guid => guid).ToArray());
        }

        private static string PrepareSource(AssetFile file, string target, string temporary)
        {
            if (target.Length == 0) return file.SourcePath;
            using (var stream = File.OpenRead(file.SourcePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entry = FindEntry(archive, file.SourcePath, target);
                if (entry == null || string.IsNullOrEmpty(entry.Name)) throw new FileNotFoundException("The ZIP target was not found: " + target);
                var source = Path.Combine(temporary, "source", Path.GetFileName(target));
                Directory.CreateDirectory(Path.GetDirectoryName(source));
                using (var input = entry.Open())
                using (var output = File.Create(source)) input.CopyTo(output);
                var meta = FindEntry(archive, file.SourcePath, target + ".meta");
                if (meta != null)
                    using (var input = meta.Open())
                    using (var output = File.Create(source + ".meta")) input.CopyTo(output);
                return source;
            }
        }

        private IEnumerable<string> EnumerateAssetPaths() => Directory.Exists(_assetsDirectory)
            ? Directory.EnumerateFiles(_assetsDirectory, "*.meta", SearchOption.AllDirectories)
                .Select(path => Path.GetFullPath(path.Substring(0, path.Length - 5))) : Enumerable.Empty<string>();

        private string ProjectPath(string path)
        {
            GitAssetVariantRepository.ValidateAssetPath(path);
            return Path.GetFullPath(Path.Combine(_assetsDirectory, "..", path));
        }

        private string UnityPath(string path)
        {
            var root = Path.GetFullPath(_assetsDirectory).TrimEnd(Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(path);
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? "Assets/" + full.Substring(root.Length + 1).Replace('\\', '/') : null;
        }

        private string ExistingGuidPath(string guid)
        {
            var path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                (File.Exists(ProjectPath(path)) || Directory.Exists(ProjectPath(path))) ? ProjectPath(path) : null;
        }

        private static bool SamePath(string first, string second) =>
            string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

        private void ValidateSafePath(string path)
        {
            if (string.IsNullOrEmpty(UnityPath(path))) throw new InvalidDataException("The import path is outside Assets.");
            for (var current = Path.GetFullPath(path); !SamePath(current, _assetsDirectory); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked Asset paths cannot be imported: " + path);
        }

        private static bool MatchesSavedAsset(ImportedAssetLibrary.Asset asset, string canonical, string destination) =>
            File.Exists(destination + ".meta") &&
            (asset.IsFolder ? Directory.Exists(destination) : File.Exists(destination) && AssetIdentity.HashFile(destination) == asset.Hash) &&
            AssetIdentity.Settings(File.ReadAllText(canonical + ".meta")) == AssetIdentity.Settings(File.ReadAllText(destination + ".meta"));

        private static void CopyAsset(string source, string destination, bool folder, string metadataSource = null)
        {
            Directory.CreateDirectory(folder ? destination : Path.GetDirectoryName(destination));
            if (!folder) File.Copy(source, destination);
            var meta = metadataSource ?? source + ".meta";
            if (File.Exists(meta)) File.Copy(meta, destination + ".meta");
        }

        private string ImportFile(
            string sourcePath,
            string targetPath,
            string destinationRoot,
            Action<bool> completed,
            ISet<string> importedGuids)
        {
            if (IsUnityPackage(targetPath))
            {
                AddPackageGuids(sourcePath, importedGuids);
                _importPackage(sourcePath, completed);
                return null;
            }

            var destinationPath = DestinationPath(
                destinationRoot,
                targetPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath, true);
            return destinationPath;
        }

        private string ImportArchiveEntry(
            string archivePath,
            string targetPath,
            string destinationRoot,
            Action<bool> completed,
            ISet<string> importedGuids)
        {
            if (!string.Equals(
                    Path.GetExtension(archivePath),
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Child targets require a ZIP source file.");
            }

            using (var stream = File.Open(
                       archivePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Read))
            {
                var entry = FindEntry(archive, archivePath, targetPath);
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.NotFound,
                        "The target was not found in the ZIP file.");
                }

                if (IsUnityPackage(targetPath))
                {
                    ImportPackageEntry(
                        entry,
                        targetPath,
                        completed,
                        importedGuids);
                    return null;
                }

                var destinationPath = DestinationPath(
                    destinationRoot,
                    targetPath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(destinationPath));
                using (var source = entry.Open())
                using (var destination = File.Create(destinationPath))
                {
                    source.CopyTo(destination);
                }

                return destinationPath;
            }
        }

        private static ZipArchiveEntry FindEntry(
            ZipArchive archive,
            string archivePath,
            string targetPath)
        {
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrEmpty(entry.Name))
                .ToArray();
            var exact = entries.FirstOrDefault(entry =>
                string.Equals(
                    NormalizeEntryPath(entry.FullName),
                    targetPath,
                    StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            var root = Path.GetFileNameWithoutExtension(archivePath);
            var prefix = root + "/";
            if (!entries.All(entry =>
                    NormalizeEntryPath(entry.FullName).StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var rootedTarget = prefix + targetPath;
            return entries.FirstOrDefault(entry =>
                string.Equals(
                    NormalizeEntryPath(entry.FullName),
                    rootedTarget,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void ImportPackageEntry(
            ZipArchiveEntry entry,
            string targetPath,
            Action<bool> completed,
            ISet<string> importedGuids)
        {
            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "ee4v-target-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                var packagePath = Path.Combine(
                    temporaryDirectory,
                    Path.GetFileName(targetPath));
                using (var source = entry.Open())
                using (var destination = File.Create(packagePath))
                {
                    source.CopyTo(destination);
                }

                AddPackageGuids(packagePath, importedGuids);

                _importPackage(
                    packagePath,
                    succeeded =>
                    {
                        TryDeleteDirectory(temporaryDirectory);
                        completed(succeeded);
                    });
            }
            catch
            {
                TryDeleteDirectory(temporaryDirectory);
                throw;
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }

        private static string DestinationPath(
            string destinationRoot,
            string targetPath)
        {
            var root = Path.GetFullPath(destinationRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(
                root,
                targetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(
                    root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "The target escapes its import directory.");
            }

            return destination;
        }

        private static string NormalizeEntryPath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim('/');
        }

        private static bool IsUnityPackage(string path)
        {
            return string.Equals(
                Path.GetExtension(path),
                ".unitypackage",
                StringComparison.OrdinalIgnoreCase);
        }

        private static void AddPackageGuids(
            string path,
            ISet<string> importedGuids)
        {
            var guids = UnityPackageReader.ReadGuids(path);
            for (var i = 0; i < guids.Count; i++)
            {
                importedGuids.Add(guids[i]);
            }
        }

        private void AddResolvedGuid(
            string path,
            ISet<string> importedGuids)
        {
            var guid = _assetGuid(path);
            if (!string.IsNullOrWhiteSpace(guid))
            {
                importedGuids.Add(guid.Trim().ToLowerInvariant());
            }
        }

        private static string ResolveAssetGuid(string absolutePath)
        {
            var assets = Path.GetFullPath(AssetImportApi.AssetsDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(absolutePath);
            if (!candidate.StartsWith(
                    assets + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var relative = candidate.Substring(assets.Length + 1)
                .Replace('\\', '/');
            return AssetDatabase.AssetPathToGUID("Assets/" + relative);
        }

        private static string SanitizeName(
            string value,
            string fallback)
        {
            var invalid = new HashSet<char>(
                Path.GetInvalidFileNameChars())
            {
                '/',
                '\\'
            };
            var sanitized = new string(
                    (value ?? string.Empty)
                    .Trim()
                    .Select(character =>
                        invalid.Contains(character) ? '_' : character)
                    .ToArray())
                .TrimEnd('.');
            return string.IsNullOrWhiteSpace(sanitized) ||
                   sanitized == "." ||
                   sanitized == ".."
                ? fallback
                : sanitized;
        }
    }
}
