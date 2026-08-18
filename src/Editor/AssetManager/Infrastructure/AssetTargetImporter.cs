using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.EditorIntegration;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class AssetTargetImporter : IAssetTargetImporter
    {
        private readonly string _assetsDirectory;
        private readonly Action _refresh;
        private readonly Action<string, Action<bool>> _importPackage;

        internal AssetTargetImporter()
            : this(
                AssetImportApi.AssetsDirectory,
                AssetImportApi.Refresh,
                (path, completed) => AssetImportApi.ImportPackage(
                    path,
                    false,
                    completed))
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
                    completed))
        {
        }

        internal AssetTargetImporter(
            string assetsDirectory,
            Action refresh,
            Action<string, Action<bool>> importPackage)
        {
            _assetsDirectory = assetsDirectory ??
                throw new ArgumentNullException(nameof(assetsDirectory));
            _refresh = refresh ??
                throw new ArgumentNullException(nameof(refresh));
            _importPackage = importPackage ??
                throw new ArgumentNullException(nameof(importPackage));
        }

        public void Import(
            AssetItem item,
            AssetFile file,
            IReadOnlyList<string> targetPaths,
            Action<bool> completed)
        {
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
            var copied = false;
            var pendingPackages = 0;
            var scheduling = true;
            var packagesSucceeded = true;
            Action completeIfReady = () =>
            {
                if (!scheduling && pendingPackages == 0)
                {
                    completed?.Invoke(packagesSucceeded);
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
                var targetPath = targetPaths[i];
                if (targetPath.Length == 0)
                {
                    if (IsUnityPackage(file.FileName))
                    {
                        pendingPackages++;
                    }

                    copied |= ImportFile(
                        file.SourcePath,
                        file.FileName,
                        destinationRoot,
                        packageCompleted);
                    continue;
                }

                if (IsUnityPackage(targetPath))
                {
                    pendingPackages++;
                }

                copied |= ImportArchiveEntry(
                    file.SourcePath,
                    targetPath,
                    destinationRoot,
                    packageCompleted);
            }

            if (copied)
            {
                _refresh();
            }

            scheduling = false;
            completeIfReady();
        }

        private bool ImportFile(
            string sourcePath,
            string targetPath,
            string destinationRoot,
            Action<bool> completed)
        {
            if (IsUnityPackage(targetPath))
            {
                _importPackage(sourcePath, completed);
                return false;
            }

            var destinationPath = DestinationPath(
                destinationRoot,
                targetPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath, true);
            return true;
        }

        private bool ImportArchiveEntry(
            string archivePath,
            string targetPath,
            string destinationRoot,
            Action<bool> completed)
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
                    ImportPackageEntry(entry, targetPath, completed);
                    return false;
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

                return true;
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
            Action<bool> completed)
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
