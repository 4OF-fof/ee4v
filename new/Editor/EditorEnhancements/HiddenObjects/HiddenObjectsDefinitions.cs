using Ee4v.Core.Settings;

namespace Ee4v.HiddenObjects
{
    internal static class HiddenObjectsDefinitions
    {
        public static readonly SettingDefinition<bool> HierarchyButtonEnabled =
            new SettingDefinition<bool>(
                "hiddenObjects.hierarchyButton.enabled",
                SettingScope.User,
                "HiddenObjects",
                "settings.section.hierarchy",
                "settings.hierarchyButton.label",
                "settings.hierarchyButton.tooltip",
                true,
                order: 0,
                keywords: new[] { "hierarchy", "hidden", "visibility" });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(HierarchyButtonEnabled);
        }
    }
}
