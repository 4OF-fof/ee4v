using System;
using System.IO;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.AssetManager.Api
{
    [InitializeOnLoad]
    internal static class AssetManagerDefinitions
    {
        private const string BoothLibraryRelativePath = "pm.booth.library-manager\\data.db";

        private static bool _registered;

        static AssetManagerDefinitions()
        {
            RegisterAll();
        }

        public static readonly SettingDefinition<string> Ee4vGlobalPath = new SettingDefinition<string>(
            "assetManager.ee4vGlobalPath",
            SettingScope.User,
            "settings.section.assetManager.paths",
            "settings.ee4vGlobalPath.label",
            "settings.ee4vGlobalPath.tooltip",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ee4v"),
            order: 0,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<string> BlmDatabasePath = new SettingDefinition<string>(
            "assetManager.blmDatabasePath",
            SettingScope.User,
            "settings.section.assetManager.paths",
            "settings.blmDatabasePath.label",
            "settings.blmDatabasePath.tooltip",
            GetDefaultBlmDatabasePath(),
            order: 1,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<string> EagleLibraryPath = new SettingDefinition<string>(
            "assetManager.eagleLibraryPath",
            SettingScope.User,
            "settings.section.assetManager.paths",
            "settings.eagleLibraryPath.label",
            "settings.eagleLibraryPath.tooltip",
            string.Empty,
            order: 2);

        public static readonly SettingDefinition<string> SourcePriority = new SettingDefinition<string>(
            "assetManager.sourcePriority",
            SettingScope.User,
            "settings.section.assetManager.source",
            "settings.sourcePriority.label",
            "settings.sourcePriority.tooltip",
            "ee4v,eagle,blm",
            order: 0,
            validator: ValidateSourcePriority);

        public static void RegisterAll()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            SettingApi.Register(Ee4vGlobalPath);
            SettingApi.Register(BlmDatabasePath);
            SettingApi.Register(EagleLibraryPath);
            SettingApi.Register(SourcePriority);
        }

        private static SettingValidationResult ValidateNonEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? SettingValidationResult.Error(I18N.Get("settings.validation.required"))
                : SettingValidationResult.Success;
        }

        private static string GetDefaultBlmDatabasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                BoothLibraryRelativePath);
        }

        private static SettingValidationResult ValidateSourcePriority(string value)
        {
            var priorities = SourcePriorityUtility.Parse(value);
            return priorities.Count == 3
                ? SettingValidationResult.Success
                : SettingValidationResult.Error(I18N.Get("settings.validation.sourcePriority"));
        }
    }
}
