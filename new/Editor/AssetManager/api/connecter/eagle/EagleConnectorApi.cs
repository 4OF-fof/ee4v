using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Api;
using Ee4v.Core.Settings;
using UnityEngine;

namespace Ee4v.AssetManager.Api.Connecter.Eagle
{
    internal static class EagleConnectorApi
    {
        private const string DefaultTargetRoot = "VRCAsset";

        internal static IReadOnlyList<EagleItemRecord> ReadItems(EagleSyncRequest request)
        {
            var libraryPath = ResolveLibraryPath(request);
            if (string.IsNullOrWhiteSpace(libraryPath) || !Directory.Exists(libraryPath))
            {
                throw new AssetManagerException(AssetManagerErrorCode.DatasourceError, "Eagle library was not found: " + libraryPath);
            }

            var imagesPath = Path.Combine(libraryPath, "images");
            if (!Directory.Exists(imagesPath))
            {
                throw new AssetManagerException(AssetManagerErrorCode.DatasourceError, "Eagle images directory was not found: " + imagesPath);
            }

            var targetRoot = string.IsNullOrWhiteSpace(request != null ? request.TargetRoot : null)
                ? DefaultTargetRoot
                : request.TargetRoot;
            var errorCount = 0;
            var targetFolders = ReadTargetFolders(libraryPath, targetRoot, ref errorCount);
            if (targetFolders.Count == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.DatasourceError, "Eagle target folder was not found: " + targetRoot);
            }

            return ReadTargetItems(imagesPath, targetFolders);
        }

        private static string ResolveLibraryPath(EagleSyncRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LibraryPath))
            {
                var configuredPath = SettingApi.Get(AssetManagerDefinitions.EagleLibraryPath);
                return string.IsNullOrWhiteSpace(configuredPath)
                    ? null
                    : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
            }

            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.LibraryPath));
        }

        private static IReadOnlyList<EagleFolderTarget> ReadTargetFolders(string libraryPath, string targetRoot, ref int errorCount)
        {
            var result = new List<EagleFolderTarget>();
            if (string.IsNullOrWhiteSpace(targetRoot))
            {
                return result;
            }

            var metadataPath = Path.Combine(libraryPath, "metadata.json");
            if (!File.Exists(metadataPath))
            {
                errorCount++;
                return result;
            }

            EagleFolderNode[] roots;
            try
            {
                roots = ParseFolderRoots(File.ReadAllText(metadataPath));
            }
            catch
            {
                errorCount++;
                return result;
            }

            var normalizedTarget = NormalizeFolderPath(targetRoot);
            for (var i = 0; i < roots.Length; i++)
            {
                CollectMatchingTargetFolders(roots[i], string.Empty, normalizedTarget, result);
            }

            return result;
        }

        private static EagleFolderNode[] ParseFolderRoots(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<EagleFolderNode>();
            }

            return new EagleFolderParser(json).ParseRoots();
        }

        private static void CollectMatchingTargetFolders(
            EagleFolderNode node,
            string parentPath,
            string normalizedTarget,
            IList<EagleFolderTarget> result)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                return;
            }

            var currentPath = string.IsNullOrWhiteSpace(parentPath)
                ? node.name
                : parentPath + "/" + node.name;
            var normalizedCurrentPath = NormalizeFolderPath(currentPath);
            var normalizedName = NormalizeFolderPath(node.name);

            if (string.Equals(normalizedName, normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedCurrentPath, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                CollectDescendantFolderTargets(node, currentPath, result, includeSelf: false);
                return;
            }

            var children = node.children ?? Array.Empty<EagleFolderNode>();
            for (var i = 0; i < children.Length; i++)
            {
                CollectMatchingTargetFolders(children[i], currentPath, normalizedTarget, result);
            }
        }

        private static void CollectDescendantFolderTargets(
            EagleFolderNode node,
            string currentPath,
            IList<EagleFolderTarget> result,
            bool includeSelf)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                return;
            }

            if (includeSelf)
            {
                result.Add(new EagleFolderTarget
                {
                    Id = node.id,
                    Name = string.IsNullOrWhiteSpace(node.name) ? node.id : node.name,
                    Path = currentPath
                });
            }

            var children = node.children ?? Array.Empty<EagleFolderNode>();
            for (var i = 0; i < children.Length; i++)
            {
                var childPath = string.IsNullOrWhiteSpace(currentPath)
                    ? children[i].name
                    : currentPath + "/" + children[i].name;
                CollectDescendantFolderTargets(children[i], childPath, result, includeSelf: true);
            }
        }

        private static IReadOnlyList<EagleItemRecord> ReadTargetItems(string imagesPath, IReadOnlyList<EagleFolderTarget> targetFolders)
        {
            var metadataPaths = Directory.GetFiles(imagesPath, "metadata.json", SearchOption.AllDirectories);
            var results = new List<EagleItemRecord>();
            var metadataById = LoadMetadataById(metadataPaths);

            for (var i = 0; i < targetFolders.Count; i++)
            {
                var targetFolder = targetFolders[i];
                var folderEntries = metadataById.Values
                    .Where(entry => HasFolder(entry.Metadata.folders, targetFolder.Id))
                    .ToArray();
                var metadataEntries = folderEntries
                    .Select(entry => new EagleMetadataWithBooth
                    {
                        Entry = entry,
                        BoothMetadata = ReadBoothMetadata(FindBoothMetadataPath(entry.DirectoryPath))
                    })
                    .ToArray();
                var boothMetadata = metadataEntries
                    .Select(entry => entry.BoothMetadata)
                    .FirstOrDefault(metadata => metadata != null);
                var fileEntries = metadataEntries
                    .Where(entry => entry.BoothMetadata == null)
                    .Select(entry => entry.Entry)
                    .ToArray();

                results.Add(new EagleItemRecord
                {
                    EagleItemId = targetFolder.Id,
                    ItemName = boothMetadata != null && !string.IsNullOrWhiteSpace(boothMetadata.name)
                        ? boothMetadata.name
                        : targetFolder.Name,
                    ItemDescription = boothMetadata != null ? boothMetadata.description : string.Empty,
                    BoothItemId = boothMetadata != null && boothMetadata.boothItemId > 0 ? boothMetadata.boothItemId : (long?)null,
                    BoothName = boothMetadata != null ? boothMetadata.name : null,
                    BoothDescription = boothMetadata != null ? boothMetadata.description : null,
                    BoothThumbnailUrl = boothMetadata != null ? boothMetadata.thumbnailUrl : null,
                    ShopName = boothMetadata != null ? boothMetadata.shopName : null,
                    ShopUrl = boothMetadata != null ? boothMetadata.shopUrl : null,
                    ShopThumbnailUrl = boothMetadata != null ? boothMetadata.shopThumbnailUrl : null,
                    BoothLastUpdatedAtUtc = boothMetadata != null ? ParseUtcTimestamp(boothMetadata.lastUpdatedAtUtc) : null,
                    Files = BuildFolderFileRecords(fileEntries, boothMetadata != null ? boothMetadata.downloads : null)
                });
            }

            return results;
        }

        private static Dictionary<string, EagleMetadataEntry> LoadMetadataById(IEnumerable<string> metadataPaths)
        {
            var results = new Dictionary<string, EagleMetadataEntry>(StringComparer.Ordinal);
            foreach (var metadataPath in metadataPaths)
            {
                var metadata = JsonUtility.FromJson<EagleItemMetadata>(File.ReadAllText(metadataPath));
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.id))
                {
                    continue;
                }

                results[metadata.id] = new EagleMetadataEntry
                {
                    Metadata = metadata,
                    DirectoryPath = Path.GetDirectoryName(metadataPath)
                };
            }

            return results;
        }

        private static bool HasFolder(string[] folders, string targetFolderId)
        {
            if (folders == null || folders.Length == 0 || string.IsNullOrWhiteSpace(targetFolderId))
            {
                return false;
            }

            return folders.Any(folder => string.Equals(folder, targetFolderId, StringComparison.Ordinal));
        }

        private static string NormalizeFolderPath(string value)
        {
            return (value ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private static string FindBoothMetadataPath(string itemDirectory)
        {
            if (string.IsNullOrWhiteSpace(itemDirectory) || !Directory.Exists(itemDirectory))
            {
                return null;
            }

            var jsonPaths = Directory.GetFiles(itemDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(Path.GetFileName(path), "metadata.json", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            for (var i = 0; i < jsonPaths.Length; i++)
            {
                var json = File.ReadAllText(jsonPaths[i]);
                if (json.IndexOf("\"boothItemId\"", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                return jsonPaths[i];
            }

            return null;
        }

        private static EagleBoothMetadata ReadBoothMetadata(string boothMetadataPath)
        {
            if (string.IsNullOrWhiteSpace(boothMetadataPath) || !File.Exists(boothMetadataPath))
            {
                return null;
            }

            return JsonUtility.FromJson<EagleBoothMetadata>(File.ReadAllText(boothMetadataPath));
        }

        private static IReadOnlyList<EagleFileRecord> BuildFolderFileRecords(IReadOnlyList<EagleMetadataEntry> fileEntries, IReadOnlyList<EagleBoothDownload> downloads)
        {
            var results = new List<EagleFileRecord>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var downloadLookup = new EagleDownloadLookup(downloads);
            for (var i = 0; i < fileEntries.Count; i++)
            {
                var entry = fileEntries[i];
                var metadata = entry.Metadata;
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.id) || !seenIds.Add(metadata.id))
                {
                    continue;
                }

                EagleBoothDownload download;
                var hasDownload = downloadLookup.TryUse(metadata, out download);
                var fileName = hasDownload && !string.IsNullOrWhiteSpace(download.filename)
                    ? download.filename
                    : GetFileName(metadata);
                var downloadId = hasDownload && download.downloadId > 0
                    ? download.downloadId
                    : (long?)null;
                results.Add(ToFileRecord(metadata, entry.DirectoryPath, fileName, downloadId));
            }

            var remainingDownloads = downloadLookup.GetUnusedDownloads();
            for (var i = 0; i < remainingDownloads.Count; i++)
            {
                results.Add(ToDownloadOnlyFileRecord(remainingDownloads[i]));
            }

            return results;
        }

        private static EagleFileRecord ToDownloadOnlyFileRecord(EagleBoothDownload download)
        {
            var fileName = !string.IsNullOrWhiteSpace(download.filename)
                ? download.filename
                : "download-" + download.downloadId;
            return new EagleFileRecord
            {
                DownloadId = download.downloadId,
                Name = fileName,
                Extension = GetExtension(fileName),
                IsDeleted = false
            };
        }

        private static string GetFileName(EagleItemMetadata metadata)
        {
            if (metadata == null)
            {
                return string.Empty;
            }

            var name = metadata.name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(metadata.ext) ||
                name.EndsWith("." + metadata.ext, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            return name + "." + metadata.ext;
        }

        private static EagleFileRecord ToFileRecord(EagleItemMetadata metadata, string directoryPath, string fileName, long? downloadId)
        {
            return new EagleFileRecord
            {
                EagleItemId = metadata.id,
                DownloadId = downloadId,
                Name = string.IsNullOrWhiteSpace(fileName) ? metadata.name : fileName,
                SizeBytes = metadata.size,
                Extension = string.IsNullOrWhiteSpace(fileName) ? metadata.ext : GetExtension(fileName),
                IsDeleted = metadata.isDeleted,
                FilePath = directoryPath
            };
        }

        private static string GetExtension(string fileName)
        {
            var extension = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetExtension(fileName);
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.TrimStart('.');
        }

        private static DateTime? ParseUtcTimestamp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DateTime parsed;
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out parsed)
                ? parsed
                : (DateTime?)null;
        }

        private sealed class EagleFolderNode
        {
            public string id;
            public string name;
            public EagleFolderNode[] children;
        }

        private sealed class EagleFolderTarget
        {
            public string Id;
            public string Name;
            public string Path;
        }

        private sealed class EagleMetadataWithBooth
        {
            public EagleMetadataEntry Entry;
            public EagleBoothMetadata BoothMetadata;
        }

        private sealed class EagleFolderParser
        {
            private readonly string _json;
            private int _index;

            public EagleFolderParser(string json)
            {
                _json = json ?? string.Empty;
            }

            public EagleFolderNode[] ParseRoots()
            {
                SkipWhitespace();
                if (Peek() == '[')
                {
                    return ParseFolderArray();
                }

                if (Peek() != '{')
                {
                    return Array.Empty<EagleFolderNode>();
                }

                Read();
                while (!IsEnd)
                {
                    SkipWhitespace();
                    if (Peek() == '}')
                    {
                        Read();
                        break;
                    }

                    var propertyName = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    if (propertyName == "folders" || propertyName == "children")
                    {
                        return ParseFolderArray();
                    }

                    SkipValue();
                    SkipComma();
                }

                return Array.Empty<EagleFolderNode>();
            }

            private EagleFolderNode[] ParseFolderArray()
            {
                SkipWhitespace();
                Expect('[');
                var nodes = new List<EagleFolderNode>();
                while (!IsEnd)
                {
                    SkipWhitespace();
                    if (Peek() == ']')
                    {
                        Read();
                        break;
                    }

                    var node = ParseFolderObject();
                    if (node != null)
                    {
                        nodes.Add(node);
                    }

                    SkipComma();
                }

                return nodes.ToArray();
            }

            private EagleFolderNode ParseFolderObject()
            {
                SkipWhitespace();
                if (Peek() != '{')
                {
                    SkipValue();
                    return null;
                }

                Read();
                var node = new EagleFolderNode
                {
                    children = Array.Empty<EagleFolderNode>()
                };

                while (!IsEnd)
                {
                    SkipWhitespace();
                    if (Peek() == '}')
                    {
                        Read();
                        break;
                    }

                    var propertyName = ParseString();
                    SkipWhitespace();
                    Expect(':');

                    if (propertyName == "id")
                    {
                        node.id = ParseString();
                    }
                    else if (propertyName == "name")
                    {
                        node.name = ParseString();
                    }
                    else if (propertyName == "children")
                    {
                        node.children = ParseFolderArray();
                    }
                    else
                    {
                        SkipValue();
                    }

                    SkipComma();
                }

                return node;
            }

            private string ParseString()
            {
                SkipWhitespace();
                Expect('"');
                var start = _index;
                var escaped = false;
                while (!IsEnd)
                {
                    var current = Read();
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (current == '"')
                    {
                        return _json.Substring(start, _index - start - 1);
                    }
                }

                return string.Empty;
            }

            private void SkipValue()
            {
                SkipWhitespace();
                var current = Peek();
                if (current == '"')
                {
                    ParseString();
                    return;
                }

                if (current == '{')
                {
                    SkipObject();
                    return;
                }

                if (current == '[')
                {
                    SkipArray();
                    return;
                }

                while (!IsEnd && Peek() != ',' && Peek() != '}' && Peek() != ']')
                {
                    Read();
                }
            }

            private void SkipObject()
            {
                Expect('{');
                while (!IsEnd)
                {
                    SkipWhitespace();
                    if (Peek() == '}')
                    {
                        Read();
                        return;
                    }

                    ParseString();
                    SkipWhitespace();
                    Expect(':');
                    SkipValue();
                    SkipComma();
                }
            }

            private void SkipArray()
            {
                Expect('[');
                while (!IsEnd)
                {
                    SkipWhitespace();
                    if (Peek() == ']')
                    {
                        Read();
                        return;
                    }

                    SkipValue();
                    SkipComma();
                }
            }

            private void SkipComma()
            {
                SkipWhitespace();
                if (Peek() == ',')
                {
                    Read();
                }
            }

            private void SkipWhitespace()
            {
                while (!IsEnd && char.IsWhiteSpace(Peek()))
                {
                    Read();
                }
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (Peek() == expected)
                {
                    Read();
                }
            }

            private char Peek()
            {
                return IsEnd ? '\0' : _json[_index];
            }

            private char Read()
            {
                return IsEnd ? '\0' : _json[_index++];
            }

            private bool IsEnd
            {
                get { return _index >= _json.Length; }
            }
        }

        [Serializable]
        private sealed class EagleItemMetadata
        {
            public string id;
            public string name;
            public long size;
            public string ext;
            public string[] folders;
            public bool isDeleted;
        }

        private sealed class EagleMetadataEntry
        {
            public EagleItemMetadata Metadata;
            public string DirectoryPath;
        }

        [Serializable]
        private sealed class EagleBoothMetadata
        {
            public long boothItemId;
            public string name;
            public string description;
            public string thumbnailUrl;
            public string shopName;
            public string shopUrl;
            public string shopThumbnailUrl;
            public string lastUpdatedAtUtc;
            public EagleBoothDownload[] downloads;
        }

        [Serializable]
        private sealed class EagleBoothDownload
        {
            public long downloadId;
            public string filename;
            public string[] importedItemIds;
        }

        private sealed class EagleDownloadLookup
        {
            private readonly IReadOnlyList<EagleBoothDownload> _downloads;
            private readonly Dictionary<string, EagleBoothDownload> _byImportedItemId = new Dictionary<string, EagleBoothDownload>(StringComparer.Ordinal);
            private readonly Dictionary<string, EagleBoothDownload> _byFileName = new Dictionary<string, EagleBoothDownload>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _ambiguousFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<EagleBoothDownload> _used = new HashSet<EagleBoothDownload>();

            public EagleDownloadLookup(IReadOnlyList<EagleBoothDownload> downloads)
            {
                _downloads = downloads ?? Array.Empty<EagleBoothDownload>();
                for (var i = 0; i < _downloads.Count; i++)
                {
                    var download = _downloads[i];
                    if (download == null || download.downloadId <= 0)
                    {
                        continue;
                    }

                    AddImportedItemIds(download);
                    AddFileName(download);
                }
            }

            public bool TryUse(EagleItemMetadata metadata, out EagleBoothDownload download)
            {
                download = null;
                if (metadata == null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(metadata.id) &&
                    _byImportedItemId.TryGetValue(metadata.id, out download))
                {
                    _used.Add(download);
                    return true;
                }

                var keys = new[] { GetFileName(metadata), metadata.name };
                for (var i = 0; i < keys.Length; i++)
                {
                    var key = NormalizeDownloadFileName(keys[i]);
                    if (string.IsNullOrWhiteSpace(key) ||
                        _ambiguousFileNames.Contains(key) ||
                        !_byFileName.TryGetValue(key, out download))
                    {
                        continue;
                    }

                    _used.Add(download);
                    return true;
                }

                return false;
            }

            public IReadOnlyList<EagleBoothDownload> GetUnusedDownloads()
            {
                var results = new List<EagleBoothDownload>();
                for (var i = 0; i < _downloads.Count; i++)
                {
                    var download = _downloads[i];
                    if (download != null && download.downloadId > 0 && !_used.Contains(download))
                    {
                        results.Add(download);
                    }
                }

                return results;
            }

            private void AddImportedItemIds(EagleBoothDownload download)
            {
                if (download.importedItemIds == null)
                {
                    return;
                }

                for (var i = 0; i < download.importedItemIds.Length; i++)
                {
                    var importedItemId = download.importedItemIds[i];
                    if (!string.IsNullOrWhiteSpace(importedItemId) && !_byImportedItemId.ContainsKey(importedItemId))
                    {
                        _byImportedItemId.Add(importedItemId, download);
                    }
                }
            }

            private void AddFileName(EagleBoothDownload download)
            {
                var fileName = NormalizeDownloadFileName(download.filename);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return;
                }

                if (_byFileName.ContainsKey(fileName))
                {
                    _byFileName.Remove(fileName);
                    _ambiguousFileNames.Add(fileName);
                    return;
                }

                if (!_ambiguousFileNames.Contains(fileName))
                {
                    _byFileName.Add(fileName, download);
                }
            }

            private static string NormalizeDownloadFileName(string fileName)
            {
                return string.IsNullOrWhiteSpace(fileName)
                    ? string.Empty
                    : Path.GetFileName(fileName.Trim());
            }
        }
    }
}
