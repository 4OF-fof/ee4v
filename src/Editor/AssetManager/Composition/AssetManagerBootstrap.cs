using System;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.Application;
using Ee4v.AssetManager.Infrastructure.Files;
using Ee4v.AssetManager.Infrastructure.Unity;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using Ee4v.Core.I18n;
using UnityEditor;

namespace Ee4v.AssetManager.Composition
{
    [InitializeOnLoad]
    public static class AssetManagerBootstrap
    {
        private static AssetManagerService _assetManager;
        private static AssetManagerUiPreferencesAdapter _uiPreferences;
        private static global::Ee4v.AssetManager.UI
            .AssetManagerProjectDecorationPresenter
            _projectDecoration;
        private static IDisposable _projectRegistration;
        private static global::Ee4v.AssetManager.Infrastructure.Unity
            .AssetProtectionService _assetProtection;
        private static AssetItemContextActionRegistry
            _itemContextActions;
        private static global::Ee4v.AssetManager.UI
            .AssetManagerProtectedInspectorPresenter
            _protectedInspector;

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

        public static void GetAvatarModifyDependencies(
            out global::Ee4v.AssetManager.Contracts.IAssetManager assetManager,
            out global::Ee4v.AssetManager.Contracts.IAssetManagerAssetDerivationService derivationService,
            out global::Ee4v.AssetManager.Contracts.IAssetItemContextActionRegistry itemContextActions)
        {
            EnsureInitialized();
            assetManager = _assetManager;
            derivationService = _assetProtection;
            itemContextActions = _itemContextActions;
        }

        private static void InitializeModule(ISettingsService settings)
        {
            AssetManagerInfrastructureSettings.Configure(
                new AssetManagerInfrastructureSettingsAdapter(settings));
            _assetProtection = new AssetProtectionService();
            _itemContextActions =
                new AssetItemContextActionRegistry();
            _assetManager = new AssetManagerService(
                new SqliteAssetManagerStore(),
                new UnityAssetImportGateway(
                    new UnityAssetFileImportEnvironment(),
                    _assetProtection),
                new UnityAssetManagerDiagnostics());
            _assetProtection.Initialize(_assetManager);
            global::Ee4v.AssetManager.Infrastructure.Unity
                .AssetProtectionEditorBridge.Configure(
                    _assetProtection,
                    I18N.Get(
                        "assetManager.protection.blocked"));
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
            _protectedInspector =
                new global::Ee4v.AssetManager.UI
                    .AssetManagerProtectedInspectorPresenter(
                        _assetProtection);
            global::Ee4v.AssetManager.UI.AssetManagerUiDependencies.Configure(
                _assetManager,
                _uiPreferences,
                new CachedAssetArchiveReader(),
                new AssetFileSystemReader(),
                uiScheduler,
                new UnityAssetManagerFilePicker(),
                new global::Ee4v.AssetManager.UI.StandaloneAssetManagerViewSession(),
                _projectDecoration,
                _assetProtection,
                _itemContextActions,
                AssetManagerStartupSync.RequestManualSync);
            _projectRegistration = InjectorApi.Register(
                new ItemInjectionRegistration(
                    "asset-manager.project-decoration",
                    InjectionChannel.ProjectItem,
                    _projectDecoration.Draw,
                    priority: -100));
            settings.Changed += OnSettingChanged;
            _projectDecoration.Initialize();
            _protectedInspector.Initialize();
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
