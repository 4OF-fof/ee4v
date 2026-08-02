using System;
using System.IO;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.SaveAndBackup.Domain;

namespace Ee4v.SaveAndBackup.Composition
{
    internal static class SaveAndBackupDefinitions
    {
        internal static readonly SettingDefinition<SaveAndBackupTrigger>
            Trigger = new SettingDefinition<SaveAndBackupTrigger>(
                "saveAndBackup.trigger",
                SettingScope.Project,
                "SaveAndBackup",
                "settings.section.backup",
                "settings.trigger.label",
                "settings.trigger.tooltip",
                SaveAndBackupTrigger.UploadSuccessOnly,
                order: 0);

        internal static readonly SettingDefinition<string> BackupRoot =
            new SettingDefinition<string>(
                "saveAndBackup.root",
                SettingScope.User,
                "SaveAndBackup",
                "settings.section.backup",
                "settings.root.label",
                "settings.root.tooltip",
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments),
                    "ee4v",
                    "save-and-backup"),
                order: 1,
                validator: value =>
                    string.IsNullOrWhiteSpace(value)
                        ? SettingValidationResult.Error(
                            I18N.Get(
                                "settings.validation.required"))
                        : SettingValidationResult.Success);

        internal static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Trigger);
            settings.Register(BackupRoot);
        }
    }
}
