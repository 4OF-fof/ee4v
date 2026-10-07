using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.BoothLibraryManager;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class ExternalAssetSource : IExternalAssetSource
    {
        public AssetSourceSnapshot Read(AssetDatasourceRequest request)
        {
            try
            {
                var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.LibraryPath));
                if (!Directory.Exists(root)) { throw new IOException("Datasource folderlibrary was not found."); }
                RejectLink(root);
                if (request.Kind == AssetDatasourceKind.BoothLibraryManager)
                {
                    if (string.IsNullOrWhiteSpace(request.DatabasePath))
                    { throw new IOException("An explicit BLM database path is required."); }
                    return ReadBlm(root, request.DatabasePath);
                }
                throw new IOException("Unknown datasource.");
            }
            catch (Exception exception)
            {
                throw new AssetManagerException(AssetManagerErrorCode.DatasourceError,
                    "Datasource could not be read: " + exception.Message, exception);
            }
        }

        private static AssetSourceSnapshot ReadBlm(string root, string databasePath)
        {
            var items = new List<AssetSourceSnapshotItem>();
            foreach (var record in BoothLibraryManagerApi.GetRegisteredItems(databasePath))
            {
                var directory = SafePath(root, record.RegisteredId);
                var files = Directory.Exists(directory)
                    ? EnumerateFiles(directory).Select(path => ToFile(
                        record.RegisteredId + "/" + RelativePath(directory, path), path)).ToArray()
                    : Array.Empty<AssetSourceSnapshotFile>();
                items.Add(new AssetSourceSnapshotItem
                {
                    SourceId = record.RegisteredId, Name = record.Name,
                    Description = record.Description, ThumbnailUrl = record.ThumbnailUrl,
                    Tags = record.Tags, Files = files,
                    Booth = record.BoothItemId <= 0 ? null : new AssetBoothMetadata
                    { ItemUrl = record.ItemUrl, ShopName = record.ShopName, ShopUrl = record.ShopUrl }
                });
            }
            return new AssetSourceSnapshot(items);
        }

        internal static AssetSourceSnapshot ReadEe4vFolder(string root)
        {
            var itemsRoot = Path.Combine(root, "Items");
            if (!Directory.Exists(itemsRoot)) { return new AssetSourceSnapshot(Array.Empty<AssetSourceSnapshotItem>()); }
            RejectLink(itemsRoot);
            var items = new List<AssetSourceSnapshotItem>();
            foreach (var directory in Directory.GetDirectories(itemsRoot).OrderBy(value => value, StringComparer.Ordinal))
            {
                RejectLink(directory);
                var metadataPath = Path.Combine(directory, "metadata.json");
                RejectLink(metadataPath);
                var metadata = JsonConvert.DeserializeObject<Ee4vFolderMetadata>(File.ReadAllText(metadataPath));
                if (metadata == null || metadata.schemaVersion != 1 || string.IsNullOrWhiteSpace(metadata.id) ||
                    metadata.id != Path.GetFileName(directory)) { throw new IOException("Invalid ee4v folder metadata."); }
                var files = new List<AssetSourceSnapshotFile>();
                var fileIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var file in metadata.files ?? Array.Empty<Ee4vFolderFile>())
                {
                    if (file == null || string.IsNullOrWhiteSpace(file.id) || !fileIds.Add(file.id))
                    { throw new IOException("ee4v file ids must be present and unique."); }
                    var path = SafePath(directory, file.fileName);
                    if (!File.Exists(path)) { throw new IOException("ee4v datasource file is missing: " + file.fileName); }
                    var snapshot = ToFile(metadata.id + "/" + file.id, path);
                    if (!string.IsNullOrWhiteSpace(file.originalFileName))
                    { snapshot.FileName = file.originalFileName; }
                    files.Add(snapshot);
                }
                items.Add(new AssetSourceSnapshotItem
                {
                    SourceId = metadata.id, Name = metadata.name, Description = metadata.description,
                    ThumbnailUrl = metadata.thumbnailUrl, Tags = metadata.tags, Files = files,
                    Booth = new AssetBoothMetadata
                    { ItemUrl = metadata.itemUrl, ShopName = metadata.shopName, ShopUrl = metadata.shopUrl }
                });
            }
            return new AssetSourceSnapshot(items);
        }

        internal static bool IsEe4vFolderFile(AssetFile file)
        {
            if (file == null || file.SourceType != AssetSourceType.Ee4v ||
                string.IsNullOrWhiteSpace(file.SourcePath)) { return false; }
            var entry = Path.GetDirectoryName(Path.GetFullPath(file.SourcePath));
            return string.Equals(Path.GetFileName(Path.GetDirectoryName(entry)), "Items",
                StringComparison.OrdinalIgnoreCase);
        }

        private static JObject ReadEe4vFolderMetadata(string directory)
        {
            RejectLink(directory);
            var path = Path.Combine(directory, "metadata.json");
            RejectLink(path);
            var metadata = JObject.Parse(File.ReadAllText(path));
            if ((int?)metadata["schemaVersion"] != 1 ||
                (string)metadata["id"] != Path.GetFileName(directory) ||
                !(metadata["files"] is JArray))
            { throw new IOException("Invalid ee4v folder metadata."); }
            return metadata;
        }

        private static JObject RequireEe4vFolderFile(JObject metadata, string directory, AssetFile file)
        {
            var match = ((JArray)metadata["files"]).OfType<JObject>().SingleOrDefault(entry =>
                (string)metadata["id"] + "/" + (string)entry["id"] == file.SourceId);
            if (match == null || !string.Equals(SafePath(directory, (string)match["fileName"]),
                    Path.GetFullPath(file.SourcePath), StringComparison.OrdinalIgnoreCase))
            { throw new IOException("ee4v folder metadata does not match the file."); }
            return match;
        }

        private static void WriteEe4vFolderMetadata(string path, string contents)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, contents);
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary)) { File.Delete(temporary); }
            }
        }

        internal static void UpdateEe4vFolder(AssetFile sourceFile, string name,
            string description, IReadOnlyList<string> tags)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(sourceFile.SourcePath));
            var metadata = ReadEe4vFolderMetadata(directory);
            RequireEe4vFolderFile(metadata, directory, sourceFile);
            metadata["name"] = name;
            metadata["description"] = description ?? string.Empty;
            metadata["tags"] = new JArray(tags ?? Array.Empty<string>());
            WriteEe4vFolderMetadata(Path.Combine(directory, "metadata.json"),
                metadata.ToString(Formatting.Indented));
        }

        internal static IEe4vDeleteOperation BeginDeleteEe4vFolderFiles(IReadOnlyList<AssetFile> files)
        {
            var operation = new Ee4vFolderDeleteOperation();
            try
            {
                foreach (var group in files.GroupBy(file =>
                    Path.GetDirectoryName(Path.GetFullPath(file.SourcePath)), StringComparer.OrdinalIgnoreCase))
                {
                    var directory = group.Key;
                    var metadata = ReadEe4vFolderMetadata(directory);
                    var metadataPath = Path.Combine(directory, "metadata.json");
                    var original = File.ReadAllText(metadataPath);
                    foreach (var file in group)
                    {
                        RequireEe4vFolderFile(metadata, directory, file).Remove();
                    }
                    var trash = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(directory)), ".trash");
                    Directory.CreateDirectory(trash);
                    RejectLink(trash);
                    if (((JArray)metadata["files"]).Count == 0)
                    {
                        operation.Move(directory, Path.Combine(trash, Guid.NewGuid().ToString("N")), true);
                    }
                    else
                    {
                        foreach (var file in group)
                        {
                            operation.Move(file.SourcePath, Path.Combine(trash, Guid.NewGuid().ToString("N")), false);
                        }
                        operation.Metadata.Add((metadataPath, original));
                        WriteEe4vFolderMetadata(metadataPath, metadata.ToString(Formatting.Indented));
                    }
                }
                return operation;
            }
            catch
            {
                operation.Dispose();
                throw;
            }
        }

        private sealed class Ee4vFolderDeleteOperation : IEe4vDeleteOperation
        {
            private readonly List<(string Original, string Staged, bool Directory)> _moves =
                new List<(string, string, bool)>();
            internal readonly List<(string Path, string Contents)> Metadata = new List<(string, string)>();
            private bool _committed;

            internal void Move(string original, string staged, bool directory)
            {
                if (directory) { System.IO.Directory.Move(original, staged); }
                else { File.Move(original, staged); }
                _moves.Add((original, staged, directory));
            }

            public void Commit()
            {
                if (_committed) { return; }
                _committed = true;
                foreach (var move in _moves)
                {
                    try
                    {
                        if (move.Directory) { System.IO.Directory.Delete(move.Staged, true); }
                        else { File.Delete(move.Staged); }
                    }
                    catch { }
                }
            }

            public void Dispose()
            {
                if (_committed) { return; }
                for (var i = _moves.Count - 1; i >= 0; i--)
                {
                    var move = _moves[i];
                    if (move.Directory) { System.IO.Directory.Move(move.Staged, move.Original); }
                    else { File.Move(move.Staged, move.Original); }
                }
                for (var i = Metadata.Count - 1; i >= 0; i--)
                {
                    WriteEe4vFolderMetadata(Metadata[i].Path, Metadata[i].Contents);
                }
            }
        }

        private static IEnumerable<string> EnumerateFiles(string root)
        {
            RejectLink(root);
            foreach (var path in Directory.GetFiles(root)) { RejectLink(path); yield return path; }
            foreach (var directory in Directory.GetDirectories(root))
            {
                foreach (var path in EnumerateFiles(directory)) { yield return path; }
            }
        }

        private static AssetSourceSnapshotFile ToFile(string id, string path) => new AssetSourceSnapshotFile
        { SourceId = id, FileName = Path.GetFileName(path), Extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(), SourcePath = path };

        private static string RelativePath(string root, string path) => path.Substring(root.Length + 1).Replace('\\', '/');

        private static string SafePath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
                relative.Replace('\\', '/').Split('/').Any(value => value == ".." || value == "." || value.Length == 0))
            { throw new IOException("Invalid datasource relative path."); }
            var full = Path.GetFullPath(Path.Combine(root, relative));
            if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) { throw new IOException("Datasource path escaped library."); }
            var current = full;
            while (current.Length >= root.Length)
            {
                if (File.Exists(current) || Directory.Exists(current)) { RejectLink(current); }
                current = Path.GetDirectoryName(current);
                if (current == null) { break; }
            }
            return full;
        }

        private static void RejectLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            { throw new IOException("Datasource symbolic links are not supported."); }
        }

        private sealed class Ee4vFolderMetadata
        {
            public int schemaVersion { get; set; }
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string thumbnailUrl { get; set; }
            public string itemUrl { get; set; }
            public string shopName { get; set; }
            public string shopUrl { get; set; }
            public string[] tags { get; set; }
            public Ee4vFolderFile[] files { get; set; }
        }
        private sealed class Ee4vFolderFile
        {
            public string id { get; set; }
            public string fileName { get; set; }
            public string originalFileName { get; set; }
        }
    }
}
