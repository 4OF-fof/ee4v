using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Ee4v.AssetManager.Infrastructure.Files
{
    internal interface IAssetFileImportEnvironment
    {
        string AssetsDirectory { get; }

        void ImportPackage(
            string packagePath,
            bool interactive,
            IReadOnlyList<string> expectedAssetGuids,
            Action<bool, IReadOnlyList<string>> onFinished);

        void Refresh();

        string GetAssetGuid(string absolutePath);

        bool AssetGuidExists(string assetGuid);
    }

    internal sealed class AssetFileImportResult
    {
        internal AssetFileImportResult(
            bool succeeded,
            IReadOnlyList<string> assetGuids)
        {
            Succeeded = succeeded;
            AssetGuids = assetGuids ?? Array.Empty<string>();
        }

        internal bool Succeeded { get; }
        internal IReadOnlyList<string> AssetGuids { get; }
    }

    internal static class AssetFileImportService
    {
        private const string UnityPackageExtension = ".unitypackage";

        public static void Import(
            string assetName,
            string assetFileName,
            string sourcePath,
            IReadOnlyList<string> relativePaths,
            IAssetFileImportEnvironment environment,
            bool showUnityPackageImportDialog,
            Action<AssetFileImportResult> completed)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "The import source was not found.");
            }

            if (environment == null || string.IsNullOrWhiteSpace(environment.AssetsDirectory))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "The Unity Assets directory is unavailable.");
            }

            var paths = (relativePaths ?? Array.Empty<string>())
                .Select(NormalizeRelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "At least one import entry is required.");
            }

            var destinationRoot = Path.Combine(
                environment.AssetsDirectory,
                SanitizeFolderName(assetName, "Asset"),
                SanitizeFolderName(Path.GetFileNameWithoutExtension(assetFileName), "File"));
            var importedAssetGuids =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var copiedPaths = new List<string>();
            var pendingPackageCount = 0;
            var schedulingPackages = true;
            var allPackagesSucceeded = true;
            Action completeIfReady = () =>
            {
                if (schedulingPackages ||
                    pendingPackageCount != 0)
                {
                    return;
                }

                completed?.Invoke(
                    new AssetFileImportResult(
                        allPackagesSucceeded,
                        importedAssetGuids.ToArray()));
            };
            var copiedAny = false;
            for (var i = 0; i < paths.Length; i++)
            {
                var relativePath = paths[i];
                if (IsUnityPackage(relativePath))
                {
                    pendingPackageCount++;
                    ImportUnityPackage(
                        sourcePath,
                        relativePath,
                        environment,
                        showUnityPackageImportDialog,
                        (succeeded, packageGuids) =>
                        {
                            if (succeeded)
                            {
                                for (var guidIndex = 0;
                                     guidIndex <
                                     packageGuids.Count;
                                     guidIndex++)
                                {
                                    var guid =
                                        packageGuids[guidIndex];
                                    if (environment
                                        .AssetGuidExists(guid))
                                    {
                                        importedAssetGuids.Add(
                                            guid);
                                    }
                                }
                            }
                            else
                            {
                                allPackagesSucceeded = false;
                            }

                            pendingPackageCount--;
                            completeIfReady();
                        });
                    continue;
                }

                copiedPaths.Add(CopyEntry(
                    sourcePath,
                    relativePath,
                    destinationRoot));
                copiedAny = true;
            }

            if (copiedAny)
            {
                environment.Refresh();
                AddResolvedGuid(
                    environment,
                    destinationRoot,
                    importedAssetGuids);
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

                    AddResolvedGuid(
                        environment,
                        importedPath,
                        importedAssetGuids);
                }
            }

            schedulingPackages = false;
            completeIfReady();
        }

        private static void ImportUnityPackage(
            string sourcePath,
            string relativePath,
            IAssetFileImportEnvironment environment,
            bool showUnityPackageImportDialog,
            Action<bool, IReadOnlyList<string>> completed)
        {
            string filePath;
            if (TryResolveFile(sourcePath, relativePath, out filePath))
            {
                var guids =
                    UnityPackageGuidReader.ReadGuids(filePath);
                environment.ImportPackage(
                    filePath,
                    showUnityPackageImportDialog,
                    guids,
                    (succeeded, importedGuids) =>
                        completed?.Invoke(
                            succeeded,
                            importedGuids));
                return;
            }

            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "ee4v-import-" + Guid.NewGuid().ToString("N"));
            var temporaryPackage = Path.Combine(temporaryDirectory, Path.GetFileName(relativePath));
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                using (var source = OpenArchiveEntry(sourcePath, relativePath))
                using (var destination = File.Create(temporaryPackage))
                {
                    source.CopyTo(destination);
                }

                var guids =
                    UnityPackageGuidReader.ReadGuids(temporaryPackage);
                environment.ImportPackage(
                    temporaryPackage,
                    showUnityPackageImportDialog,
                    guids,
                    (succeeded, importedGuids) =>
                    {
                        TryDeleteDirectory(
                            temporaryDirectory);
                        completed?.Invoke(
                            succeeded,
                            importedGuids);
                    });
            }
            catch
            {
                TryDeleteDirectory(temporaryDirectory);
                throw;
            }
        }

        private static string CopyEntry(
            string sourcePath,
            string relativePath,
            string destinationRoot)
        {
            var destinationPath = ResolveDestinationPath(destinationRoot, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            string filePath;
            if (TryResolveFile(sourcePath, relativePath, out filePath))
            {
                File.Copy(filePath, destinationPath, true);
                return destinationPath;
            }

            using (var source = OpenArchiveEntry(sourcePath, relativePath))
            using (var destination = File.Create(destinationPath))
            {
                source.CopyTo(destination);
            }

            return destinationPath;
        }

        private static void AddResolvedGuid(
            IAssetFileImportEnvironment environment,
            string path,
            ISet<string> assetGuids)
        {
            var assetGuid = environment.GetAssetGuid(path);
            if (!string.IsNullOrWhiteSpace(assetGuid))
            {
                assetGuids.Add(assetGuid);
            }
        }

        private static bool TryResolveFile(string sourcePath, string relativePath, out string filePath)
        {
            filePath = null;
            if (!Directory.Exists(sourcePath))
            {
                return false;
            }

            var root = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathInside(root, candidate) || !File.Exists(candidate))
            {
                return false;
            }

            filePath = candidate;
            return true;
        }

        private static Stream OpenArchiveEntry(string sourcePath, string relativePath)
        {
            string archivePath;
            string entryPath;
            ResolveArchivePath(sourcePath, relativePath, out archivePath, out entryPath);

            var archiveStream = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            ZipArchive archive = null;
            try
            {
                archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
                var ignoredRoot = AssetArchivePathUtility.ResolveIgnoredRootFolder(
                    archivePath,
                    archive.Entries.Select(candidate => candidate.FullName));
                var archiveEntryPath = AssetArchivePathUtility.ToArchiveEntryPath(entryPath, ignoredRoot);
                var entry = archive.Entries.FirstOrDefault(candidate =>
                    string.Equals(
                        NormalizeArchiveEntryPath(candidate.FullName),
                        NormalizeArchiveEntryPath(archiveEntryPath),
                        StringComparison.OrdinalIgnoreCase));
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    throw new AssetManagerException(AssetManagerErrorCode.NotFound, "The import entry was not found in the archive.");
                }

                return new ArchiveEntryReadStream(entry.Open(), archive, archiveStream);
            }
            catch
            {
                archive?.Dispose();
                archiveStream.Dispose();
                throw;
            }
        }

        private static void ResolveArchivePath(
            string sourcePath,
            string relativePath,
            out string archivePath,
            out string entryPath)
        {
            if (File.Exists(sourcePath) && IsZip(sourcePath))
            {
                archivePath = sourcePath;
                entryPath = relativePath;
                return;
            }

            if (!Directory.Exists(sourcePath))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "The import source is not an archive or directory.");
            }

            var parts = relativePath.Split('/');
            var root = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                current = Path.GetFullPath(Path.Combine(current, parts[i]));
                if (!IsPathInside(root, current))
                {
                    break;
                }

                if (File.Exists(current) && IsZip(current))
                {
                    archivePath = current;
                    entryPath = string.Join("/", parts.Skip(i + 1).ToArray());
                    return;
                }
            }

            throw new AssetManagerException(AssetManagerErrorCode.NotFound, "The import entry was not found.");
        }

        private static string ResolveDestinationPath(string destinationRoot, string relativePath)
        {
            var root = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathInside(root, destination))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "The import destination escapes its asset folder.");
            }

            return destination;
        }

        private static string NormalizeRelativePath(string path)
        {
            var normalized = (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Import entry paths must be relative.");
            }

            var parts = normalized.Split('/');
            if (parts.Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".." || part.IndexOf(':') >= 0))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Import entry paths cannot contain traversal segments.");
            }

            return string.Join("/", parts);
        }

        private static string NormalizeArchiveEntryPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static bool IsUnityPackage(string path)
        {
            return string.Equals(Path.GetExtension(path), UnityPackageExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsZip(string path)
        {
            return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathInside(string root, string path)
        {
            var prefix = root + Path.DirectorySeparatorChar;
            return string.Equals(root, path, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeFolderName(string value, string fallback)
        {
            var name = (value ?? string.Empty).Trim();
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\' };
            var characters = name.Select(character => invalid.Contains(character) ? '_' : character).ToArray();
            var sanitized = new string(characters).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(sanitized) || sanitized == "." || sanitized == ".."
                ? fallback
                : sanitized;
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
                // Temporary import files are best-effort cleanup after Unity finishes with the package.
            }
        }

        private sealed class ArchiveEntryReadStream : Stream
        {
            private readonly Stream _entryStream;
            private readonly ZipArchive _archive;
            private readonly Stream _archiveStream;

            public ArchiveEntryReadStream(Stream entryStream, ZipArchive archive, Stream archiveStream)
            {
                _entryStream = entryStream;
                _archive = archive;
                _archiveStream = archiveStream;
            }

            public override bool CanRead => _entryStream.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _entryStream.Length;
            public override long Position
            {
                get { return _entryStream.Position; }
                set { throw new NotSupportedException(); }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _entryStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _entryStream.Dispose();
                    _archive.Dispose();
                    _archiveStream.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
