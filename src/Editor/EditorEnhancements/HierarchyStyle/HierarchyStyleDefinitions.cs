using Ee4v.Core.Settings;

namespace Ee4v.HierarchyStyle
{
    internal static class HierarchyStyleDefinitions
    {
        public static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "hierarchyStyle.enabled",
                SettingScope.User,
                "HierarchyStyle",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 10,
                keywords: new[]
                {
                    "hierarchy",
                    "color",
                    "background",
                    "icon",
                    "hide",
                    "alt"
                });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Enabled);
        }
    }
}
