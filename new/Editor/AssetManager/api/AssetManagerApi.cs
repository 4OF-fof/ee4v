using System;
using System.Collections.Generic;
using SQLite;

namespace Ee4v.AssetManager.Api
{
    public static class AssetManagerApi
    {
        public static event Action Changed;

        public static AssetSearchResult SearchItems(AssetItemQuery query)
        {
            return Execute(() => AssetManagerDatabase.SearchItems(query));
        }

        public static AssetItem GetItem(string itemId)
        {
            return Execute(() => AssetManagerDatabase.GetItem(itemId));
        }

        public static AssetThumbnail GetThumbnail(string itemId)
        {
            return Execute(() => AssetManagerDatabase.GetThumbnail(itemId));
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

        public static AssetFile GetPrimaryFile(string itemId)
        {
            return Execute(() => AssetManagerDatabase.GetPrimaryFile(itemId));
        }

        public static AssetFile RegisterFile(string itemId, RegisterFileRequest request)
        {
            return ExecuteChanged(() => AssetManagerDatabase.RegisterFile(itemId, request));
        }

        public static void SetPrimaryFile(string itemId, string fileId)
        {
            ExecuteChanged(() => AssetManagerDatabase.SetPrimaryFile(itemId, fileId));
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
    }
}
