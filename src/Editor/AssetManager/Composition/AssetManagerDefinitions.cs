using System;
using System.IO;
using System.Text.RegularExpressions;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.AssetManager.Composition
{
    internal static class AssetManagerDefinitions
    {
        private const string BoothLibraryRelativePath = "pm.booth.library-manager\\data.db";
        private const string DefaultAvatarNames = "Airi,Alue,Bokusei,Chiffon,Chocolat,Eku,ELusion,Grus,Hanka,Ichigo,Kaguya,Kanata,Karin,Kikyo,Kipfel,Komano,Kumaly,Kuuta,Lapwing,Lasyusha,Lili,Lime,LowpolyKon,Luchika,Lumina,Mafuyu,Mamehinata,Manuka,Mao,Maon,Marycia,Maya,Mayo,Milfy,Milfy Eku,Milltina,Miltina,Millitina,Minase,Misaki,Moe,Nakiya,Plum,Ramune,Rei,Reien,Rindo,Riru,Rui,Rurune,Rurune Mizuki,Selestia,Shinano,Shinra,Shuan,Sio,Suiha,TubeRose,Ukon,Uzuki,Wendy,Yugi Miyo";
        private const string DefaultVersionGroupRegex = @"(?i)(?:(?:v|ver|version)[\s_\-.]*(?<name>\d+(?:\.\d+){0,3})(?=$|[\s_\-.\]\)]|[^\d.])|(?:^|[\s_\-])(?<name>\d+\.\d+(?:\.\d+){0,2})(?=\.(?:zip|psd|mp4|unitypackage)(?:$|\s)|$|\s))";
        public static readonly SettingRange<int> ItemGridItemsPerRowRange = new SettingRange<int>(
            1,
            12,
            () => SettingValidationResult.Error(I18N.Get("settings.validation.itemGridItemsPerRow")));

        public static readonly SettingDefinition<string> Ee4vGlobalPath = new SettingDefinition<string>(
            "assetManager.ee4vGlobalPath",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.paths",
            "settings.ee4vGlobalPath.label",
            "settings.ee4vGlobalPath.tooltip",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ee4v"),
            order: 0,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<string> BlmDatabasePath = new SettingDefinition<string>(
            "assetManager.blmDatabasePath",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.paths",
            "settings.blmDatabasePath.label",
            "settings.blmDatabasePath.tooltip",
            GetDefaultBlmDatabasePath(),
            order: 1,
            validator: ValidateNonEmpty);

        public static readonly SettingDefinition<string> EagleLibraryPath = new SettingDefinition<string>(
            "assetManager.eagleLibraryPath",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.paths",
            "settings.eagleLibraryPath.label",
            "settings.eagleLibraryPath.tooltip",
            string.Empty,
            order: 2);

        public static readonly SettingDefinition<string> SourcePriority = new SettingDefinition<string>(
            "assetManager.sourcePriority",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.source",
            "settings.sourcePriority.label",
            "settings.sourcePriority.tooltip",
            "ee4v,eagle,blm",
            order: 0,
            validator: ValidateSourcePriority);

        public static readonly SettingDefinition<bool> AutoSyncBlmOnStartup = new SettingDefinition<bool>(
            "assetManager.autoSyncBlmOnStartup",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.source",
            "settings.autoSyncBlmOnStartup.label",
            "settings.autoSyncBlmOnStartup.tooltip",
            true,
            order: 1);

        public static readonly SettingDefinition<bool> AutoSyncEagleOnStartup = new SettingDefinition<bool>(
            "assetManager.autoSyncEagleOnStartup",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.source",
            "settings.autoSyncEagleOnStartup.label",
            "settings.autoSyncEagleOnStartup.tooltip",
            true,
            order: 2);

        public static readonly SettingDefinition<string> AvatarNames = new SettingDefinition<string>(
            "assetManager.avatarNames",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.import",
            "settings.avatarNames.label",
            "settings.avatarNames.tooltip",
            DefaultAvatarNames,
            order: 0);

        public static readonly SettingDefinition<string> VersionGroupRegex = new SettingDefinition<string>(
            "assetManager.versionGroupRegex",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.import",
            "settings.versionGroupRegex.label",
            "settings.versionGroupRegex.tooltip",
            DefaultVersionGroupRegex,
            order: 1,
            validator: ValidateRegexOrEmpty);

        public static readonly SettingDefinition<bool> ShowUnityPackageImportDialog = new SettingDefinition<bool>(
            "assetManager.showUnityPackageImportDialog",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.import",
            "settings.showUnityPackageImportDialog.label",
            "settings.showUnityPackageImportDialog.tooltip",
            true,
            order: 2);

        public static readonly SettingDefinition<int> ItemGridItemsPerRow = new SettingDefinition<int>(
            "assetManager.itemGridItemsPerRow",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.view",
            "settings.itemGridItemsPerRow.label",
            "settings.itemGridItemsPerRow.tooltip",
            7,
            order: 0,
            range: ItemGridItemsPerRowRange);

        public static readonly SettingDefinition<bool> ShowFileTreeImageTooltip = new SettingDefinition<bool>(
            "assetManager.showFileTreeImageTooltip",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.view",
            "settings.showFileTreeImageTooltip.label",
            "settings.showFileTreeImageTooltip.tooltip",
            true,
            order: 1);

        public static readonly SettingDefinition<int> HistoryOverlayMaximumItems = new SettingDefinition<int>(
            "assetManager.historyOverlayMaximumItems",
            SettingScope.User,
            "AssetManager",
            "settings.section.assetManager.view",
            "settings.historyOverlayMaximumItems.label",
            "settings.historyOverlayMaximumItems.tooltip",
            5,
            order: 2,
            validator: ValidateHistoryOverlayMaximumItems);

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Ee4vGlobalPath);
            settings.Register(BlmDatabasePath);
            settings.Register(EagleLibraryPath);
            settings.Register(SourcePriority);
            settings.Register(AutoSyncBlmOnStartup);
            settings.Register(AutoSyncEagleOnStartup);
            settings.Register(AvatarNames);
            settings.Register(VersionGroupRegex);
            settings.Register(ShowUnityPackageImportDialog);
            settings.Register(ItemGridItemsPerRow);
            settings.Register(ShowFileTreeImageTooltip);
            settings.Register(HistoryOverlayMaximumItems);
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

        private static SettingValidationResult ValidateHistoryOverlayMaximumItems(int value)
        {
            return value >= 1 && value <= 20
                ? SettingValidationResult.Success
                : SettingValidationResult.Error(I18N.Get("settings.validation.historyOverlayMaximumItems"));
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
