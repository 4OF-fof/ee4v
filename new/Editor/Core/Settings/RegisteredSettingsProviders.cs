using Ee4v.Core.I18n;
using UnityEditor;

namespace Ee4v.Core.Settings
{
    internal static class RegisteredSettingsProviders
    {
        [SettingsProvider]
        public static SettingsProvider CreateUserProvider()
        {
            return CreateProvider(
                "Preferences/4OF/ee4v",
                SettingsScope.User,
                SettingScope.User,
                new[] { "ee4v", "settings", "localization" });
        }

        [SettingsProvider]
        public static SettingsProvider CreateProjectProvider()
        {
            return CreateProvider(
                "Project/4OF/ee4v",
                SettingsScope.Project,
                SettingScope.Project,
                new[] { "ee4v", "settings", "project", "injector" });
        }

        private static SettingsProvider CreateProvider(
            string path,
            SettingsScope settingsScope,
            SettingScope settingScope,
            string[] keywords)
        {
            return new SettingsProvider(path, settingsScope)
            {
                label = "ee4v",
                guiHandler = searchContext => { SettingsUiRenderer.DrawScope(settingScope, searchContext); },
                keywords = keywords
            };
        }
    }
}
