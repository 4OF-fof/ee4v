using Ee4v.Core.Settings;

namespace Ee4v.ProjectTabs
{
    internal static class ProjectTabsDefinitions
    {
        public static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "projectTabs.enabled",
                SettingScope.User,
                "ProjectTabs",
                "settings.section.project",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "project",
                    "tab",
                    "history",
                    "navigation"
                });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Enabled);
        }
    }
}
