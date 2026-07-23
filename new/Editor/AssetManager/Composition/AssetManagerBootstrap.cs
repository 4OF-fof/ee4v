using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.Application;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
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

            var settings = CoreSettings.Current;
            FeatureBootstrapContract.Initialize(
                "AssetManager",
                typeof(AssetManagerDefinitions),
                () => AssetManagerDefinitions.RegisterAll(settings),
                () => InitializeModule(settings));
        }

        private static void InitializeModule(ISettingsService settings)
        {
            AssetManagerInfrastructure.ConfigureSettings(
                new AssetManagerInfrastructureSettingsAdapter(settings));
            _assetManager = AssetManagerInfrastructure.CreateDefaultService();
            _uiPreferences = new AssetManagerUiPreferencesAdapter(settings);
            global::Ee4v.AssetManager.UI.AssetManagerUiDependencies.Configure(
                _assetManager,
                _uiPreferences,
                AssetManagerInfrastructure.CreateArchiveReader(),
                new AssetManagerUiSchedulerAdapter());
            AssetManagerStartupSyncConflictPresenter.Initialize(_assetManager);
            AssetManagerDebugSyncMenu.Initialize(_assetManager);
            AssetManagerStartupSync.EnsureInitialized(_assetManager, settings);
        }
    }
}
