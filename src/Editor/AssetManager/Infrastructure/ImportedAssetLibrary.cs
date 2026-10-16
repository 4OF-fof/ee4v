using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ee4v.AssetManager.Contracts;
using Newtonsoft.Json;

namespace Ee4v.AssetManager.Infrastructure
{
    // The import library is durable source data, not a disposable analysis cache.
    internal sealed class ImportedAssetLibrary
    {
        internal sealed class Asset
        {
            public string Path;
            public bool IsFolder;
            public bool IsProjectPath;
            public bool IsImportRoot;
            public bool IsSourceAsset;
            public string Guid;
            public string Hash;
            public string MetaHash;
        }

        private readonly string _root;
        internal ImportedAssetLibrary(string libraryPath)
        {
            _root = System.IO.Path.Combine(libraryPath, "AssetManager", "Assets", "Imported");
        }

        internal IDisposable Lock(AssetFile file)
        {
            Directory.CreateDirectory(_root);
            return File.Open(System.IO.Path.Combine(_root, FileKey(file) + ".lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private static string FileKey(AssetFile file) => AssetIdentity.HashBytes(
            Encoding.UTF8.GetBytes(file.Id + "\n" + file.SourceType + "\n" + file.SourceId)).Substring(0, 32);

        internal static string SourceHash(AssetFile file) => AssetIdentity.HashBytes(
            Encoding.UTF8.GetBytes(AssetIdentity.HashFile(file.SourcePath) + "\n" +
                (File.Exists(file.SourcePath + ".meta") ? AssetIdentity.HashFile(file.SourcePath + ".meta") : "")));

        internal string Revision(AssetFile file, string sourceHash, string target) =>
            System.IO.Path.Combine(_root, FileKey(file), AssetIdentity.HashBytes(Encoding.UTF8.GetBytes(
                sourceHash + "\n" + target.Replace('\\', '/').ToLowerInvariant())).Substring(0, 32));

        internal Asset[] Read(string revision)
        {
            var manifest = System.IO.Path.Combine(revision, "manifest.json");
            if (!File.Exists(manifest)) return Array.Empty<Asset>();
            var assets = JsonConvert.DeserializeObject<Asset[]>(File.ReadAllText(manifest))
                ?? throw new InvalidDataException("Invalid imported Asset manifest.");
            foreach (var asset in assets)
            {
                // Paths are relative to the raw import root, or start with Assets/ for packages.
                var path = ContentPath(revision, asset);
                if (AssetIdentity.HashFile(path + ".meta") != asset.MetaHash ||
                    AssetIdentity.ReadGuid(File.ReadAllText(path + ".meta")) != asset.Guid ||
                    (!asset.IsFolder && !asset.IsSourceAsset && AssetIdentity.HashFile(path) != asset.Hash))
                    throw new InvalidDataException("The shared imported Asset does not match its manifest: " + asset.Path);
            }
            return assets;
        }

        internal static string ContentPath(string revision, Asset asset)
        {
            var relative = asset.Path;
            if (string.IsNullOrEmpty(relative) || relative.Contains(':') || relative.Contains('\\') ||
                relative.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("Invalid imported Asset path: " + relative);
            return System.IO.Path.Combine(revision, "content", AssetIdentity.HashBytes(Encoding.UTF8.GetBytes(
                (asset.IsImportRoot ? "root" : asset.IsProjectPath ? "project" : "relative") + "\n" + relative)).Substring(0, 32));
        }

        internal void Save(string revision, Asset[] assets, Func<Asset, string> source)
        {
            if (Directory.Exists(revision))
            {
                // Keep existing identities and bytes immutable, but retain assets generated
                // by later imports of the same source revision as well.
                var saved = Read(revision);
                var paths = saved.Select(asset => ContentPath(revision, asset)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var additions = assets.Where(asset => !paths.Contains(ContentPath(revision, asset))).ToArray();
                if (additions.Length == 0) return;
                foreach (var asset in additions)
                    CopyContent(ContentPath(revision, asset), asset, source(asset), true);
                // Content is written before atomically publishing the expanded manifest.
                WriteManifest(revision, saved.Concat(additions).OrderBy(asset => asset.Path, StringComparer.Ordinal).ToArray());
                return;
            }
            var staging = revision + "." + Guid.NewGuid().ToString("N").Substring(0, 16) + ".tmp";
            try
            {
                Directory.CreateDirectory(staging);
                foreach (var asset in assets)
                {
                    var destination = ContentPath(staging, asset);
                    CopyContent(destination, asset, source(asset), false);
                }
                WriteManifest(staging, assets);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(revision));
                Directory.Move(staging, revision);
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }

        private static void CopyContent(string destination, Asset asset, string original, bool overwrite)
        {
            Directory.CreateDirectory(asset.IsFolder ? destination : System.IO.Path.GetDirectoryName(destination));
            // Source bodies remain in the registered File (or its ZIP entry).
            if (!asset.IsFolder && !asset.IsSourceAsset) File.Copy(original, destination, overwrite);
            File.Copy(original + ".meta", destination + ".meta", overwrite);
        }

        private static void WriteManifest(string revision, Asset[] assets)
        {
            var path = System.IO.Path.Combine(revision, "manifest.json");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(assets, Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

    }

    internal static class AssetIdentity
    {
        private static readonly Regex GuidLine = new Regex(@"(?m)^guid: ([0-9a-fA-F]{32})\r?$", RegexOptions.CultureInvariant);

        internal static string ReadGuid(string meta) => GuidLine.Match(meta).Groups[1].Value.ToLowerInvariant();
        internal static string Settings(string meta) => GuidLine.Replace(meta.Replace("\r\n", "\n"), "guid: <identity>");
        internal static bool Equivalent(string source, string destination, bool folder = false) =>
            File.Exists(source + ".meta") && File.Exists(destination + ".meta") &&
            (folder ? Directory.Exists(destination) : File.Exists(destination) && HashFile(source) == HashFile(destination)) &&
            Settings(File.ReadAllText(source + ".meta")) == Settings(File.ReadAllText(destination + ".meta"));

        internal static string SnapshotHash(string path) => HashBytes(System.Text.Encoding.UTF8.GetBytes(
            HashFile(path) + "\n" + Settings(File.ReadAllText(path + ".meta"))));

        internal static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal static string HashBytes(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
