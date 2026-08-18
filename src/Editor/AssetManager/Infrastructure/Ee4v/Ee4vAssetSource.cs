using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Ee4v
{
    internal sealed class Ee4vAssetSource : IEe4vAssetSource
    {
        private const int SchemaVersion = 1;
        private const string AssetsDirectoryName = "Assets";
        private const string MetadataFileName = "metadata.json";
        private const string ItemKind = "item";
        private const string FileKind = "file";

        public AssetSourceSnapshot Read(Ee4vSyncRequest request)
        {
            try
            {
                return ReadCore(request);
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Error(
                    "ee4v source could not be read.",
                    exception);
            }
        }

        private static AssetSourceSnapshot ReadCore(
            Ee4vSyncRequest request)
        {
            var libraryPath = ResolveLibraryPath(
                request == null ? null : request.LibraryPath,
                false);
            var assetsPath = Path.Combine(
                libraryPath,
                AssetsDirectoryName);
            if (!Directory.Exists(assetsPath))
            {
                return new AssetSourceSnapshot(
                    Array.Empty<AssetSourceSnapshotItem>());
            }

            try
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var items = new List<AssetSourceSnapshotItem>();
                var files = new List<AssetSourceSnapshotFile>();
                foreach (var entryPath in Directory
                             .GetDirectories(assetsPath)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    var metadataPath = Path.Combine(
                        entryPath,
                        MetadataFileName);
                    if (!File.Exists(metadataPath))
                    {
                        continue;
                    }

                    var metadata = ReadMetadata(metadataPath);
                    if (!seen.Add(metadata.id))
                    {
                        throw Error(
                            "Duplicate ee4v source id was found.");
                    }

                    if (metadata.kind == ItemKind)
                    {
                        items.Add(ToItemSnapshot(metadata, entryPath));
                    }
                    else
                    {
                        var file = ToFileSnapshot(metadata, entryPath);
                        if (file != null)
                        {
                            files.Add(file);
                        }
                    }
                }

                return new AssetSourceSnapshot(items, files);
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Error(
                    "ee4v source could not be read.",
                    exception);
            }
        }

        public AssetSourceSnapshotItem Import(
            ImportEe4vFileRequest request,
            IReadOnlyList<string> normalizedTags)
        {
            try
            {
                return ImportCore(request, normalizedTags);
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Error(
                    "ee4v source file could not be imported.",
                    exception);
            }
        }

        private static AssetSourceSnapshotItem ImportCore(
            ImportEe4vFileRequest request,
            IReadOnlyList<string> normalizedTags)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.FilePath))
            {
                throw Error("Import file path is required.");
            }

            var stored = Store(
                request.LibraryPath,
                request.FilePath,
                null,
                (id, fileName) => new Ee4vMetadata
                {
                    schemaVersion = SchemaVersion,
                    kind = ItemKind,
                    id = id,
                    name = string.IsNullOrWhiteSpace(request.Name)
                        ? Path.GetFileNameWithoutExtension(fileName)
                        : request.Name.Trim(),
                    description = request.Description ?? string.Empty,
                    tags = (normalizedTags ?? Array.Empty<string>())
                        .ToArray(),
                    fileName = fileName
                });
            return ToItemSnapshot(stored.Metadata, stored.EntryPath);
        }

        public AssetSourceSnapshotFile Register(RegisterFileRequest request)
        {
            try
            {
                if (request == null)
                {
                    throw Error("Register file request is required.");
                }

                var stored = Store(
                    request.LibraryPath,
                    request.FilePath,
                    request.FileName,
                    (id, fileName) => new Ee4vMetadata
                    {
                        schemaVersion = SchemaVersion,
                        kind = FileKind,
                        id = id,
                        fileName = fileName
                    });
                return ToFileSnapshot(
                    stored.Metadata,
                    stored.EntryPath);
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Error(
                    "ee4v source file could not be registered.",
                    exception);
            }
        }

        public IEe4vDeleteOperation BeginDelete(
            IReadOnlyList<AssetFile> files)
        {
            try
            {
                var source = files ?? Array.Empty<AssetFile>();
                var entryPaths = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < source.Count; i++)
                {
                    var file = source[i];
                    if (file == null ||
                        file.SourceType != AssetSourceType.Ee4v ||
                        string.IsNullOrWhiteSpace(file.SourcePath))
                    {
                        throw Error("ee4v source file was not found.");
                    }

                    var sourcePath = Path.GetFullPath(file.SourcePath);
                    var entryPath = Path.GetDirectoryName(sourcePath);
                    if (string.IsNullOrWhiteSpace(entryPath) ||
                        !Directory.Exists(entryPath))
                    {
                        throw Error("ee4v source directory was not found.");
                    }

                    var metadata = ReadMetadata(Path.Combine(
                        entryPath,
                        MetadataFileName));
                    var expectedPath = Path.GetFullPath(Path.Combine(
                        entryPath,
                        metadata.fileName));
                    if (!string.Equals(
                            metadata.id,
                            file.SourceId,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            expectedPath,
                            sourcePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw Error(
                            "ee4v source metadata does not match the file.");
                    }

                    entryPaths.Add(entryPath);
                }

                var moves = new List<StagedEntry>();
                try
                {
                    foreach (var entryPath in entryPaths)
                    {
                        var assetsPath = Path.GetDirectoryName(entryPath);
                        var libraryPath = string.IsNullOrWhiteSpace(assetsPath)
                            ? null
                            : Path.GetDirectoryName(assetsPath);
                        if (string.IsNullOrWhiteSpace(libraryPath) ||
                            !string.Equals(
                                Path.GetFileName(assetsPath),
                                AssetsDirectoryName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw Error(
                                "ee4v source directory is invalid.");
                        }

                        var trashPath = Path.Combine(
                            libraryPath,
                            ".trash");
                        Directory.CreateDirectory(trashPath);
                        var stagedPath = Path.Combine(
                            trashPath,
                            Path.GetFileName(entryPath) + "-" +
                            Guid.NewGuid().ToString("N"));
                        Directory.Move(entryPath, stagedPath);
                        moves.Add(new StagedEntry(
                            entryPath,
                            stagedPath));
                    }
                }
                catch
                {
                    Restore(moves);
                    throw;
                }

                return new Ee4vDeleteOperation(moves);
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Error(
                    "ee4v source file could not be staged for deletion.",
                    exception);
            }
        }

        private static void Restore(IReadOnlyList<StagedEntry> entries)
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (Directory.Exists(entries[i].StagedPath) &&
                    !Directory.Exists(entries[i].OriginalPath))
                {
                    Directory.Move(
                        entries[i].StagedPath,
                        entries[i].OriginalPath);
                }
            }
        }

        public void Update(
            AssetFile sourceFile,
            string name,
            string description,
            IReadOnlyList<string> normalizedTags)
        {
            if (sourceFile == null ||
                sourceFile.SourceType != AssetSourceType.Ee4v ||
                string.IsNullOrWhiteSpace(sourceFile.SourcePath))
            {
                throw Error("ee4v source file was not found.");
            }

            var entryPath = Path.GetDirectoryName(sourceFile.SourcePath);
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                throw Error("ee4v source path is invalid.");
            }

            var metadataPath = Path.Combine(entryPath, MetadataFileName);
            var metadata = ReadMetadata(metadataPath);
            if (!string.Equals(
                    metadata.id,
                    sourceFile.SourceId,
                    StringComparison.Ordinal) ||
                metadata.kind != ItemKind)
            {
                throw Error("ee4v source metadata does not match the file.");
            }

            metadata.name = name;
            metadata.description = description ?? string.Empty;
            metadata.tags = (normalizedTags ?? Array.Empty<string>())
                .ToArray();
            WriteMetadata(metadataPath, metadata);
        }

        private static AssetSourceSnapshotItem ToItemSnapshot(
            Ee4vMetadata metadata,
            string entryPath)
        {
            Validate(metadata);
            var file = ToFileSnapshot(metadata, entryPath);
            return new AssetSourceSnapshotItem
            {
                SourceId = metadata.id,
                Name = metadata.name,
                Description = metadata.description ?? string.Empty,
                ThumbnailUrl = metadata.thumbnailUrl,
                Tags = metadata.tags ?? Array.Empty<string>(),
                Files = file == null
                    ? Array.Empty<AssetSourceSnapshotFile>()
                    : new[] { file }
            };
        }

        private static AssetSourceSnapshotFile ToFileSnapshot(
            Ee4vMetadata metadata,
            string entryPath)
        {
            Validate(metadata);
            var sourcePath = Path.Combine(
                entryPath,
                metadata.fileName);
            if (!File.Exists(sourcePath) || Directory.Exists(sourcePath))
            {
                return null;
            }

            return new AssetSourceSnapshotFile
            {
                SourceId = metadata.id,
                FileName = metadata.fileName,
                Extension = Path.GetExtension(metadata.fileName)
                    .TrimStart('.')
                    .ToLowerInvariant(),
                SourcePath = sourcePath
            };
        }

        private static StoredEntry Store(
            string libraryValue,
            string sourceValue,
            string requestedFileName,
            Func<string, string, Ee4vMetadata> createMetadata)
        {
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                throw Error("File path is required.");
            }

            var sourcePath = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(sourceValue));
            if (Directory.Exists(sourcePath))
            {
                throw Error("Directories cannot be registered as files.");
            }

            if (!File.Exists(sourcePath))
            {
                throw Error("File was not found.");
            }

            var fileName = string.IsNullOrWhiteSpace(requestedFileName)
                ? Path.GetFileName(sourcePath)
                : requestedFileName.Trim();
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName == "." ||
                fileName == ".." ||
                string.Equals(
                    fileName,
                    MetadataFileName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    fileName,
                    Path.GetFileName(fileName),
                    StringComparison.Ordinal))
            {
                throw Error("File name is invalid.");
            }

            var libraryPath = ResolveLibraryPath(libraryValue, true);
            var assetsPath = Path.Combine(
                libraryPath,
                AssetsDirectoryName);
            Directory.CreateDirectory(assetsPath);
            var id = NewSourceId(assetsPath);
            var targetPath = Path.Combine(assetsPath, id);
            var temporaryPath = targetPath + ".tmp";
            var metadata = createMetadata(id, fileName);
            try
            {
                Directory.CreateDirectory(temporaryPath);
                File.Copy(
                    sourcePath,
                    Path.Combine(temporaryPath, fileName),
                    false);
                File.WriteAllText(
                    Path.Combine(temporaryPath, MetadataFileName),
                    JsonUtility.ToJson(metadata, true));
                Directory.Move(temporaryPath, targetPath);
                return new StoredEntry(metadata, targetPath);
            }
            catch (Exception exception)
            {
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, true);
                }

                throw Error(
                    "ee4v source file could not be stored.",
                    exception);
            }
        }

        private static Ee4vMetadata ReadMetadata(string metadataPath)
        {
            try
            {
                if (!File.Exists(metadataPath))
                {
                    throw Error("ee4v source metadata was not found.");
                }

                var metadata = JsonUtility.FromJson<Ee4vMetadata>(
                    File.ReadAllText(metadataPath));
                Validate(metadata);
                return metadata;
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Error(
                    "ee4v source metadata is invalid.",
                    exception);
            }
        }

        private static void WriteMetadata(
            string metadataPath,
            Ee4vMetadata metadata)
        {
            var temporaryPath = metadataPath + ".tmp";
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(metadata, true));
                File.Replace(temporaryPath, metadataPath, null);
            }
            catch (Exception exception)
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                throw Error(
                    "ee4v source metadata could not be updated.",
                    exception);
            }
        }

        private static void Validate(Ee4vMetadata metadata)
        {
            if (metadata == null ||
                metadata.schemaVersion != SchemaVersion ||
                (metadata.kind != ItemKind && metadata.kind != FileKind) ||
                string.IsNullOrWhiteSpace(metadata.id) ||
                (metadata.kind == ItemKind &&
                 string.IsNullOrWhiteSpace(metadata.name)) ||
                string.IsNullOrWhiteSpace(metadata.fileName) ||
                metadata.fileName == "." ||
                metadata.fileName == ".." ||
                string.Equals(
                    metadata.fileName,
                    MetadataFileName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    metadata.fileName,
                    Path.GetFileName(metadata.fileName),
                    StringComparison.Ordinal))
            {
                throw Error("ee4v source metadata is invalid.");
            }
        }

        private static string ResolveLibraryPath(
            string value,
            bool create)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Error("ee4v library path is required.");
            }

            var path = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(value));
            if (create)
            {
                Directory.CreateDirectory(path);
            }
            else if (!Directory.Exists(path))
            {
                throw Error("ee4v library was not found.");
            }

            return path;
        }

        private static string NewSourceId(string assetsPath)
        {
            string id;
            do
            {
                id = Guid.NewGuid().ToString("N");
            }
            while (Directory.Exists(Path.Combine(assetsPath, id)) ||
                   Directory.Exists(Path.Combine(assetsPath, id + ".tmp")));

            return id;
        }

        private static AssetManagerException Error(
            string message,
            Exception innerException = null)
        {
            return innerException == null
                ? new AssetManagerException(
                    AssetManagerErrorCode.DatasourceError,
                    message)
                : new AssetManagerException(
                    AssetManagerErrorCode.DatasourceError,
                    message,
                    innerException);
        }

        [Serializable]
        private sealed class Ee4vMetadata
        {
            public int schemaVersion;
            public string kind;
            public string id;
            public string name;
            public string description;
            public string thumbnailUrl;
            public string[] tags;
            public string fileName;
        }

        private sealed class StoredEntry
        {
            internal StoredEntry(
                Ee4vMetadata metadata,
                string entryPath)
            {
                Metadata = metadata;
                EntryPath = entryPath;
            }

            internal Ee4vMetadata Metadata { get; }
            internal string EntryPath { get; }
        }

        private sealed class StagedEntry
        {
            internal StagedEntry(
                string originalPath,
                string stagedPath)
            {
                OriginalPath = originalPath;
                StagedPath = stagedPath;
            }

            internal string OriginalPath { get; }
            internal string StagedPath { get; }
        }

        private sealed class Ee4vDeleteOperation
            : IEe4vDeleteOperation
        {
            private readonly IReadOnlyList<StagedEntry> _entries;
            private bool _committed;

            internal Ee4vDeleteOperation(
                IReadOnlyList<StagedEntry> entries)
            {
                _entries = entries ?? Array.Empty<StagedEntry>();
            }

            public void Commit()
            {
                if (_committed)
                {
                    return;
                }

                _committed = true;
                for (var i = 0; i < _entries.Count; i++)
                {
                    try
                    {
                        if (Directory.Exists(_entries[i].StagedPath))
                        {
                            Directory.Delete(
                                _entries[i].StagedPath,
                                true);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            public void Dispose()
            {
                if (!_committed)
                {
                    Restore(_entries);
                }
            }
        }
    }
}
