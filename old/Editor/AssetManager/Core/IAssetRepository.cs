using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Core {
    public interface IAssetRepository {
        void Initialize();
        void Load();
        Task<VerificationResult> LoadAndVerifyAsync();
        void ApplyVerificationResult(VerificationResult result);

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
        bool HasImportItems(Ulid assetId);
        string GetImportDirectoryPath(Ulid assetId);
        List<string> GetAssetFiles(Ulid assetId, string searchPattern = "*");
        List<string> GetAllTags();
        event Action LibraryChanged;
        event Action<Ulid> AssetChanged;
        event Action<Ulid> FolderChanged;
    }

    public class VerificationResult {
        public Dictionary<Ulid, AssetMetadata> OnDisk { get; set; } = new();
        public List<Ulid> MissingInCache { get; set; } = new();
        public List<Ulid> MissingOnDisk { get; set; } = new();
        public List<AssetMetadata> Modified { get; set; } = new();
        public string Error { get; set; }

        public bool HasChanges =>
            MissingInCache is { Count: > 0 } ||
            MissingOnDisk is { Count: > 0 } ||
            Modified is { Count: > 0 };
    }
}