using System;
using System.Collections.Generic;
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

        public AssetFile RegisterFile(string itemId, RegisterFileRequest request) =>
            Execute(() => AssetManagerDatabase.RegisterFile(itemId, request));

        public IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId) =>
            Execute(() => AssetManagerDatabase.GetVariantGroups(itemId));

        public AssetVariantGroup CreateVariantGroup(string itemId, CreateVariantGroupRequest request) =>
            Execute(() => AssetManagerDatabase.CreateVariantGroup(itemId, request));

        public IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId) =>
            Execute(() => AssetManagerDatabase.GetVersionGroups(itemId));

        public AssetVersionGroup CreateVersionGroup(string itemId, CreateVersionGroupRequest request) =>
            Execute(() => AssetManagerDatabase.CreateVersionGroup(itemId, request));

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

        public void MoveCollection(
            string collectionId,
            string parentCollectionId,
            int siblingIndex) =>
            Execute(() => AssetManagerDatabase.MoveCollection(
                collectionId,
                parentCollectionId,
                siblingIndex));

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
        private readonly AssetProtectionService _protection;

        internal UnityAssetImportGateway(
            AssetProtectionService protection = null)
        {
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
                    new UnityAssetFileImportEnvironment(),
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
    }

    internal static class AssetManagerInfrastructure
    {
        internal static void ConfigureSettings(IAssetManagerInfrastructureSettings settings)
        {
            AssetManagerInfrastructureSettings.Configure(settings);
        }

        internal static IAssetManager CreateDefault()
        {
            return CreateDefaultService();
        }

        internal static AssetManagerService CreateDefaultService()
        {
            return CreateDefaultService(null);
        }

        internal static AssetManagerService CreateDefaultService(
            AssetProtectionService protection)
        {
            var store = new SqliteAssetManagerStore();
            return new AssetManagerService(
                store,
                new UnityAssetImportGateway(protection),
                new UnityAssetManagerDiagnostics());
        }

        internal static AssetProtectionService
            CreateProtectionService()
        {
            return new AssetProtectionService();
        }

        internal static IAssetArchiveReader CreateArchiveReader()
        {
            return new CachedAssetArchiveReader();
        }

        internal static IAssetFileSystemReader CreateFileSystemReader()
        {
            return new AssetFileSystemReader();
        }
    }
}
