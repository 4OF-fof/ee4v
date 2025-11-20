using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Data;

namespace _4OF.ee4v.AssetManager.Data {
    public static class AssetManagerContainer {
        static AssetManagerContainer() {
            Initialize();
        }

        public static IAssetRepository Repository { get; private set; }
        public static AssetService AssetService { get; private set; }
        public static FolderService FolderService { get; private set; }
        public static TextureService TextureService { get; private set; }

        private static void Initialize() {
            Repository = new JsonAssetRepository(EditorPrefsManager.ContentFolderPath);

            Repository.Initialize();
            Repository.Load();

            AssetService = new AssetService(Repository);
            FolderService = new FolderService(Repository, AssetService);
            TextureService = new TextureService(Repository);
        }

        public static void SetDependenciesForTest(IAssetRepository repository, AssetService assetService,
            FolderService folderService, TextureService textureService) {
            Repository = repository;
            AssetService = assetService;
            FolderService = folderService;
            TextureService = textureService;
        }

        public static void Reload() {
            Initialize();
        }
    }
}