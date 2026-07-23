using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine.UIElements;

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
                activateHandler = (searchContext, root) =>
                    Activate(root, settingScope, searchContext),
                keywords = keywords
            };
        }

        private static void Activate(
            VisualElement root,
            SettingScope settingScope,
            string searchContext)
        {
            var settings = CoreSettings.Current;
            SettingsUiRenderer.BuildScope(root, settings, settingScope, searchContext);

            void OnLocalizationReloaded()
            {
                root.schedule.Execute(() =>
                    SettingsUiRenderer.BuildScope(root, settings, settingScope, searchContext));
            }

            I18N.Reloaded += OnLocalizationReloaded;
            root.RegisterCallback<DetachFromPanelEvent>(
                _ => I18N.Reloaded -= OnLocalizationReloaded);
        }
    }
}
