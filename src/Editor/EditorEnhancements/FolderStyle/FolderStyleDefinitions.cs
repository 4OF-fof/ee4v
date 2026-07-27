using Ee4v.Core.Settings;

namespace Ee4v.FolderStyle
{
    internal static class FolderStyleDefinitions
    {
        public static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "folderStyle.enabled",
                SettingScope.User,
                "FolderStyle",
                "settings.section.project",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "project",
                    "folder",
                    "color",
                    "icon",
                    "style"
                });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Enabled);
        }
    }
}
