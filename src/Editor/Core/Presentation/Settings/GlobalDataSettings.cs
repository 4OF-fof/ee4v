using System;
using System.IO;
using UnityEditor;

namespace Ee4v.Core.Settings
{
    [InitializeOnLoad]
    public static class GlobalDataSettings
    {
        internal static readonly SettingDefinition<string> RootPath =
            new SettingDefinition<string>(
                "assetManager.ee4vGlobalPath",
                SettingScope.User,
                "AssetManager",
                "settings.section.assetManager.paths",
                "settings.ee4vGlobalPath.label",
                "settings.ee4vGlobalPath.tooltip",
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments),
                    "ee4v"),
                order: 2,
                keywords: new[]
                {
                    "asset manager",
                    "ee4v",
                    "library",
                    "path"
                });

        private static ISettingsService _registeredSettings;

        static GlobalDataSettings()
        {
            EnsureRegistered();
        }

        public static event Action PathChanged;

        public static string RootDirectory
        {
            get
            {
                var value = EnsureRegistered().Get(RootPath);
                return System.IO.Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(value));
            }
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

            settings.Register(RootPath);
            settings.Changed += OnSettingChanged;
            _registeredSettings = settings;
            return settings;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(args.Definition, RootPath))
            {
                PathChanged?.Invoke();
            }
        }
    }
}
