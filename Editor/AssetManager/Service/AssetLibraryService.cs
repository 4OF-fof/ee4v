using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Service {
    public static class AssetLibraryService {
        public static void LoadAssetLibrary() {
            AssetLibrarySerializer.LoadLibrary();
            AssetLibrarySerializer.LoadAllAssets();
        }

        public static void RefreshAssetLibrary() {
            AssetLibrary.Instance.UnloadLibrary();
            LoadAssetLibrary();
        }

        public static void UpdateAsset(AssetMetadata asset) {
            var oldAsset = AssetLibrary.Instance.GetAsset(asset.ID);
            if (oldAsset.Name != asset.Name) {
                AssetLibrarySerializer.RenameAsset(asset.ID, asset.Name);
                asset.UpdateName(asset.Name);
            }
            AssetLibrary.Instance.UpdateAsset(asset);
            AssetLibrarySerializer.SaveAsset(asset);
            AssetLibrarySerializer.LoadAsset(asset.ID);
        }
        
        public static void RenameAsset(Ulid assetId, string newName) {
            var oldAsset = AssetLibrary.Instance.GetAsset(assetId);
            var newAsset = new AssetMetadata(
                oldAsset.ID,
                newName,
                oldAsset.Description,
                oldAsset.Size,
                oldAsset.Ext,
                oldAsset.Folder,
                oldAsset.Tags.ToList(),
                oldAsset.IsDeleted,
                oldAsset.ModificationTime
            );
            UpdateAsset(newAsset);
        }
    }
}