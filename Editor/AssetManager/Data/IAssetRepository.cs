using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public interface IAssetRepository {
        void Initialize();
        void Load();
        Task LoadAndVerifyAsync();
        AssetMetadata GetAsset(Ulid assetId);
        IEnumerable<AssetMetadata> GetAllAssets();
        LibraryMetadata GetLibraryMetadata();
        void CreateAssetFromFile(string sourcePath);
        void AddFileToAsset(Ulid assetId, string sourcePath);
        bool HasAssetFile(Ulid assetId);
        AssetMetadata CreateEmptyAsset();
        void SaveAsset(AssetMetadata asset);
        void SaveAssets(IEnumerable<AssetMetadata> assets);
        void RenameAssetFile(Ulid assetId, string newName);
        void DeleteAsset(Ulid assetId);
        void SaveLibraryMetadata(LibraryMetadata libraryMetadata);
        void SaveFolder(Ulid folderId, bool structureChanged = false);
        void SetThumbnail(Ulid assetId, string imagePath);
        void RemoveThumbnail(Ulid assetId);
        string GetThumbnailPath(Ulid assetId);
        Task<byte[]> GetThumbnailDataAsync(Ulid assetId);
        void SetFolderThumbnail(Ulid folderId, string imagePath);
        void RemoveFolderThumbnail(Ulid folderId);
        string GetFolderThumbnailPath(Ulid folderId);
        Task<byte[]> GetFolderThumbnailDataAsync(Ulid folderId);
        void ImportFiles(Ulid assetId, string sourceRootPath, List<string> relativePaths);
        List<string> GetAssetFiles(Ulid assetId, string searchPattern = "*");
        List<string> GetAllTags();
        event Action LibraryChanged;
        event Action<Ulid> AssetChanged;
        event Action<Ulid> FolderChanged;
    }
}