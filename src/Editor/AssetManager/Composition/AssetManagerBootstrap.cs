using System;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.Application;
using Ee4v.Core.Injector;
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
        private static global::Ee4v.AssetManager.UI
            .AssetManagerProjectDecorationPresenter
            _projectDecoration;
        private static IDisposable _projectRegistration;

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
                () =>
                {
                    AssetManagerDefinitions.RegisterAll(settings);
                    global::Ee4v.AssetManager.UI
                        .SourcePrioritySettingDrawer.Register(
                        AssetManagerDefinitions.SourcePriority);
                    CommaSeparatedListSettingDrawer.Register(
                        AssetManagerDefinitions.AvatarNames);
                },
                () => InitializeModule(settings));
        }

        private static void InitializeModule(ISettingsService settings)
        {
            AssetManagerInfrastructure.ConfigureSettings(
                new AssetManagerInfrastructureSettingsAdapter(settings));
            _assetManager = AssetManagerInfrastructure.CreateDefaultService();
            _uiPreferences = new AssetManagerUiPreferencesAdapter(settings);
            var uiScheduler =
                new AssetManagerUiSchedulerAdapter();
            _projectDecoration =
                new global::Ee4v.AssetManager.UI
                    .AssetManagerProjectDecorationPresenter(
                        _assetManager,
                        uiScheduler)
                {
                    ShowIcons = settings.Get(
                        AssetManagerDefinitions
                            .ShowProjectWindowIcons)
                };
            _projectDecoration.DecorationChanged +=
                RepaintProjectWindow;
            global::Ee4v.AssetManager.UI.AssetManagerUiDependencies.Configure(
                _assetManager,
                _uiPreferences,
                AssetManagerInfrastructure.CreateArchiveReader(),
                AssetManagerInfrastructure.CreateFileSystemReader(),
                uiScheduler,
                new global::Ee4v.AssetManager.UI.StandaloneAssetManagerViewSession(),
                _projectDecoration,
                AssetManagerStartupSync.RequestManualSync);
            _projectRegistration = InjectorApi.Register(
                new ItemInjectionRegistration(
                    "asset-manager.project-decoration",
                    InjectionChannel.ProjectItem,
                    _projectDecoration.Draw,
                    priority: -100));
            settings.Changed += OnSettingChanged;
            _projectDecoration.Initialize();
            AssetManagerStartupSyncConflictPresenter.Initialize(_assetManager);
            AssetManagerStartupSync.EnsureInitialized(_assetManager, settings);
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (_projectDecoration == null ||
                !ReferenceEquals(
                    args.Definition,
                    AssetManagerDefinitions
                        .ShowProjectWindowIcons))
            {
                return;
            }

            _projectDecoration.ShowIcons =
                CoreSettings.Current.Get(
                    AssetManagerDefinitions
                        .ShowProjectWindowIcons);
            RepaintProjectWindow();
        }

        private static void RepaintProjectWindow()
        {
            InjectorApi.Repaint(
                InjectionChannel.ProjectItem);
        }
    }
}
