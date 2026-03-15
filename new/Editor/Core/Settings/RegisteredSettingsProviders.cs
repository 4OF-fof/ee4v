using Ee4v.Core.I18n;
using UnityEditor;

namespace Ee4v.Core.Settings
{
    internal static class RegisteredSettingsProviders
    {
        [SettingsProvider]
        public static SettingsProvider CreateUserProvider()
        {
            CoreLocalizationDefinitions.RegisterAll();
            return new SettingsProvider("Preferences/4OF/ee4v", SettingsScope.User)
            {
                label = "ee4v",
                guiHandler = searchContext => { SettingsUiRenderer.DrawScope(SettingScope.User, searchContext); },
                keywords = new[] { "ee4v", "settings", "localization" }
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProjectProvider()
        {
            CoreLocalizationDefinitions.RegisterAll();
            return new SettingsProvider("Project/4OF/ee4v", SettingsScope.Project)
            {
                label = "ee4v",
                guiHandler = searchContext => { SettingsUiRenderer.DrawScope(SettingScope.Project, searchContext); },
                keywords = new[] { "ee4v", "settings", "project", "injector" }
            };
        }
    }
}
