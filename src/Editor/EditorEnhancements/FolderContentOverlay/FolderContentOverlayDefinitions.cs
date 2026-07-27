using Ee4v.Core.Settings;

namespace Ee4v.FolderContentOverlay
{
    internal static class FolderContentOverlayDefinitions
    {
        public static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "folderContentOverlay.enabled",
                SettingScope.User,
                "FolderContentOverlay",
                "settings.section.project",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[] { "project", "folder", "content", "overlay" });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Enabled);
        }
    }
}
