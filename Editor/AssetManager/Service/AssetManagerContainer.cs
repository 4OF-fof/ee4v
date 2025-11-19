using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Data;

namespace _4OF.ee4v.AssetManager {
    /// <summary>
    /// アプリケーションの依存関係を解決し、シングルトンインスタンスを保持するコンテナ。
    /// </summary>
    public static class AssetManagerContainer {
        public static IAssetRepository Repository { get; private set; }
        public static AssetService AssetService { get; private set; }
        public static FolderService FolderService { get; private set; }

        static AssetManagerContainer() {
            Initialize();
        }

        private static void Initialize() {
            // 1. リポジトリの生成 (パスは設定から取得)
            Repository = new JsonAssetRepository(EditorPrefsManager.ContentFolderPath);
            
            // 2. リポジトリの初期化とロード
            Repository.Initialize();
            Repository.Load();

            // 3. サービスの生成 (DI)
            AssetService = new AssetService(Repository);
            FolderService = new FolderService(Repository, AssetService);
        }

        /// <summary>
        /// テスト用にリポジトリとサービスを差し替えます
        /// </summary>
        public static void SetDependenciesForTest(IAssetRepository repository, AssetService assetService, FolderService folderService) {
            Repository = repository;
            AssetService = assetService;
            FolderService = folderService;
        }
        
        /// <summary>
        /// 設定変更時などにリポジトリを作り直す場合に使用
        /// </summary>
        public static void Reload() {
            Initialize();
        }
    }
}