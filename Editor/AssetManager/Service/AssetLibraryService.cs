using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Service {
    public static class AssetLibraryService {
        public static async void LoadAssetLibrary() {
            await AssetLoadingService.LoadAssetLibraryAsync();
        }

        public static void RefreshAssetLibrary() {
            AssetLoadingService.RefreshAssetLibrary();
        }

        public static void CreateAsset(string path) {
            AssetService.CreateAsset(path);
        }

        public static void DeleteAsset(Ulid assetId) {
            AssetService.DeleteAsset(assetId);
        }

        public static void UpdateAsset(AssetMetadata newAsset) {
            AssetService.UpdateAsset(newAsset);
        }

        public static void SetAssetName(Ulid assetId, string newName) {
            AssetService.SetAssetName(assetId, newName);
        }

        public static void SetDescription(Ulid assetId, string newDescription) {
            AssetService.SetDescription(assetId, newDescription);
        }

        public static void SetBoothShopDomain(Ulid assetId, string shopURL) {
            AssetBoothService.SetBoothShopDomain(assetId, shopURL);
        }

        public static void SetBoothItemId(Ulid assetId, string itemURL) {
            AssetBoothService.SetBoothItemId(assetId, itemURL);
        }

        public static void SetBoothDownloadId(Ulid assetId, string downloadURL) {
            AssetBoothService.SetBoothDownloadId(assetId, downloadURL);
        }

        public static void SetFolder(Ulid assetId, Ulid newFolder) {
            AssetService.SetFolder(assetId, newFolder);
        }

        public static void AddTag(Ulid assetId, string tag) {
            AssetService.AddTag(assetId, tag);
        }

        public static void RemoveTag(Ulid assetId, string tag) {
            AssetService.RemoveTag(assetId, tag);
        }

        public static void RenameTag(string oldTag, string newTag) {
            TagService.RenameTag(oldTag, newTag);
        }

        public static void RemoveAsset(Ulid assetId) {
            AssetService.RemoveAsset(assetId);
        }

        public static void RestoreAsset(Ulid assetId) {
            AssetService.RestoreAsset(assetId);
        }

        public static void CreateFolder(Ulid parentFolderId, string name, string description = null) {
            FolderService.CreateFolder(parentFolderId, name, description);
        }

        public static void MoveFolder(Ulid folderId, Ulid parentFolderId) {
            if (folderId == default) return;
            FolderService.MoveFolder(folderId, parentFolderId);
        }

        public static void UpdateFolder(Folder newFolder) {
            FolderService.UpdateFolder(newFolder);
        }

        public static void UpdateBoothItemFolder(BoothItemFolder newFolder) {
            FolderService.UpdateBoothItemFolder(newFolder);
        }

        public static void SetFolderName(Ulid folderId, string newName) {
            FolderService.SetFolderName(folderId, newName);
        }

        public static void SetFolderDescription(Ulid folderId, string description) {
            FolderService.SetFolderDescription(folderId, description);
        }

        public static void DeleteFolder(Ulid folderId) {
            FolderService.DeleteFolder(folderId);
        }

        // Note: Internal helper methods were moved to specialized services (FolderService, AssetValidationService).
    }
}