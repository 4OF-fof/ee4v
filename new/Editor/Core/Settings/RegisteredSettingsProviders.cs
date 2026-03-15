using Ee4v.I18n;
using Ee4v.Phase1;
using UnityEditor;

namespace Ee4v.Settings
{
    internal static class RegisteredSettingsProviders
    {
        [SettingsProvider]
        public static SettingsProvider CreateUserProvider()
        {
            Phase1Bootstrap.EnsureInitialized();
            return new SettingsProvider("Preferences/4OF/ee4v", SettingsScope.User)
            {
                label = "ee4v",
                guiHandler = searchContext => { SettingsUiRenderer.DrawScope(SettingScope.User, searchContext); },
                keywords = new[] { "ee4v", I18N.Get("settings.section.localization") }
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProjectProvider()
        {
            Phase1Bootstrap.EnsureInitialized();
            return new SettingsProvider("Project/4OF/ee4v", SettingsScope.Project)
            {
                label = "ee4v",
                guiHandler = searchContext => { SettingsUiRenderer.DrawScope(SettingScope.Project, searchContext); },
                keywords = new[] { "ee4v", I18N.Get("settings.section.injectorProject") }
            };
        }
    }
}
