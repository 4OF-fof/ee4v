using System.Threading.Tasks;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.OldData;

namespace _4OF.ee4v.AssetManager.Service {
    internal static class AssetLoadingService {
        public static async Task LoadAssetLibraryAsync() {
            var cacheLoaded = AssetLibrarySerializer.LoadCache();

            if (!cacheLoaded) {
                AssetLibrarySerializer.LoadLibrary();
                AssetLibrarySerializer.LoadAllAssets();
                AssetLibrarySerializer.SaveCache();
            }
            else {
                AssetLibraryService.NotifyAssetLibraryLoaded();
                await AssetLibrarySerializer.LoadAndVerifyAsync();
            }

            AssetLibraryService.NotifyAssetLibraryLoaded();
        }

        public static void RefreshAssetLibrary() {
            AssetLibrary.Instance.UnloadAssetLibrary();
            _ = LoadAssetLibraryAsync();
        }
    }
}