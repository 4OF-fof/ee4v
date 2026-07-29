using Ee4v.Core.Settings;

namespace Ee4v.HierarchyDecoration
{
    internal static class HierarchyDecorationDefinitions
    {
        public static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "hierarchyDecoration.enabled",
                SettingScope.User,
                "HierarchyDecoration",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 20,
                keywords: new[]
                {
                    "hierarchy",
                    "decoration",
                    "separator"
                });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Enabled);
        }
    }
}
