using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Newtonsoft.Json;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class GitAssetVariantRepository : IAssetVariantRepository
    {
        private const string MasterRef = "refs/heads/master";
        private const string BranchPrefix = "refs/heads/variant/";
        private const string TagPrefix = "refs/tags/variant/";
        private const string VariantFolder = "variants/";
        private const string GalleryFolder = "galleries/";
        private readonly string _root;
        private readonly string _previewCacheRoot;
        private readonly string _stagingRoot;

        private sealed class VariantMetadata
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string RootAssetPath { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        internal GitAssetVariantRepository(string libraryPath)
        {
            _root = Path.Combine(Path.GetFullPath(libraryPath), "AssetManager", "Assets", "Variant");
            _previewCacheRoot = Path.Combine(Path.GetFullPath(libraryPath), "cache", "asset-manager", "variant-thumbnails");
            _stagingRoot = Path.Combine(Path.GetTempPath(), "ee4v-variant");
        }

        public IReadOnlyList<AssetVariantSnapshot> ReadAll()
        {
            var result = new List<AssetVariantSnapshot>();
            foreach (var repository in Repositories())
            {
                var assetId = Path.GetFileNameWithoutExtension(repository);
                var metadataPaths = new HashSet<string>(Git(repository, null, null,
                    "ls-tree", "-r", "--name-only", MasterRef, "--", VariantFolder)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                var snapshots = ReadRefs(repository, TagPrefix).Select(pair =>
                {
                    var variantId = pair.Key.Substring(TagPrefix.Length).Split('/')[0];
                    var snapshot = ReadCommit(repository, pair.Value, variantId);
                    if (snapshot.Variant.ParentItemId != assetId ||
                        pair.Key != RevisionRef(snapshot))
                    {
                        throw new InvalidDataException("A Variant revision tag is invalid.");
                    }
                    return snapshot;
                }).ToArray();
                foreach (var group in snapshots.GroupBy(snapshot => snapshot.Variant.Id))
                {
                    var latest = group.OrderByDescending(snapshot => snapshot.Revision.Number).First();
                    var metadataPath = VariantFolder + group.Key + "/metadata.json";
                    if (metadataPaths.Contains(metadataPath))
                    {
                        var metadata = JsonConvert.DeserializeObject<VariantMetadata>(Git(repository, null, null,
                            "show", MasterRef + ":" + metadataPath));
                        if (metadata == null || string.IsNullOrWhiteSpace(metadata.Name))
                        {
                            throw new InvalidDataException("Invalid Variant metadata.");
                        }
                        latest.Variant.Name = metadata.Name;
                        latest.Variant.Description = metadata.Description ?? string.Empty;
                        if (!string.IsNullOrEmpty(metadata.RootAssetPath))
                        {
                            ValidateAssetPath(metadata.RootAssetPath);
                            latest.Variant.RootAssetPath = metadata.RootAssetPath;
                        }
                        latest.Variant.UpdatedAt = metadata.UpdatedAt;
                    }
                    foreach (var snapshot in group)
                    {
                        snapshot.Variant.HeadRevisionId = latest.Revision.Id;
                        result.Add(snapshot);
                    }
                }
            }
            return result;
        }

        public AssetVariantSnapshot Read(string variantId, string revisionId)
        {
            ValidateId(variantId);
            ValidateId(revisionId);
            foreach (var repository in Repositories())
            {
                var tag = ReadRefs(repository, TagPrefix + variantId + "/")
                    .SingleOrDefault(pair => pair.Key.EndsWith("-" + revisionId, StringComparison.Ordinal));
                if (tag.Value == null) { continue; }
                var snapshot = ReadCommit(repository, tag.Value, variantId);
                if (snapshot.Variant.Id != variantId || snapshot.Revision.Id != revisionId ||
                    snapshot.Variant.ParentItemId != Path.GetFileNameWithoutExtension(repository) ||
                    tag.Key != RevisionRef(snapshot))
                {
                    throw new InvalidDataException("A Variant revision does not match its tag.");
                }
                return snapshot;
            }
            throw new InvalidOperationException("The Variant revision was not found.");
        }

        public AssetVariantSnapshot Save(AssetVariantSnapshot snapshot, string stagingPath)
        {
            ValidateId(snapshot.Variant.Id);
            ValidateId(snapshot.Variant.ParentItemId);
            ValidateId(snapshot.Variant.SourcePrefabGuid);
            var previewCachePath = PreviewCachePath(snapshot.PreviewHash);
            Directory.CreateDirectory(_root);
            var repository = RepositoryPath(snapshot.Variant.ParentItemId);
            using (new FileStream(Path.Combine(_root, snapshot.Variant.ParentItemId + ".save.lock"),
                       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var otherRepository = FindRepository(snapshot.Variant.Id) ?? FindGalleryRepository(snapshot.Variant.Id);
                if (otherRepository != null && otherRepository != repository)
                {
                    throw new InvalidOperationException("A saved Variant cannot change its owning AssetManager asset.");
                }
                var masterHead = InitializeRepository(repository, snapshot.Variant.ParentItemId);
                var variantRef = BranchPrefix + snapshot.Variant.Id;
                ReadRefs(repository, variantRef).TryGetValue(variantRef, out var variantHead);
                var revisions = ReadRefs(repository, TagPrefix + snapshot.Variant.Id + "/")
                    .Values.Select(commit => ReadCommit(repository, commit, snapshot.Variant.Id)).ToArray();
                var latest = revisions.OrderByDescending(revision => revision.Revision.Number).FirstOrDefault();
                AssetVariantSnapshot parent = null;
                if (!string.IsNullOrEmpty(snapshot.Revision.ParentRevisionId))
                {
                    parent = Read(snapshot.Variant.Id, snapshot.Revision.ParentRevisionId);
                }
                else
                {
                    parent = latest;
                }
                if (latest != null && latest.ContentHash == snapshot.ContentHash)
                {
                    return latest;
                }
                snapshot.Revision.Id = Guid.NewGuid().ToString("N");
                snapshot.Revision.Number = revisions.Select(revision => revision.Revision.Number)
                    .DefaultIfEmpty(0).Max() + 1;
                snapshot.Revision.ParentRevisionId = parent?.Revision.Id;
                snapshot.Revision.CreatedAt = DateTime.UtcNow;
                snapshot.Variant.HeadRevisionId = snapshot.Revision.Id;
                snapshot.Variant.UpdatedAt = snapshot.Revision.CreatedAt;
                var variantPath = VariantFolder + snapshot.Variant.Id;
                var variantStage = Path.Combine(stagingPath, variantPath);
                Directory.CreateDirectory(variantStage);
                Directory.Move(Path.Combine(stagingPath, "Assets"), Path.Combine(variantStage, "Assets"));
                File.Move(Path.Combine(stagingPath, "preview.png"), Path.Combine(variantStage, "preview.png"));
                File.WriteAllText(Path.Combine(variantStage, "manifest.json"),
                    JsonConvert.SerializeObject(snapshot, Formatting.Indented), new UTF8Encoding(false));
                WriteMetadata(variantStage, snapshot.Variant);
                File.WriteAllText(Path.Combine(stagingPath, ".gitattributes"),
                    "* -filter -text\n", new UTF8Encoding(false));
                WriteAssetMarker(stagingPath, snapshot.Variant.ParentItemId);
                var index = stagingPath + ".index";
                try
                {
                    var variantParent = variantHead ?? Git(repository, null, null,
                        "rev-list", "--first-parent", "--max-parents=0", masterHead).Trim();
                    Git(repository, stagingPath, index, "read-tree", variantParent);
                    Git(repository, stagingPath, index, "add", "--force", "--all", "--",
                        variantPath, ".gitattributes", "asset.json");
                    var variantTree = Git(repository, stagingPath, index, "write-tree").Trim();
                    var commit = Git(repository, null, null, "commit-tree", variantTree, "-p", variantParent,
                        "-m", "Save Variant " + snapshot.Variant.Id + " v" + snapshot.Revision.Number).Trim();

                    // Integrate only this Variant's folder and preserve all other latest folders.
                    Git(repository, stagingPath, index, "read-tree", masterHead);
                    Git(repository, stagingPath, index, "add", "--force", "--all", "--",
                        variantPath, ".gitattributes", "asset.json");
                    var masterTree = Git(repository, stagingPath, index, "write-tree").Trim();
                    var masterCommit = Git(repository, null, null, "commit-tree", masterTree,
                        "-p", masterHead, "-p", commit,
                        "-m", "Integrate Variant " + snapshot.Variant.Id + " v" + snapshot.Revision.Number).Trim();
                    var transaction = "start\ncreate " +
                        RevisionRef(snapshot) + " " + commit +
                        "\nupdate " + variantRef + " " + commit + " " +
                        (variantHead ?? new string('0', commit.Length)) +
                        "\nupdate " + MasterRef + " " + masterCommit + " " + masterHead +
                        "\nprepare\ncommit\n";
                    Git(repository, null, null, new[] { "update-ref", "--stdin" }, transaction);
                    snapshot.Revision.CommitId = commit;
                    Git(repository, null, null, "symbolic-ref", "HEAD", MasterRef);
                    var preview = TryReadPreview(Path.Combine(variantStage, "preview.png"), snapshot.PreviewHash);
                    if (preview != null) { TryWritePreview(previewCachePath, preview); }
                    return snapshot;
                }
                finally
                {
                    if (File.Exists(index)) { File.Delete(index); }
                }
            }
        }

        public void UpdateMetadata(AssetVariant variant)
        {
            ValidateId(variant.Id);
            ValidateId(variant.ParentItemId);
            if (string.IsNullOrWhiteSpace(variant.Name))
            {
                throw new InvalidDataException("A Variant name is required.");
            }
            var repository = RepositoryPath(variant.ParentItemId);
            using (new FileStream(Path.Combine(_root, variant.ParentItemId + ".save.lock"),
                       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (!Directory.Exists(repository) || ReadRefs(repository, TagPrefix + variant.Id + "/").Count == 0)
                {
                    throw new InvalidOperationException("The saved Variant was not found.");
                }
                var masterHead = ReadRefs(repository, MasterRef)[MasterRef];
                var stage = NewStagingPath();
                var index = stage + ".index";
                try
                {
                    var variantPath = VariantFolder + variant.Id;
                    var variantStage = Path.Combine(stage, variantPath);
                    Directory.CreateDirectory(variantStage);
                    WriteMetadata(variantStage, variant);
                    Git(repository, stage, index, "read-tree", masterHead);
                    Git(repository, stage, index, "add", "--force", "--", variantPath + "/metadata.json");
                    var tree = Git(repository, stage, index, "write-tree").Trim();
                    var commit = Git(repository, null, null, "commit-tree", tree, "-p", masterHead,
                        "-m", "Update Variant metadata " + variant.Id).Trim();
                    Git(repository, null, null, "update-ref", MasterRef, commit, masterHead);
                }
                finally
                {
                    if (File.Exists(index)) { File.Delete(index); }
                    DeleteStaging(stage);
                }
            }
        }

        private static void WriteMetadata(string variantStage, AssetVariant variant) =>
            File.WriteAllText(Path.Combine(variantStage, "metadata.json"),
                JsonConvert.SerializeObject(new VariantMetadata
                {
                    Name = variant.Name, Description = variant.Description ?? string.Empty,
                    RootAssetPath = variant.RootAssetPath,
                    UpdatedAt = variant.UpdatedAt
                }, Formatting.Indented), new UTF8Encoding(false));

        public string Extract(AssetVariantSnapshot snapshot)
        {
            var stage = NewStagingPath();
            var index = stage + ".index";
            try
            {
                var repository = RepositoryPath(snapshot.Variant.ParentItemId);
                Git(repository, stage, index, "read-tree",
                    snapshot.Revision.CommitId + ":" + VariantFolder + snapshot.Variant.Id);
                Git(repository, stage, index, "checkout-index", "--all", "--force");
                return stage;
            }
            catch
            {
                DeleteStaging(stage);
                throw;
            }
            finally
            {
                if (File.Exists(index)) { File.Delete(index); }
            }
        }

        public byte[] ReadPreview(AssetVariantSnapshot snapshot)
        {
            var cachePath = PreviewCachePath(snapshot.PreviewHash);
            var cached = TryReadPreview(cachePath, snapshot.PreviewHash);
            if (cached != null) { return cached; }
            var bytes = ReadBlob(RepositoryPath(snapshot.Variant.ParentItemId),
                snapshot.Revision.CommitId + ":" + VariantFolder + snapshot.Variant.Id + "/preview.png");
            if (!HasPreviewHash(bytes, snapshot.PreviewHash))
            {
                throw new InvalidDataException("The Variant preview does not match its saved hash.");
            }
            TryWritePreview(cachePath, bytes);
            return bytes;
        }

        private static byte[] ReadBlob(string repository, string objectPath)
        {
            var start = new ProcessStartInfo("git", string.Join(" ", new[]
            {
                "--no-pager", "--git-dir=" + repository, "show", objectPath
            }.Select(Quote)))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var key in start.EnvironmentVariables.Keys.Cast<string>()
                         .Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToArray())
            {
                start.EnvironmentVariables.Remove(key);
            }
            using (var process = Process.Start(start))
            using (var output = new MemoryStream())
            {
                var copy = process.StandardOutput.BaseStream.CopyToAsync(output);
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    throw new TimeoutException("Git did not finish within 60 seconds.");
                }
                copy.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Variant image could not be read: " +
                        error.GetAwaiter().GetResult().Trim());
                }
                return output.ToArray();
            }
        }

        public IReadOnlyList<AssetVariantGalleryImage> ReadGallery(string variantId)
        {
            ValidateId(variantId);
            var repository = FindGalleryRepository(variantId);
            return repository == null ? Array.Empty<AssetVariantGalleryImage>() : ReadGallery(repository, variantId);
        }

        private AssetVariantGalleryImage[] ReadGallery(string repository, string variantId)
        {
            var path = GalleryFolder + variantId + "/gallery.json";
            if (string.IsNullOrWhiteSpace(Git(repository, null, null,
                    "ls-tree", "--name-only", MasterRef, "--", path)))
            {
                return Array.Empty<AssetVariantGalleryImage>();
            }
            var images = JsonConvert.DeserializeObject<AssetVariantGalleryImage[]>(
                Git(repository, null, null, "show", MasterRef + ":" + path));
            if (images == null || images.Any(image => image == null ||
                    !Regex.IsMatch(image.Id ?? string.Empty, "\\A[0-9a-f]{64}\\z")) ||
                images.Select(image => image.Id).Distinct(StringComparer.Ordinal).Count() != images.Length)
            {
                throw new InvalidDataException("Invalid Variant gallery.");
            }
            return images.Reverse().ToArray();
        }

        public byte[] ReadGalleryImage(string variantId, string imageId)
        {
            ValidateId(variantId);
            var cachePath = PreviewCachePath(imageId);
            var repository = FindGalleryRepository(variantId);
            if (repository == null || !ReadGallery(repository, variantId).Any(image => image.Id == imageId))
            {
                throw new InvalidOperationException("The Variant gallery image was not found.");
            }
            var cached = TryReadPreview(cachePath, imageId);
            if (cached != null) { return cached; }
            var bytes = ReadBlob(repository, MasterRef + ":" + GalleryFolder + variantId + "/" + imageId + ".image");
            if (!HasPreviewHash(bytes, imageId)) { throw new InvalidDataException("Invalid Variant gallery image hash."); }
            TryWritePreview(cachePath, bytes);
            return bytes;
        }

        public void AddGalleryImages(string variantId, string parentItemId,
            IReadOnlyList<AssetVariantGalleryUpload> uploads)
        {
            ValidateId(variantId);
            ValidateId(parentItemId);
            var additions = new List<AssetVariantGalleryImage>();
            foreach (var upload in uploads)
            {
                var bytes = upload?.Data;
                var png = bytes != null && bytes.Length >= 8 &&
                    bytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                var jpeg = bytes != null && bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
                if ((!png && !jpeg) || bytes.Length > 16 * 1024 * 1024)
                {
                    throw new InvalidDataException("Gallery images must be PNG or JPEG files up to 16 MB.");
                }
                using (var hash = SHA256.Create())
                {
                    additions.Add(new AssetVariantGalleryImage
                    {
                        Id = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(),
                        FileName = Path.GetFileName(upload.FileName ?? string.Empty)
                    });
                }
            }
            Directory.CreateDirectory(_root);
            var repository = RepositoryPath(parentItemId);
            using (new FileStream(Path.Combine(_root, parentItemId + ".save.lock"),
                       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var existing = FindRepository(variantId) ?? FindGalleryRepository(variantId);
                if (existing != null && existing != repository)
                {
                    throw new InvalidOperationException("A Variant cannot change its owning AssetManager asset.");
                }
                var master = InitializeRepository(repository, parentItemId);
                var images = ReadGallery(repository, variantId).ToList();
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                for (var index = 0; index < additions.Count; index++)
                {
                    var image = additions[index];
                    if (images.Any(candidate => candidate.Id == image.Id)) { continue; }
                    images.Insert(0, image);
                    files.Add(image.Id, uploads[index].Data);
                }
                if (files.Count > 0) { WriteGallery(repository, variantId, master, images, files, null); }
            }
        }

        public void RemoveGalleryImage(string variantId, string imageId)
        {
            ValidateId(variantId);
            PreviewCachePath(imageId);
            var repository = FindGalleryRepository(variantId);
            if (repository == null) { return; }
            var parentItemId = Path.GetFileNameWithoutExtension(repository);
            using (new FileStream(Path.Combine(_root, parentItemId + ".save.lock"),
                       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var images = ReadGallery(repository, variantId).ToList();
                if (images.RemoveAll(image => image.Id == imageId) == 0) { return; }
                WriteGallery(repository, variantId, ReadRefs(repository, MasterRef)[MasterRef], images,
                    new Dictionary<string, byte[]>(), imageId);
            }
        }

        public void MoveGalleryImageToFront(string variantId, string imageId)
        {
            ValidateId(variantId);
            PreviewCachePath(imageId);
            var repository = FindGalleryRepository(variantId);
            if (repository == null) { throw new InvalidOperationException("The Variant gallery image was not found."); }
            var parentItemId = Path.GetFileNameWithoutExtension(repository);
            using (new FileStream(Path.Combine(_root, parentItemId + ".save.lock"),
                       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var images = ReadGallery(repository, variantId).ToList();
                var index = images.FindIndex(image => image.Id == imageId);
                if (index < 0) { throw new InvalidOperationException("The Variant gallery image was not found."); }
                if (index == 0) { return; }
                var image = images[index];
                images.RemoveAt(index);
                images.Insert(0, image);
                WriteGallery(repository, variantId, ReadRefs(repository, MasterRef)[MasterRef], images,
                    new Dictionary<string, byte[]>(), null);
            }
        }

        private void WriteGallery(string repository, string variantId, string master,
            IReadOnlyList<AssetVariantGalleryImage> images, IReadOnlyDictionary<string, byte[]> files, string removedId)
        {
            var stage = NewStagingPath();
            var index = stage + ".index";
            var path = GalleryFolder + variantId;
            try
            {
                var folder = Path.Combine(stage, path);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "gallery.json"), JsonConvert.SerializeObject(images.Reverse().ToArray(),
                    Formatting.Indented), new UTF8Encoding(false));
                Git(repository, stage, index, "read-tree", master);
                Git(repository, stage, index, "add", "--force", "--", path + "/gallery.json");
                foreach (var file in files)
                {
                    File.WriteAllBytes(Path.Combine(folder, file.Key + ".image"), file.Value);
                    Git(repository, stage, index, "add", "--force", "--", path + "/" + file.Key + ".image");
                }
                if (removedId != null)
                {
                    Git(repository, stage, index, "update-index", "--force-remove", "--", path + "/" + removedId + ".image");
                }
                var tree = Git(repository, stage, index, "write-tree").Trim();
                var commit = Git(repository, null, null, "commit-tree", tree, "-p", master,
                    "-m", "Update Variant gallery " + variantId).Trim();
                Git(repository, null, null, "update-ref", MasterRef, commit, master);
            }
            finally
            {
                if (File.Exists(index)) { File.Delete(index); }
                DeleteStaging(stage);
            }
        }

        private string FindGalleryRepository(string variantId) => Repositories().FirstOrDefault(repository =>
            !string.IsNullOrWhiteSpace(Git(repository, null, null, "ls-tree", "--name-only", MasterRef,
                "--", GalleryFolder + variantId + "/gallery.json")));

        private string PreviewCachePath(string previewHash)
        {
            if (!Regex.IsMatch(previewHash ?? string.Empty, "\\A[0-9a-f]{64}\\z"))
            {
                throw new InvalidDataException("Invalid Variant preview hash.");
            }
            return Path.Combine(_previewCacheRoot, previewHash + ".png");
        }

        private static byte[] TryReadPreview(string path, string expectedHash)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 16L * 1024 * 1024) { return null; }
                var bytes = File.ReadAllBytes(path);
                return HasPreviewHash(bytes, expectedHash) ? bytes : null;
            }
            catch (Exception exception) when (exception is IOException ||
                                             exception is UnauthorizedAccessException ||
                                             exception is System.Security.SecurityException)
            {
                return null;
            }
        }

        private static bool HasPreviewHash(byte[] bytes, string expectedHash)
        {
            using (var hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty)
                    .ToLowerInvariant() == expectedHash;
            }
        }

        private void TryWritePreview(string cachePath, byte[] bytes)
        {
            var temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(_previewCacheRoot);
                File.WriteAllBytes(temporaryPath, bytes);
                if (File.Exists(cachePath)) { File.Replace(temporaryPath, cachePath, null); }
                else { File.Move(temporaryPath, cachePath); }
            }
            catch (Exception exception) when (exception is IOException ||
                                             exception is UnauthorizedAccessException ||
                                             exception is NotSupportedException ||
                                             exception is System.Security.SecurityException)
            {
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) { File.Delete(temporaryPath); } }
                catch (Exception exception) when (exception is IOException ||
                                                 exception is UnauthorizedAccessException ||
                                                 exception is System.Security.SecurityException)
                {
                }
            }
        }

        internal string NewStagingPath()
        {
            var path = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public void DeleteStaging(string path)
        {
            var stagingRoot = Path.GetFullPath(_stagingRoot) +
                Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(path);
            if (!target.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The path is outside the Variant staging directory.");
            }
            if (Directory.Exists(target)) { Directory.Delete(target, true); }
        }

        private IEnumerable<string> Repositories() => Directory.Exists(_root)
            ? Directory.EnumerateDirectories(_root, "*.git")
                .Where(path => Regex.IsMatch(Path.GetFileNameWithoutExtension(path), "\\A[0-9a-f]{32}\\z") &&
                    File.Exists(Path.Combine(path, "HEAD"))).OrderBy(path => path, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

        private string RepositoryPath(string assetId)
        {
            ValidateId(assetId);
            return Path.Combine(_root, assetId + ".git");
        }

        private string FindRepository(string variantId) => Repositories().FirstOrDefault(repository =>
            ReadRefs(repository, TagPrefix + variantId + "/").Count > 0);

        private string InitializeRepository(string repository, string assetId)
        {
            if (!File.Exists(Path.Combine(repository, "HEAD")))
            {
                Git(repository, null, null, "init", "--bare", "--initial-branch=master", "--template=", repository);
            }
            var master = ReadRefs(repository, MasterRef);
            if (master.TryGetValue(MasterRef, out var commit)) { return commit; }
            var stage = NewStagingPath();
            var index = stage + ".index";
            try
            {
                WriteAssetMarker(stage, assetId);
                Git(repository, stage, index, "add", "--force", "--all");
                var tree = Git(repository, stage, index, "write-tree").Trim();
                commit = Git(repository, null, null, "commit-tree", tree,
                    "-m", "Initialize Asset variants " + assetId).Trim();
                Git(repository, null, null, "update-ref", MasterRef, commit,
                    new string('0', commit.Length));
                return commit;
            }
            finally
            {
                if (File.Exists(index)) { File.Delete(index); }
                DeleteStaging(stage);
            }
        }

        private static void WriteAssetMarker(string stage, string assetId) =>
            File.WriteAllText(Path.Combine(stage, "asset.json"),
                JsonConvert.SerializeObject(new { SchemaVersion = 1, AssetId = assetId },
                    Formatting.Indented), new UTF8Encoding(false));

        private Dictionary<string, string> ReadRefs(string repository, string prefix)
        {
            return Git(repository, null, null, "for-each-ref", "--format=%(refname) %(objectname)", prefix)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(' '))
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        }

        private AssetVariantSnapshot ReadCommit(string repository, string commit, string variantId)
        {
            ValidateId(variantId);
            if (!Regex.IsMatch(commit, "\\A[0-9a-f]{40}([0-9a-f]{24})?\\z"))
            {
                throw new InvalidDataException("Invalid Git commit ID.");
            }
            var snapshot = JsonConvert.DeserializeObject<AssetVariantSnapshot>(
                Git(repository, null, null, "show", commit + ":" + VariantFolder + variantId + "/manifest.json"));
            if (snapshot == null || snapshot.SchemaVersion != 1 ||
                snapshot.Variant == null || snapshot.Revision == null ||
                snapshot.Assets == null || snapshot.Dependencies == null)
            {
                throw new InvalidDataException("Invalid Variant manifest.");
            }
            ValidateId(snapshot.Variant.Id);
            ValidateId(snapshot.Variant.ParentItemId);
            ValidateId(snapshot.Variant.SourcePrefabGuid);
            ValidateId(snapshot.Revision.Id);
            if (snapshot.Variant.Id != variantId || snapshot.Revision.VariantId != snapshot.Variant.Id)
            {
                throw new InvalidDataException("Invalid Variant revision owner.");
            }
            foreach (var asset in snapshot.Assets)
            {
                ValidateAssetPath(asset.Path);
                ValidateId(asset.Guid);
            }
            ValidateAssetPath(snapshot.Variant.RootAssetPath);
            snapshot.ContentHash = ComputeContentHash(snapshot);
            snapshot.Revision.CommitId = commit;
            return snapshot;
        }

        internal static string ComputeContentHash(AssetVariantSnapshot snapshot)
        {
            var content = JsonConvert.SerializeObject(new
            {
                snapshot.Variant.Id, snapshot.Variant.SourcePrefabGuid,
                snapshot.Variant.Name, snapshot.Variant.Description,
                snapshot.Variant.ParentItemId, snapshot.Variant.RootAssetPath, snapshot.Assets,
                Dependencies = snapshot.Dependencies.Select(dependency => new
                {
                    dependency.SourceType, dependency.SourceId,
                    dependency.TargetPaths, dependency.AssetGuids
                })
            });
            using (var hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(content)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        internal static void ValidateAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains('\\') || path.Contains(':') ||
                path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                path.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
            {
                throw new InvalidDataException("Invalid Variant asset path: " + path);
            }
        }

        private static void ValidateId(string value)
        {
            if (value == null || !Regex.IsMatch(value, "\\A[0-9a-f]{32}\\z"))
            {
                throw new InvalidDataException("Invalid Variant or asset ID.");
            }
        }

        private static string RevisionRef(AssetVariantSnapshot snapshot) =>
            TagPrefix + snapshot.Variant.Id + "/v" + snapshot.Revision.Number + "-" + snapshot.Revision.Id;

        private string Git(string repository, string workTree, string index, params string[] arguments) =>
            Git(repository, workTree, index, arguments, null);

        private string Git(string repository, string workTree, string index, string[] arguments, string input)
        {
            var args = new List<string>
            {
                "-c", "core.autocrlf=false", "-c", "core.safecrlf=false",
                "-c", "core.longpaths=true",
                "-c", "core.quotePath=false", "-c", "user.name=ee4v",
                "-c", "user.email=ee4v@localhost", "--git-dir=" + repository
            };
            if (workTree != null) { args.Add("--work-tree=" + workTree); }
            args.AddRange(arguments);
            var start = new ProcessStartInfo("git", string.Join(" ", args.Select(Quote)))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                RedirectStandardInput = input != null,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var key in start.EnvironmentVariables.Keys.Cast<string>()
                         .Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToArray())
            {
                start.EnvironmentVariables.Remove(key);
            }
            if (index != null) { start.EnvironmentVariables["GIT_INDEX_FILE"] = index; }
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (input != null)
                {
                    process.StandardInput.Write(input);
                    process.StandardInput.Close();
                }
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    throw new TimeoutException("Git did not finish within 60 seconds.");
                }
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Variant Git operation failed: " +
                        error.GetAwaiter().GetResult().Trim());
                }
                return output.GetAwaiter().GetResult();
            }
        }

        private static string Quote(string argument)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }
    }
}
