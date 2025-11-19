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

        AssetMetadata CreateEmptyAsset();

        void SaveAsset(AssetMetadata asset);

        void RenameAssetFile(Ulid assetId, string newName);

        void DeleteAsset(Ulid assetId);

        void SaveLibraryMetadata(LibraryMetadata libraryMetadata);

        void SetThumbnail(Ulid assetId, string imagePath);

        void RemoveThumbnail(Ulid assetId);
        
        string GetThumbnailPath(Ulid assetId);
    }
}