using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Ee4v.AssetManager.Application;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.Files;
using Ee4v.AssetManager.Infrastructure.Persistence.SQLite;
using Ee4v.AssetManager.Infrastructure.Unity;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class SqliteAssetManagerStore :
        IAssetManagerStore
    {
        public AssetCatalogSnapshot LoadCatalogSnapshot() =>
            Execute(AssetManagerDatabase.LoadCatalogSnapshot);

        public AssetSearchResult SearchItems(AssetItemQuery query) =>
            Execute(() => AssetManagerDatabase.SearchItems(query));

        public AssetSearchResult SearchItemSummaries(AssetItemQuery query) =>
            Execute(() => AssetManagerDatabase.SearchItemSummaries(query));

        public AssetItem GetItem(string itemId) =>
            Execute(() => AssetManagerDatabase.GetItem(itemId));

        public AssetThumbnail GetThumbnail(string itemId) =>
            Execute(() => AssetManagerDatabase.GetThumbnail(itemId));

        public IReadOnlyDictionary<string, AssetThumbnail> GetThumbnails(IReadOnlyList<string> itemIds) =>
            Execute(() => AssetManagerDatabase.GetThumbnails(itemIds));

        public AssetItem CreateItem(CreateAssetItemRequest request) =>
            Execute(() => AssetManagerDatabase.CreateItem(request));

        public AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request) =>
            Execute(() => AssetManagerDatabase.UpdateItem(itemId, request));

        public IReadOnlyList<AssetTag> GetTags(string keyword) =>
            Execute(() => AssetManagerDatabase.GetTags(keyword));

        public AssetTag CreateTag(string name) =>
            Execute(() => AssetManagerDatabase.CreateTag(name));

        public void SetItemTags(string itemId, IReadOnlyList<string> tagIds) =>
            Execute(() => AssetManagerDatabase.SetItemTags(itemId, tagIds));

        public IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query) =>
            Execute(() => AssetManagerDatabase.GetFiles(itemId, query));

        public IReadOnlyList<AssetFile> GetUnassignedFiles(
            AssetFileQuery query) =>
            Execute(() =>
                AssetManagerDatabase.GetUnassignedFiles(query));

        public AssetFile GetFile(string fileId) =>
            Execute(() => AssetManagerDatabase.GetFile(fileId));

        public string GetFileOwnerItemId(string fileId) =>
            Execute(() =>
                AssetManagerDatabase.GetFileOwnerItemId(fileId));

        public AssetFile RegisterFile(string itemId, RegisterFileRequest request) =>
            Execute(() => AssetManagerDatabase.RegisterFile(itemId, request));

        public IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId) =>
            Execute(() => AssetManagerDatabase.GetVariantGroups(itemId));

        public AssetVariantGroup CreateVariantGroup(string itemId, CreateVariantGroupRequest request) =>
            Execute(() => AssetManagerDatabase.CreateVariantGroup(itemId, request));

        public AssetVariantGroup UpdateVariantGroup(string variantGroupId, UpdateVariantGroupRequest request) =>
            Execute(() => AssetManagerDatabase.UpdateVariantGroup(variantGroupId, request));

        public IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId) =>
            Execute(() => AssetManagerDatabase.GetVersionGroups(itemId));

        public AssetVersionGroup CreateVersionGroup(string itemId, CreateVersionGroupRequest request) =>
            Execute(() => AssetManagerDatabase.CreateVersionGroup(itemId, request));

        public AssetVersionGroup UpdateVersionGroup(string versionGroupId, UpdateVersionGroupRequest request) =>
            Execute(() => AssetManagerDatabase.UpdateVersionGroup(versionGroupId, request));

        public string SetVersionGroupPrimaryFile(string versionGroupId, string fileId) =>
            Execute(() => AssetManagerDatabase.SetVersionGroupPrimaryFile(versionGroupId, fileId));

        public void ArchiveFile(string fileId) =>
            Execute(() => AssetManagerDatabase.ArchiveFile(fileId));

        public AssetFilePathResolution ResolveFilePath(string fileId) =>
            Execute(() => AssetManagerDatabase.ResolveFilePath(fileId));

        public IReadOnlyList<AssetCollection> GetCollections() =>
            Execute(AssetManagerDatabase.GetCollections);

        public AssetCollection CreateCollection(CreateCollectionRequest request) =>
            Execute(() => AssetManagerDatabase.CreateCollection(request));

        public AssetCollection CreateSmartCollection(CreateSmartCollectionRequest request) =>
            Execute(() => AssetManagerDatabase.CreateSmartCollection(request));

        public AssetCollection UpdateCollection(
            string collectionId,
            UpdateCollectionRequest request) =>
            Execute(() => AssetManagerDatabase.UpdateCollection(
                collectionId,
                request));

        public AssetCollection UpdateSmartCollection(
            string collectionId,
            UpdateSmartCollectionRequest request) =>
            Execute(() => AssetManagerDatabase.UpdateSmartCollection(
                collectionId,
                request));

        public bool DeleteCollections(
            IReadOnlyList<string> collectionIds) =>
            Execute(() => AssetManagerDatabase.DeleteCollections(
                collectionIds));

        public void MoveCollections(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex) =>
            Execute(() => AssetManagerDatabase.MoveCollections(
                collectionIds,
                parentCollectionId,
                siblingIndex));

        public void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds) =>
            Execute(() => AssetManagerDatabase.SetItemCollections(itemId, collectionIds));

        public bool AddItemsToCollection(
            IReadOnlyList<string> itemIds,
            string collectionId) =>
            Execute(() => AssetManagerDatabase.AddItemsToCollection(
                itemIds,
                collectionId));

        public IReadOnlyList<AssetFileDependency> GetFileDependencies(string fileId) =>
            Execute(() => AssetManagerDatabase.GetFileDependencies(fileId));

        public void SetFileDependencies(string dependentFileId, IReadOnlyList<string> dependencyFileIds) =>
            Execute(() => AssetManagerDatabase.SetFileDependencies(dependentFileId, dependencyFileIds));

        public IReadOnlyList<AssetDependency> GetDependencies(DependencyEndpointRequest source) =>
            Execute(() => AssetManagerDatabase.GetDependencies(source));

        public void SetDependencies(
            DependencyEndpointRequest source,
            IReadOnlyList<DependencyEndpointRequest> targets) =>
            Execute(() => AssetManagerDatabase.SetDependencies(source, targets));

        public IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId) =>
            Execute(() => AssetManagerDatabase.GetFileImportTargets(fileId));

        public void ReplaceFileImportTargets(
            string fileId,
            IReadOnlyList<string> normalizedRelativePaths) =>
            Execute(() => AssetManagerDatabase.ReplaceFileImportTargets(fileId, normalizedRelativePaths));

        public IReadOnlyList<string> GetFileImportedAssetGuids(
            string fileId) =>
            Execute(() =>
                AssetManagerDatabase.GetFileImportedAssetGuids(fileId));

        public IReadOnlyList<string> GetItemImportedAssetGuids(
            string itemId) =>
            Execute(() =>
                AssetManagerDatabase.GetItemImportedAssetGuids(itemId));

        public IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations() =>
            Execute(AssetManagerDatabase.GetImportedAssetAssociations);

        public void ReplaceFileImportedAssetGuids(
            string fileId,
            IReadOnlyList<string> assetGuids) =>
            Execute(() =>
                AssetManagerDatabase.ReplaceFileImportedAssetGuids(
                    fileId,
                    assetGuids));

        public void SetImportedAssetProtection(
            string assetGuid,
            bool isProtected) =>
            Execute(() =>
                AssetManagerDatabase.SetImportedAssetProtection(
                    assetGuid,
                    isProtected));

        public AssetSyncResult SyncBlm(BlmSyncRequest request) =>
            Execute(() => AssetManagerDatabase.SyncBlm(request));

        public AssetSyncResult SyncEagle(EagleSyncRequest request) =>
            Execute(() => AssetManagerDatabase.SyncEagle(request));

        public PreparedAssetSync PrepareBlmSync(BlmSyncRequest request)
        {
            var prepared = Execute(() => AssetManagerDatabase.PrepareBlmSync(request));
            return new PreparedAssetSync(prepared.Preview, prepared);
        }

        public PreparedAssetSync PrepareEagleSync(EagleSyncRequest request)
        {
            var prepared = Execute(() => AssetManagerDatabase.PrepareEagleSync(request));
            return new PreparedAssetSync(prepared.Preview, prepared);
        }

        public AssetSyncResult ApplyPreparedSync(
            PreparedAssetSync prepared,
            bool overwriteItemText)
        {
            if (prepared == null)
            {
                throw new ArgumentNullException(nameof(prepared));
            }

            var blm = prepared.AdapterState as PreparedBlmSync;
            if (blm != null)
            {
                return Execute(() =>
                    AssetManagerDatabase.ApplyPreparedBlmSync(blm, overwriteItemText));
            }

            var eagle = prepared.AdapterState as PreparedEagleSync;
            if (eagle != null)
            {
                return Execute(() =>
                    AssetManagerDatabase.ApplyPreparedEagleSync(eagle, overwriteItemText));
            }

            throw new AssetManagerException(
                AssetManagerErrorCode.InvalidRequest,
                "Prepared sync state does not belong to this adapter.");
        }

        public IReadOnlyList<AssetSyncInfo> GetSyncInfo() =>
            Execute(AssetManagerDatabase.GetSyncInfo);

        private static T Execute<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (SQLiteException exception)
            {
                throw AssetManagerDatabase.ToAssetManagerException(exception);
            }
        }

        private static void Execute(Action action)
        {
            Execute(() =>
            {
                action();
                return true;
            });
        }
    }

    internal sealed class UnityAssetImportGateway : IAssetImportGateway
    {
        private readonly IAssetFileImportEnvironment _environment;
        private readonly AssetProtectionService _protection;

        internal UnityAssetImportGateway(
            IAssetFileImportEnvironment environment,
            AssetProtectionService protection = null)
        {
            _environment = environment ??
                throw new ArgumentNullException(nameof(environment));
            _protection = protection;
        }

        public void Import(
            AssetImportPlan plan,
            Action<AssetImportResult> completed)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            _protection?.BeginImport(plan.FileId);
            try
            {
                AssetFileImportService.Import(
                    plan.AssetName,
                    plan.AssetFileName,
                    plan.SourcePath,
                    plan.RelativePaths,
                    _environment,
                    AssetManagerInfrastructureSettings.Current.ShowUnityPackageImportDialog,
                    result =>
                    {
                        try
                        {
                            completed?.Invoke(
                                new AssetImportResult(
                                    result.Succeeded,
                                    result.AssetGuids));
                        }
                        finally
                        {
                            _protection?.EndImport(
                                plan.FileId);
                        }
                    });
            }
            catch
            {
                _protection?.EndImport(plan.FileId);
                throw;
            }
        }
    }

    internal sealed class CachedAssetArchiveReader : IAssetArchiveReader
    {
        public string CacheDirectory => AssetFileTreeCache.ResolveCacheDirectory();

        public IReadOnlyList<AssetArchiveEntry> ReadZipEntries(
            string zipPath,
            System.Threading.CancellationToken cancellationToken) =>
            AssetFileTreeCache.ReadZipEntries(CacheDirectory, zipPath, cancellationToken);

        public AssetArchiveContent ReadZipContent(
            string zipPath,
            System.Threading.CancellationToken
                cancellationToken)
        {
            var entries = ReadZipEntries(
                zipPath,
                cancellationToken);
            var source = new FileInfo(zipPath);
            return new AssetArchiveContent(
                AssetArchiveContentKind.Zip,
                source.Length,
                entries
                    .Select(entry =>
                        new AssetArchiveContentEntry(
                            entry.FullName.TrimEnd(
                                '/',
                                '\\'),
                            entry.FullName.EndsWith(
                                "/",
                                StringComparison.Ordinal) ||
                            entry.FullName.EndsWith(
                                "\\",
                                StringComparison.Ordinal)
                                ? AssetArchiveContentEntryKind
                                    .Directory
                                : AssetArchiveContentEntryKind
                                    .File,
                            entry.Length,
                            entry.ArchiveFullName))
                    .Where(entry =>
                        !string.IsNullOrWhiteSpace(
                            entry.Path))
                    .ToArray());
        }

        public AssetArchiveContent
            ReadUnityPackageContent(
                string packagePath,
                System.Threading.CancellationToken
                    cancellationToken)
        {
            var source = new FileInfo(packagePath);
            if (!source.Exists)
            {
                throw new FileNotFoundException(
                    "UnityPackage was not found.",
                    packagePath);
            }

            var snapshot =
                UnityPackageContentReader.Read(
                    source.FullName,
                    cancellationToken);
            return new AssetArchiveContent(
                AssetArchiveContentKind.UnityPackage,
                source.Length,
                snapshot.Entries);
        }

        public AssetArchiveContent
            ReadUnityPackageContentFromZip(
                string zipPath,
                string entryPath,
                System.Threading.CancellationToken
                    cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                throw new ArgumentException(
                    "ZIP path is required.",
                    nameof(zipPath));
            }

            if (string.IsNullOrWhiteSpace(entryPath))
            {
                throw new ArgumentException(
                    "ZIP entry path is required.",
                    nameof(entryPath));
            }

            using (var stream = File.Open(
                       zipPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Read,
                       false))
            {
                var entry = FindEntry(
                    archive,
                    entryPath,
                    cancellationToken);

                if (entry == null)
                {
                    throw new FileNotFoundException(
                        "UnityPackage ZIP entry was not found.",
                        entryPath);
                }

                using (var packageStream = entry.Open())
                {
                    var snapshot =
                        UnityPackageContentReader.Read(
                            packageStream,
                            cancellationToken);
                    return new AssetArchiveContent(
                        AssetArchiveContentKind
                            .UnityPackage,
                        entry.Length,
                        snapshot.Entries);
                }
            }
        }

        public byte[] ReadEntryBytes(
            AssetArchiveContentKind kind,
            string archivePath,
            string packageEntryPath,
            string contentEntryPath,
            long maximumBytes,
            System.Threading.CancellationToken
                cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(
                    archivePath) ||
                string.IsNullOrWhiteSpace(
                    contentEntryPath) ||
                maximumBytes < 0L)
            {
                return null;
            }

            if (kind == AssetArchiveContentKind.Zip)
            {
                using (var stream = File.Open(
                           archivePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                using (var archive = new ZipArchive(
                           stream,
                           ZipArchiveMode.Read,
                           false))
                {
                    var entry = FindEntry(
                        archive,
                        contentEntryPath,
                        cancellationToken);
                    return ReadZipEntryBytes(
                        entry,
                        maximumBytes,
                        cancellationToken);
                }
            }

            if (string.IsNullOrWhiteSpace(
                    packageEntryPath))
            {
                using (var packageStream = File.Open(
                           archivePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                {
                    return UnityPackageContentReader
                        .ReadEntry(
                            packageStream,
                            contentEntryPath,
                            maximumBytes,
                            cancellationToken);
                }
            }

            using (var stream = File.Open(
                       archivePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Read,
                       false))
            {
                var packageEntry = FindEntry(
                    archive,
                    packageEntryPath,
                    cancellationToken);
                if (packageEntry == null)
                {
                    return null;
                }

                using (var packageStream =
                       packageEntry.Open())
                {
                    return UnityPackageContentReader
                        .ReadEntry(
                            packageStream,
                            contentEntryPath,
                            maximumBytes,
                            cancellationToken);
                }
            }
        }

        private static ZipArchiveEntry FindEntry(
            ZipArchive archive,
            string entryPath,
            System.Threading.CancellationToken
                cancellationToken)
        {
            if (archive == null)
            {
                return null;
            }

            for (var i = 0;
                 i < archive.Entries.Count;
                 i++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
                if (string.Equals(
                        archive.Entries[i].FullName,
                        entryPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return archive.Entries[i];
                }
            }

            return null;
        }

        private static byte[] ReadZipEntryBytes(
            ZipArchiveEntry entry,
            long maximumBytes,
            System.Threading.CancellationToken
                cancellationToken)
        {
            if (entry == null ||
                entry.Length > maximumBytes ||
                entry.Length > int.MaxValue)
            {
                return null;
            }

            var bytes = new byte[(int)entry.Length];
            using (var source = entry.Open())
            {
                var offset = 0;
                while (offset < bytes.Length)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    var read = source.Read(
                        bytes,
                        offset,
                        bytes.Length - offset);
                    if (read == 0)
                    {
                        throw new InvalidDataException(
                            "ZIP entry ended unexpectedly.");
                    }

                    offset += read;
                }
            }

            return bytes;
        }
    }

}
