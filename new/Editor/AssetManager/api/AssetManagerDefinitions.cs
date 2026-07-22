using System;
using System.IO;
using System.Text.RegularExpressions;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.AssetManager.Api
{
    [InitializeOnLoad]
    internal static class AssetManagerDefinitions
    {
        private const string BoothLibraryRelativePath = "pm.booth.library-manager\\data.db";
        private const string DefaultAvatarNames = "Airi,Alue,Bokusei,Chiffon,Chocolat,Eku,ELusion,Grus,Hanka,Ichigo,Kaguya,Kanata,Karin,Kikyo,Kipfel,Komano,Kumaly,Kuuta,Lapwing,Lasyusha,Lili,Lime,LowpolyKon,Luchika,Lumina,Mafuyu,Mamehinata,Manuka,Mao,Maon,Marycia,Maya,Mayo,Milfy,Milfy Eku,Milltina,Miltina,Millitina,Minase,Misaki,Moe,Nakiya,Plum,Ramune,Rei,Reien,Rindo,Riru,Rui,Rurune,Rurune Mizuki,Selestia,Shinano,Shinra,Shuan,Sio,Suiha,TubeRose,Ukon,Uzuki,Wendy,Yugi Miyo";
        private const string DefaultVersionGroupRegex = @"(?i)(?:(?:v|ver|version)[\s_\-.]*(?<name>\d+(?:\.\d+){0,3})(?=$|[\s_\-.\]\)]|[^\d.])|(?:^|[\s_\-])(?<name>\d+\.\d+(?:\.\d+){0,2})(?=\.(?:zip|psd|mp4|unitypackage)(?:$|\s)|$|\s))";
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

        public static readonly SettingDefinition<bool> AutoSyncBlmOnStartup = new SettingDefinition<bool>(
            "assetManager.autoSyncBlmOnStartup",
            SettingScope.User,
            "settings.section.assetManager.source",
            "settings.autoSyncBlmOnStartup.label",
            "settings.autoSyncBlmOnStartup.tooltip",
            true,
            order: 1);

        public static readonly SettingDefinition<bool> AutoSyncEagleOnStartup = new SettingDefinition<bool>(
            "assetManager.autoSyncEagleOnStartup",
            SettingScope.User,
            "settings.section.assetManager.source",
            "settings.autoSyncEagleOnStartup.label",
            "settings.autoSyncEagleOnStartup.tooltip",
            true,
            order: 2);

        public static readonly SettingDefinition<string> AvatarNames = new SettingDefinition<string>(
            "assetManager.avatarNames",
            SettingScope.User,
            "settings.section.assetManager.import",
            "settings.avatarNames.label",
            "settings.avatarNames.tooltip",
            DefaultAvatarNames,
            order: 0);

        public static readonly SettingDefinition<string> VersionGroupRegex = new SettingDefinition<string>(
            "assetManager.versionGroupRegex",
            SettingScope.User,
            "settings.section.assetManager.import",
            "settings.versionGroupRegex.label",
            "settings.versionGroupRegex.tooltip",
            DefaultVersionGroupRegex,
            order: 1,
            validator: ValidateRegexOrEmpty);

        public static readonly SettingDefinition<int> ItemGridItemsPerRow = new SettingDefinition<int>(
            "assetManager.itemGridItemsPerRow",
            SettingScope.User,
            "settings.section.assetManager.view",
            "settings.itemGridItemsPerRow.label",
            "settings.itemGridItemsPerRow.tooltip",
            7,
            order: 0,
            validator: ValidateItemGridItemsPerRow);

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
            SettingApi.Register(AutoSyncBlmOnStartup);
            SettingApi.Register(AutoSyncEagleOnStartup);
            SettingApi.Register(AvatarNames);
            SettingApi.Register(VersionGroupRegex);
            SettingApi.Register(ItemGridItemsPerRow);
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

        private static SettingValidationResult ValidateItemGridItemsPerRow(int value)
        {
            return value >= 1 && value <= 12
                ? SettingValidationResult.Success
                : SettingValidationResult.Error(I18N.Get("settings.validation.itemGridItemsPerRow"));
        }

        private static SettingValidationResult ValidateRegexOrEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SettingValidationResult.Success;
            }

            try
            {
                Regex.Match(string.Empty, value);
                return SettingValidationResult.Success;
            }
            catch (ArgumentException)
            {
                return SettingValidationResult.Error(I18N.Get("settings.validation.regex"));
            }
        }
    }
}
