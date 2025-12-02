using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.Setting;

namespace _4OF.ee4v.AssetManager {
    public static class AssetManagerContainer {
        static AssetManagerContainer() {
            Initialize();
        }

        public static IAssetRepository Repository { get; private set; }
        public static AssetService AssetService { get; private set; }
        public static FolderService FolderService { get; private set; }
        public static TextureService TextureService { get; private set; }

        private static void Initialize() {
            Repository = new AssetRepository(SettingSingleton.I.contentFolderPath);

            Repository.Initialize();
            Repository.Load();

            FolderService = new FolderService(Repository);
            AssetService = new AssetService(Repository, FolderService);
            TextureService = new TextureService(Repository);
        }
    }
}