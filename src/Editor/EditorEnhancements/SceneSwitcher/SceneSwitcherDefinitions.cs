using Ee4v.Core.Settings;

namespace Ee4v.SceneSwitcher
{
    internal static class SceneSwitcherDefinitions
    {
        public static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "sceneSwitcher.enabled",
                SettingScope.User,
                "SceneSwitcher",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "hierarchy",
                    "scene",
                    "switcher"
                });

        public static readonly SettingDefinition<string>
            CreateFolder =
                new SettingDefinition<string>(
                    "sceneSwitcher.createFolder",
                    SettingScope.User,
                    "SceneSwitcher",
                    "settings.section.creation",
                    "settings.createFolder.label",
                    "settings.createFolder.tooltip",
                    "Assets/Scene",
                    order: 0,
                    keywords: new[]
                    {
                        "scene",
                        "create",
                        "folder",
                        "template"
                    });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Enabled);
            settings.Register(CreateFolder);
        }
    }
}
