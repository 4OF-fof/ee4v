using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Settings;
using SQLite;

namespace Ee4v.AssetManager.Api
{
    public static class AssetManagerApi
    {
        public static event Action Changed;
        public static event Action FileTreeChanged;
        public static event Action<string, IReadOnlyList<AssetFileImportTarget>> FileImportTargetsChanged;
        public static event Action<string, string> VersionGroupPrimaryFileChanged;

        public static AssetSearchResult SearchItems(AssetItemQuery query)
        {
            return Execute(() => AssetManagerDatabase.SearchItems(query));
        }

        public static AssetSearchResult SearchItemSummaries(AssetItemQuery query)
        {
            return Execute(() => AssetManagerDatabase.SearchItemSummaries(query));
        }

        public static AssetItem GetItem(string itemId)
        {
            return Execute(() => AssetManagerDatabase.GetItem(itemId));
        }

        public static AssetThumbnail GetThumbnail(string itemId)
        {
            return Execute(() => AssetManagerDatabase.GetThumbnail(itemId));
        }

        public static IReadOnlyDictionary<string, AssetThumbnail> GetThumbnails(IReadOnlyList<string> itemIds)
        {
            return Execute(() => AssetManagerDatabase.GetThumbnails(itemIds));
        }

        public static AssetItem CreateItem(CreateAssetItemRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.CreateItem(request));
        }

        public static AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.UpdateItem(itemId, request));
        }

        public static IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query = null)
        {
            return Execute(() => AssetManagerDatabase.GetFiles(itemId, query));
        }

        public static AssetFile RegisterFile(string itemId, RegisterFileRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.RegisterFile(itemId, request));
        }

        public static IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId)
        {
            return Execute(() => AssetManagerDatabase.GetVariantGroups(itemId));
        }

        public static AssetVariantGroup CreateVariantGroup(string itemId, CreateVariantGroupRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.CreateVariantGroup(itemId, request));
        }

        public static IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId)
        {
            return Execute(() => AssetManagerDatabase.GetVersionGroups(itemId));
        }

        public static AssetVersionGroup CreateVersionGroup(string itemId, CreateVersionGroupRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.CreateVersionGroup(itemId, request));
        }

        public static void SetVersionGroupPrimaryFile(string versionGroupId, string fileId)
        {
            var primaryFileId = Execute(() => AssetManagerDatabase.SetVersionGroupPrimaryFile(versionGroupId, fileId));
            VersionGroupPrimaryFileChanged?.Invoke(versionGroupId, primaryFileId);
            FileTreeChanged?.Invoke();
        }

        public static void ArchiveFile(string fileId)
        {
            ExecuteChanged(() => AssetManagerDatabase.ArchiveFile(fileId));
        }

        public static AssetFilePathResolution ResolveFilePath(string fileId)
        {
            return Execute(() => AssetManagerDatabase.ResolveFilePath(fileId));
        }

        public static IReadOnlyList<AssetTag> GetTags(string keyword = null)
        {
            return Execute(() => AssetManagerDatabase.GetTags(keyword));
        }

        public static AssetTag CreateTag(string name)
        {
            return ExecuteChanged(() => AssetManagerDatabase.CreateTag(name));
        }

        public static void SetItemTags(string itemId, IReadOnlyList<string> tagIds)
        {
            ExecuteChanged(() => AssetManagerDatabase.SetItemTags(itemId, tagIds));
        }

        public static IReadOnlyList<AssetCollection> GetCollections()
        {
            return Execute(() => AssetManagerDatabase.GetCollections());
        }

        public static AssetCollection CreateCollection(CreateCollectionRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.CreateCollection(request));
        }

        public static AssetCollection CreateSmartCollection(CreateSmartCollectionRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.CreateSmartCollection(request));
        }

        public static void MoveCollection(string collectionId, string parentCollectionId)
        {
            ExecuteChanged(() => AssetManagerDatabase.MoveCollection(collectionId, parentCollectionId));
        }

        public static void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds)
        {
            ExecuteChanged(() => AssetManagerDatabase.SetItemCollections(itemId, collectionIds));
        }

        public static IReadOnlyList<AssetFileDependency> GetFileDependencies(string fileId)
        {
            return Execute(() => AssetManagerDatabase.GetFileDependencies(fileId));
        }

        public static void SetFileDependencies(string dependentFileId, IReadOnlyList<string> dependencyFileIds)
        {
            ExecuteChanged(() => AssetManagerDatabase.SetFileDependencies(dependentFileId, dependencyFileIds));
        }

        public static IReadOnlyList<AssetDependency> GetDependencies(DependencyEndpointRequest source)
        {
            return Execute(() => AssetManagerDatabase.GetDependencies(source));
        }

        public static void SetDependencies(DependencyEndpointRequest source, IReadOnlyList<DependencyEndpointRequest> targets)
        {
            ExecuteChanged(() => AssetManagerDatabase.SetDependencies(source, targets));
        }

        public static IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId)
        {
            return Execute(() => AssetManagerDatabase.GetFileImportTargets(fileId));
        }

        public static void SetFileImportTargets(string fileId, IReadOnlyList<AssetFileImportTargetRequest> targets)
        {
            var updatedTargets = Execute(() =>
            {
                AssetManagerDatabase.SetFileImportTargets(fileId, targets);
                return AssetManagerDatabase.GetFileImportTargets(fileId);
            });
            FileImportTargetsChanged?.Invoke(fileId, updatedTargets);
            FileTreeChanged?.Invoke();
        }

        public static void ImportFileTargets(string itemId, string fileId)
        {
            var relativePaths = GetFileImportTargets(fileId)
                .Select(target => target.RelativePath)
                .ToArray();
            ImportFileEntries(itemId, fileId, relativePaths);
        }

        public static void ImportFileEntry(string itemId, string fileId, string relativePath)
        {
            ImportFileEntries(itemId, fileId, new[] { relativePath });
        }

        public static AssetSyncResult SyncBlm(BlmSyncRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.SyncBlm(request));
        }

        public static AssetSyncResult SyncEagle(EagleSyncRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.SyncEagle(request));
        }

        public static IReadOnlyList<AssetSyncInfo> GetSyncInfo()
        {
            return Execute(() => AssetManagerDatabase.GetSyncInfo());
        }

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

        private static T ExecuteChanged<T>(Func<T> action)
        {
            var result = Execute(action);
            Changed?.Invoke();
            return result;
        }

        private static void ExecuteChanged(Action action)
        {
            ExecuteChanged(() =>
            {
                action();
                return true;
            });
        }

        private static void Execute(Action action)
        {
            Execute(() =>
            {
                action();
                return true;
            });
        }

        internal static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static void ImportFileEntries(string itemId, string fileId, IReadOnlyList<string> relativePaths)
        {
            if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(fileId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Item id and file id are required.");
            }

            var item = GetItem(itemId);
            var file = GetFiles(itemId, new AssetFileQuery
                {
                    Lifecycle = AssetFileLifecycle.Active
                })
                .FirstOrDefault(candidate => string.Equals(candidate.Id, fileId, StringComparison.Ordinal));
            if (file == null)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "The file was not found in the item.");
            }

            var resolution = ResolveFilePath(fileId);
            if (resolution == null || !resolution.Found || string.IsNullOrWhiteSpace(resolution.Path))
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "The file path could not be resolved.");
            }

            AssetFileImportService.Import(
                item.Name,
                file.FileName,
                resolution.Path,
                relativePaths,
                new UnityAssetFileImportEnvironment(),
                SettingApi.Get(AssetManagerDefinitions.ShowUnityPackageImportDialog));
        }
    }
}
