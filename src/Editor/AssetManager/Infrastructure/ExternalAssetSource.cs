using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.BoothLibraryManager;
using Newtonsoft.Json;

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
                if (request.Kind != AssetDatasourceKind.Custom) { throw new IOException("Unknown datasource."); }
                return ReadCustom(root);
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

        private static AssetSourceSnapshot ReadCustom(string root)
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
                var metadata = JsonConvert.DeserializeObject<CustomMetadata>(File.ReadAllText(metadataPath));
                if (metadata == null || metadata.schemaVersion != 1 || string.IsNullOrWhiteSpace(metadata.id) ||
                    metadata.id != Path.GetFileName(directory)) { throw new IOException("Invalid custom metadata."); }
                var files = new List<AssetSourceSnapshotFile>();
                var fileIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var file in metadata.files ?? Array.Empty<CustomFile>())
                {
                    if (file == null || string.IsNullOrWhiteSpace(file.id) || !fileIds.Add(file.id))
                    { throw new IOException("Custom file ids must be present and unique."); }
                    var path = SafePath(directory, file.fileName);
                    if (!File.Exists(path)) { throw new IOException("Custom datasource file is missing: " + file.fileName); }
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

        private sealed class CustomMetadata
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
            public CustomFile[] files { get; set; }
        }
        private sealed class CustomFile
        {
            public string id { get; set; }
            public string fileName { get; set; }
            public string originalFileName { get; set; }
        }
    }
}
