using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.BoothLibraryManager;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    [InitializeOnLoad]
    internal static class AssetManagerSettings
    {
        private static readonly SettingDefinition<AssetDatasourceKind> Datasource =
            new SettingDefinition<AssetDatasourceKind>("assetManager.datasource", SettingScope.User,
                "AssetManager", "settings.section.assetManager.source", "settings.datasource.label",
                "settings.datasource.tooltip", AssetDatasourceKind.Eagle, order: -1);
        private static readonly SettingDefinition<string> FolderLibrary =
            new SettingDefinition<string>("assetManager.folderLibraryPath", SettingScope.User,
                "AssetManager", "settings.section.assetManager.paths", "settings.folderLibrary.label",
                "settings.folderLibrary.tooltip", string.Empty, order: 3);
        private static readonly SettingDefinition<string> BlmDatabase =
            new SettingDefinition<string>("assetManager.blmDatabasePath", SettingScope.User,
                "AssetManager", "settings.section.assetManager.paths", "settings.blmDatabase.label",
                "settings.blmDatabase.tooltip", BoothLibraryManagerApi.GetDefaultDatabasePath(), order: 4);
        private static readonly SettingDefinition<bool> AutoSyncDatasource =
            new SettingDefinition<bool>("assetManager.autoSyncDatasourceOnStartup", SettingScope.User,
                "AssetManager", "settings.section.assetManager.source", "settings.autoSyncDatasource.label",
                "settings.autoSyncDatasource.tooltip", true, order: 0);

        internal static event Action DatasourceChanged;
        internal static AssetDatasourceKind SelectedDatasource => Get(Datasource);
        internal static AssetDatasourceRequest DatasourceRequest => new AssetDatasourceRequest
        {
            Kind = SelectedDatasource,
            LibraryPath = SelectedDatasource == AssetDatasourceKind.Eagle ? EagleLibraryPath
                : SelectedDatasource == AssetDatasourceKind.Ee4v ? Ee4vLibraryPath : Get(FolderLibrary),
            DatabasePath = Get(BlmDatabase), TargetRoot = EagleTargetRoot
        };
        internal static bool AutoSyncDatasourceOnStartup => Get(AutoSyncDatasource);
        private static readonly SettingDefinition<string> EagleLibrary =
            new SettingDefinition<string>(
                "assetManager.eagleLibraryPath",
                SettingScope.User,
                "AssetManager",
                "settings.section.assetManager.paths",
                "settings.eagleLibraryPath.label",
                "settings.eagleLibraryPath.tooltip",
                string.Empty,
                order: 0,
                keywords: new[]
                {
                    "asset manager",
                    "eagle",
                    "library",
                    "path"
                });

        private static readonly SettingDefinition<string> EagleTarget =
            new SettingDefinition<string>(
                "assetManager.eagleTargetRoot",
                SettingScope.User,
                "AssetManager",
                "settings.section.assetManager.paths",
                "settings.eagleTargetRoot.label",
                "settings.eagleTargetRoot.tooltip",
                "VRCAsset",
                order: 1,
                keywords: new[]
                {
                    "asset manager",
                    "eagle",
                    "target",
                    "root"
                });

        private static readonly SettingDefinition<bool>
            ApplyProjectThumbnailOnImport =
                new SettingDefinition<bool>(
                    "assetManager.applyProjectThumbnailOnImport",
                    SettingScope.User,
                    "AssetManager",
                    "settings.section.assetManager.view",
                    "settings.applyProjectThumbnailOnImport.label",
                    "settings.applyProjectThumbnailOnImport.tooltip",
                    true,
                    order: 0,
                    keywords: new[]
                    {
                        "asset manager",
                        "project",
                        "thumbnail",
                        "icon"
                    });

        private static readonly SettingDefinition<string>
            ExcludedPartNamePrefixes =
                new SettingDefinition<string>(
                    "assetManager.excludedPartNamePrefixes",
                    SettingScope.User,
                    "AssetManager",
                    "settings.section.assetManager.view",
                    "settings.excludedPartNamePrefixes.label",
                    "settings.excludedPartNamePrefixes.tooltip",
                    "Armature",
                    order: 1,
                    keywords: new[]
                    {
                        "asset manager",
                        "parts",
                        "exclude",
                        "armature"
                    });

        private static ISettingsService _registeredSettings;

        internal static event Action ProjectThumbnailImportChanged;
        internal static event Action PartListExclusionsChanged;

        static AssetManagerSettings()
        {
            PathSettingDrawer.Register(EagleLibrary);
            PathSettingDrawer.Register(FolderLibrary);
            PathSettingDrawer.Register(BlmDatabase, PathFieldKind.File, "db");
            CommaSeparatedListSettingDrawer.Register(
                ExcludedPartNamePrefixes);
            EnsureRegistered();
        }

        internal static string EagleLibraryPath =>
            Get(EagleLibrary);

        internal static string EagleTargetRoot =>
            Get(EagleTarget);

        internal static string Ee4vLibraryPath =>
            GlobalDataSettings.RootDirectory;

        internal static bool ApplyProjectThumbnailOnImportEnabled =>
            Get(ApplyProjectThumbnailOnImport);

        internal static IReadOnlyList<string> ExcludedPartPrefixes =>
            CommaSeparatedListSettingDrawer.ParseItems(
                Get(ExcludedPartNamePrefixes));

        private static T Get<T>(SettingDefinition<T> definition)
        {
            var settings = EnsureRegistered();
            return settings.Get(definition);
        }

        private static ISettingsService EnsureRegistered()
        {
            var settings = CoreSettings.Current;
            if (ReferenceEquals(_registeredSettings, settings))
            {
                return settings;
            }

            if (_registeredSettings != null)
            {
                _registeredSettings.Changed -= OnSettingChanged;
            }

            settings.Register(EagleLibrary);
            settings.Register(EagleTarget);
            settings.Register(Datasource);
            settings.Register(FolderLibrary);
            settings.Register(BlmDatabase);
            settings.Register(AutoSyncDatasource);
            settings.Register(ApplyProjectThumbnailOnImport);
            settings.Register(ExcludedPartNamePrefixes);
            settings.Changed += OnSettingChanged;
            _registeredSettings = settings;
            return settings;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(args.Definition, Datasource) ||
                ReferenceEquals(args.Definition, FolderLibrary) || ReferenceEquals(args.Definition, BlmDatabase))
            {
                DatasourceChanged?.Invoke();
            }
            if (ReferenceEquals(
                    args.Definition,
                    ApplyProjectThumbnailOnImport))
            {
                ProjectThumbnailImportChanged?.Invoke();
            }
            if (ReferenceEquals(
                    args.Definition,
                    ExcludedPartNamePrefixes))
            {
                PartListExclusionsChanged?.Invoke();
            }
        }
    }
}
