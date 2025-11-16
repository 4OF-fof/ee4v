using System.Linq;
using System.Runtime.CompilerServices;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    public static class AssetLibraryService {
        public static void LoadAssetLibrary() {
            AssetLibrarySerializer.LoadLibrary();
            AssetLibrarySerializer.LoadAllAssets();
        }

        public static void RefreshAssetLibrary() {
            AssetLibrary.Instance.UnloadAssetLibrary();
            LoadAssetLibrary();
        }

        public static void UpdateAsset(AssetMetadata newAsset) {
            if (AssetLibrary.Instance.GetAsset(newAsset.ID) == null) {
                Debug.LogError($"Asset with ID {newAsset.ID} does not exist.");
                return;
            }
            var oldAsset = AssetLibrary.Instance.GetAsset(newAsset.ID);
            if (oldAsset.Name != newAsset.Name) {
                AssetLibrarySerializer.RenameAsset(newAsset.ID, newAsset.Name);
            }
            AssetLibrary.Instance.UpdateAsset(newAsset);
            AssetLibrarySerializer.SaveAsset(newAsset);
        }
        
        public static void RenameAsset(Ulid assetId, string newName) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetName(newName);
            UpdateAsset(asset);
        }
        
        public static void SetDescription(Ulid assetId, string newDescription) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetDescription(newDescription);
            UpdateAsset(asset);
        }
        
        public static void SetFolder(Ulid assetId, Ulid? newFolder) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetFolder(newFolder);
            UpdateAsset(asset);
        }
        
        public static void AddTag(Ulid assetId, string tag) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.AddTag(tag);
            UpdateAsset(asset);
        }
        
        public static void RemoveTag(Ulid assetId, string tag) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.RemoveTag(tag);
            UpdateAsset(asset);
        }
        
        public static void DeleteAsset(Ulid assetId) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetDeleted(true);
            UpdateAsset(asset);
        }
        
        public static void RestoreAsset(Ulid assetId) {
            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            asset.SetDeleted(false);
            UpdateAsset(asset);
        }
    }
}