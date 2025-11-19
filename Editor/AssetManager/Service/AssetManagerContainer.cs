using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Data;

namespace _4OF.ee4v.AssetManager {
    public static class AssetManagerContainer {
        public static IAssetRepository Repository { get; private set; }
        public static AssetService AssetService { get; private set; }
        public static FolderService FolderService { get; private set; }

        static AssetManagerContainer() {
            Initialize();
        }

        private static void Initialize() {
            Repository = new JsonAssetRepository(EditorPrefsManager.ContentFolderPath);
            
            Repository.Initialize();
            Repository.Load();

            AssetService = new AssetService(Repository);
            FolderService = new FolderService(Repository, AssetService);
        }

        public static void SetDependenciesForTest(IAssetRepository repository, AssetService assetService, FolderService folderService) {
            Repository = repository;
            AssetService = assetService;
            FolderService = folderService;
        }
        
        public static void Reload() {
            Initialize();
        }
    }
}