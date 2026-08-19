using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Eagle
{
    internal sealed class EagleAssetSource : IEagleAssetSource
    {
        private const string DefaultTargetRoot = "VRCAsset";

        public AssetSourceSnapshot Read(EagleSyncRequest request)
        {
            var libraryPath = request == null
                ? null
                : request.LibraryPath;
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                throw Error("Eagle library path is required.");
            }

            libraryPath = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(libraryPath));
            var imagesPath = Path.Combine(libraryPath, "images");
            var folderMetadataPath = Path.Combine(
                libraryPath,
                "metadata.json");
            if (!Directory.Exists(imagesPath) ||
                !File.Exists(folderMetadataPath))
            {
                throw Error("Eagle library was not found.");
            }

            try
            {
                var folderMetadata = JsonUtility.FromJson<
                    EagleFolderMetadata>(
                    File.ReadAllText(folderMetadataPath));
                var targetRoot = string.IsNullOrWhiteSpace(
                        request.TargetRoot)
                    ? DefaultTargetRoot
                    : request.TargetRoot;
                var targets = FindTargetFolders(
                    folderMetadata == null
                        ? null
                        : folderMetadata.folders,
                    targetRoot,
                    out var targetFound);
                if (!targetFound)
                {
                    throw Error(
                        "Eagle target folder was not found: " +
                        targetRoot);
                }

                var entries = ReadEntries(imagesPath);
                var claimedFiles = new HashSet<string>(
                    StringComparer.Ordinal);
                var items = new List<AssetSourceSnapshotItem>();
                foreach (var target in targets
                             .OrderBy(
                                 value => value.Path,
                                 StringComparer.Ordinal)
                             .ThenBy(
                                 value => value.Id,
                                 StringComparer.Ordinal))
                {
                    var folderEntries = entries
                        .Where(entry => HasFolder(
                            entry.Metadata.folders,
                            target.Id))
                        .ToArray();
                    var booth = folderEntries
                        .Select(ReadBoothMetadata)
                        .FirstOrDefault(value => value != null);
                    var files = new List<AssetSourceSnapshotFile>();
                    for (var i = 0; i < folderEntries.Length; i++)
                    {
                        var entry = folderEntries[i];
                        if (ReadBoothMetadata(entry) != null ||
                            !claimedFiles.Add(entry.Metadata.id))
                        {
                            continue;
                        }

                        var file = ToFile(entry);
                        if (file != null)
                        {
                            files.Add(file);
                        }
                    }

                    items.Add(new AssetSourceSnapshotItem
                    {
                        SourceId = target.Id,
                        Name = booth != null &&
                               !string.IsNullOrWhiteSpace(booth.name)
                            ? booth.name
                            : target.Name,
                        Description = booth == null
                            ? string.Empty
                            : booth.description ?? string.Empty,
                        ThumbnailUrl = booth == null
                            ? null
                            : booth.thumbnailUrl,
                        Tags = folderEntries
                            .Where(entry =>
                                ReadBoothMetadata(entry) == null)
                            .SelectMany(entry =>
                                entry.Metadata.tags ??
                                Array.Empty<string>())
                            .Where(tag => !IsSystemMetadataTag(tag))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        Files = files
                    });
                }

                return new AssetSourceSnapshot(items);
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatasourceError,
                    "Eagle library could not be read.",
                    exception);
            }
        }

        private static IReadOnlyList<EagleFolderTarget>
            FindTargetFolders(
                IReadOnlyList<EagleFolderNode> roots,
                string targetRoot,
                out bool targetFound)
        {
            var result = new List<EagleFolderTarget>();
            targetFound = false;
            var normalizedTarget = NormalizePath(targetRoot);
            var source = roots ?? Array.Empty<EagleFolderNode>();
            for (var i = 0; i < source.Count; i++)
            {
                FindTargetFolders(
                    source[i],
                    string.Empty,
                    normalizedTarget,
                    result,
                    ref targetFound);
            }

            return result;
        }

        private static void FindTargetFolders(
            EagleFolderNode node,
            string parentPath,
            string targetRoot,
            ICollection<EagleFolderTarget> result,
            ref bool targetFound)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                return;
            }

            var path = string.IsNullOrWhiteSpace(parentPath)
                ? node.name
                : parentPath + "/" + node.name;
            if (string.Equals(
                    NormalizePath(node.name),
                    targetRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    NormalizePath(path),
                    targetRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                targetFound = true;
                AddDescendants(node, path, result);
                return;
            }

            var children = node.children ??
                           Array.Empty<EagleFolderNode>();
            for (var i = 0; i < children.Length; i++)
            {
                FindTargetFolders(
                    children[i],
                    path,
                    targetRoot,
                    result,
                    ref targetFound);
            }
        }

        private static void AddDescendants(
            EagleFolderNode parent,
            string parentPath,
            ICollection<EagleFolderTarget> result)
        {
            var children = parent.children ??
                           Array.Empty<EagleFolderNode>();
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var path = parentPath + "/" + child.name;
                result.Add(new EagleFolderTarget
                {
                    Id = child.id,
                    Name = string.IsNullOrWhiteSpace(child.name)
                        ? child.id
                        : child.name,
                    Path = path
                });
                AddDescendants(child, path, result);
            }
        }

        private static IReadOnlyList<EagleEntry> ReadEntries(
            string imagesPath)
        {
            var result = new List<EagleEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var paths = Directory.GetFiles(
                imagesPath,
                "metadata.json",
                SearchOption.AllDirectories);
            for (var i = 0; i < paths.Length; i++)
            {
                var metadata = JsonUtility.FromJson<EagleItemMetadata>(
                    File.ReadAllText(paths[i]));
                if (metadata == null ||
                    string.IsNullOrWhiteSpace(metadata.id) ||
                    !seen.Add(metadata.id))
                {
                    continue;
                }

                result.Add(new EagleEntry
                {
                    Metadata = metadata,
                    DirectoryPath = Path.GetDirectoryName(paths[i])
                });
            }

            return result;
        }

        private static EagleBoothMetadata ReadBoothMetadata(
            EagleEntry entry)
        {
            var paths = Directory.GetFiles(
                entry.DirectoryPath,
                "*.json",
                SearchOption.TopDirectoryOnly);
            for (var i = 0; i < paths.Length; i++)
            {
                if (string.Equals(
                        Path.GetFileName(paths[i]),
                        "metadata.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var json = File.ReadAllText(paths[i]);
                if (json.IndexOf(
                        "\"boothItemId\"",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                return JsonUtility.FromJson<EagleBoothMetadata>(json);
            }

            return null;
        }

        private static AssetSourceSnapshotFile ToFile(EagleEntry entry)
        {
            var metadata = entry.Metadata;
            var fileName = GetFileName(metadata);
            var sourcePath = ResolveSourcePath(
                entry.DirectoryPath,
                fileName,
                metadata.name);
            if (metadata.isDeleted ||
                string.IsNullOrWhiteSpace(sourcePath) ||
                !File.Exists(sourcePath) ||
                Directory.Exists(sourcePath))
            {
                return null;
            }

            return new AssetSourceSnapshotFile
            {
                SourceId = metadata.id,
                FileName = fileName,
                Extension = string.IsNullOrWhiteSpace(metadata.ext)
                    ? GetExtension(fileName)
                    : metadata.ext.Trim().TrimStart('.').ToLowerInvariant(),
                SourcePath = sourcePath
            };
        }

        private static string GetFileName(EagleItemMetadata metadata)
        {
            var name = metadata.name ?? string.Empty;
            var extension = (metadata.ext ?? string.Empty)
                .Trim()
                .TrimStart('.');
            if (extension.Length == 0 ||
                name.EndsWith(
                    "." + extension,
                    StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            return name + "." + extension;
        }

        private static string ResolveSourcePath(
            string directoryPath,
            params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(names[i]))
                {
                    continue;
                }

                var candidate = Path.Combine(directoryPath, names[i]);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            var payloads = Directory.GetFileSystemEntries(directoryPath)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    "metadata.json",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.EndsWith(
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return payloads.Length == 1 ? payloads[0] : null;
        }

        private static string GetExtension(string fileName)
        {
            return Path.GetExtension(fileName ?? string.Empty)
                .TrimStart('.')
                .ToLowerInvariant();
        }

        private static bool HasFolder(
            IReadOnlyList<string> folders,
            string folderId)
        {
            var source = folders ?? Array.Empty<string>();
            for (var i = 0; i < source.Count; i++)
            {
                if (string.Equals(
                        source[i],
                        folderId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSystemMetadataTag(string tag)
        {
            var normalized = (tag ?? string.Empty).Trim();
            return string.Equals(
                       normalized,
                       "BoothMeta",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       normalized,
                       "VRCMeta",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private static AssetManagerException Error(string message)
        {
            return new AssetManagerException(
                AssetManagerErrorCode.DatasourceError,
                message);
        }

        [Serializable]
        private sealed class EagleFolderMetadata
        {
            public EagleFolderNode[] folders;
        }

        [Serializable]
        private sealed class EagleFolderNode
        {
            public string id;
            public string name;
            public EagleFolderNode[] children;
        }

        [Serializable]
        private sealed class EagleItemMetadata
        {
            public string id;
            public string name;
            public string ext;
            public string[] folders;
            public string[] tags;
            public bool isDeleted;
        }

        [Serializable]
        private sealed class EagleBoothMetadata
        {
            public long boothItemId;
            public string name;
            public string description;
            public string thumbnailUrl;
        }

        private sealed class EagleEntry
        {
            internal EagleItemMetadata Metadata { get; set; }
            internal string DirectoryPath { get; set; }
        }

        private sealed class EagleFolderTarget
        {
            internal string Id { get; set; }
            internal string Name { get; set; }
            internal string Path { get; set; }
        }
    }
}
