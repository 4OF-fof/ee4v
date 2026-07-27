using Ee4v.Core.Settings;

namespace Ee4v.HiddenObjects
{
    internal static class HiddenObjectsDefinitions
    {
        internal const string DefaultExcludedScenePatterns = "*NDMF*";
        internal const string DefaultExcludedObjectPatterns =
            "nadena.dev.ndmf*Activator";

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

        public static readonly SettingDefinition<string>
            ExcludedScenePatterns =
                new SettingDefinition<string>(
                    "hiddenObjects.exclusions.scenePatterns",
                    SettingScope.User,
                    "HiddenObjects",
                    "settings.section.exclusions",
                    "settings.exclusions.scenes.label",
                    "settings.exclusions.scenes.tooltip",
                    DefaultExcludedScenePatterns,
                    order: 0,
                    keywords: new[]
                    {
                        "hidden",
                        "exclude",
                        "scene",
                        "ndmf"
                    });

        public static readonly SettingDefinition<string>
            ExcludedObjectPatterns =
                new SettingDefinition<string>(
                    "hiddenObjects.exclusions.objectPatterns",
                    SettingScope.User,
                    "HiddenObjects",
                    "settings.section.exclusions",
                    "settings.exclusions.objects.label",
                    "settings.exclusions.objects.tooltip",
                    DefaultExcludedObjectPatterns,
                    order: 1,
                    keywords: new[]
                    {
                        "hidden",
                        "exclude",
                        "object",
                        "ndmf"
                    });

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(HierarchyButtonEnabled);
            settings.Register(ExcludedScenePatterns);
            settings.Register(ExcludedObjectPatterns);
        }
    }
}
