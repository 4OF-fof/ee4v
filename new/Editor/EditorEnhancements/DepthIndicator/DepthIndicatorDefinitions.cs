using Ee4v.Core.Settings;

namespace Ee4v.DepthIndicator
{
    internal static class DepthIndicatorDefinitions
    {
        public static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "depthIndicator.enabled",
                SettingScope.User,
                "DepthIndicator",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[] { "hierarchy", "depth", "indicator" });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Enabled);
        }
    }
}
