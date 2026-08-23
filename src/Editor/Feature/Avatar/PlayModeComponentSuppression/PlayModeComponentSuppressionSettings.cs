using System;
using System.Collections.Generic;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.PlayModeComponentSuppression
{
    [InitializeOnLoad]
    public static class PlayModeComponentSuppressionSettings
    {
        private static readonly SettingDefinition<string> SuppressedTypes =
            new SettingDefinition<string>(
                "playModeComponentSuppression.suppressedTypes",
                SettingScope.Project,
                "PlayModeComponentSuppression",
                "settings.section.playMode",
                "settings.suppressedTypes.label",
                "settings.suppressedTypes.tooltip",
                string.Empty,
                keywords: new[]
                {
                    "avatar",
                    "component",
                    "ndmf",
                    "play mode",
                    "suppress"
                });

        static PlayModeComponentSuppressionSettings()
        {
            EnsureRegistered();
        }

        public static IReadOnlyCollection<Type> GetSuppressedTypes()
        {
            var settings = EnsureRegistered();
            return ComponentTypeIdentity.ResolveAll(
                settings.Get(SuppressedTypes));
        }

        private static ISettingsService EnsureRegistered()
        {
            var settings = CoreSettings.Current;
            settings.Register(SuppressedTypes);
            ComponentTypeSettingDrawer.Register(SuppressedTypes);
            return settings;
        }
    }
}
