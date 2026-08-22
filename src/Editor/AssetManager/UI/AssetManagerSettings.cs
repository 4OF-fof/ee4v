using System;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    [InitializeOnLoad]
    internal static class AssetManagerSettings
    {
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

        private static readonly SettingDefinition<bool> AutoSyncEagle =
            new SettingDefinition<bool>(
                "assetManager.autoSyncEagleOnStartup",
                SettingScope.User,
                "AssetManager",
                "settings.section.assetManager.source",
                "settings.autoSyncEagleOnStartup.label",
                "settings.autoSyncEagleOnStartup.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "asset manager",
                    "eagle",
                    "sync",
                    "startup"
                });

        private static readonly SettingDefinition<bool> AutoSyncEe4v =
            new SettingDefinition<bool>(
                "assetManager.autoSyncEe4vOnStartup",
                SettingScope.User,
                "AssetManager",
                "settings.section.assetManager.source",
                "settings.autoSyncEe4vOnStartup.label",
                "settings.autoSyncEe4vOnStartup.tooltip",
                true,
                order: 1,
                keywords: new[]
                {
                    "asset manager",
                    "ee4v",
                    "sync",
                    "startup"
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

        private static ISettingsService _registeredSettings;

        internal static event Action ProjectThumbnailImportChanged;

        static AssetManagerSettings()
        {
            EnsureRegistered();
        }

        internal static string EagleLibraryPath =>
            Get(EagleLibrary);

        internal static string EagleTargetRoot =>
            Get(EagleTarget);

        internal static string Ee4vLibraryPath =>
            GlobalDataSettings.RootDirectory;

        internal static bool AutoSyncEagleOnStartup =>
            Get(AutoSyncEagle);

        internal static bool AutoSyncEe4vOnStartup =>
            Get(AutoSyncEe4v);

        internal static bool ApplyProjectThumbnailOnImportEnabled =>
            Get(ApplyProjectThumbnailOnImport);

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
            settings.Register(AutoSyncEagle);
            settings.Register(AutoSyncEe4v);
            settings.Register(ApplyProjectThumbnailOnImport);
            settings.Changed += OnSettingChanged;
            _registeredSettings = settings;
            return settings;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    ApplyProjectThumbnailOnImport))
            {
                ProjectThumbnailImportChanged?.Invoke();
            }
        }
    }
}
