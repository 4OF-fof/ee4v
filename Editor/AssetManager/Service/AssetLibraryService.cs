using _4OF.ee4v.AssetManager.Data;

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
    }
}