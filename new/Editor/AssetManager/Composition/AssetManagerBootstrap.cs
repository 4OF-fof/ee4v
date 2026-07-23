using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.Application;
using Ee4v.Core.Internal;
using UnityEditor;

namespace Ee4v.AssetManager.Composition
{
    [InitializeOnLoad]
    internal static class AssetManagerBootstrap
    {
        private static AssetManagerService _assetManager;
        private static AssetManagerUiPreferencesAdapter _uiPreferences;

        static AssetManagerBootstrap()
        {
            EnsureInitialized();
        }

        internal static void EnsureInitialized()
        {
            if (_assetManager != null)
            {
                return;
            }

            FeatureBootstrapContract.Initialize(
                "AssetManager",
                AssetManagerDefinitions.RegisterAll,
                InitializeModule);
        }

        private static void InitializeModule()
        {
            AssetManagerInfrastructure.ConfigureSettings(
                new AssetManagerInfrastructureSettingsAdapter());
            _assetManager = AssetManagerInfrastructure.CreateDefaultService();
            _uiPreferences = new AssetManagerUiPreferencesAdapter();
            global::Ee4v.AssetManager.AssetManagerUiDependencies.Configure(
                _assetManager,
                _uiPreferences,
                AssetManagerInfrastructure.CreateArchiveReader(),
                new AssetManagerUiSchedulerAdapter());
            AssetManagerStartupSyncConflictPresenter.Initialize(_assetManager);
            AssetManagerDebugSyncMenu.Initialize(_assetManager);
            AssetManagerStartupSync.EnsureInitialized(_assetManager);
        }
    }
}
