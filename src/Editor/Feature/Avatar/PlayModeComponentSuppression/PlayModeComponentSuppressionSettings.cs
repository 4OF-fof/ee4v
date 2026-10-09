using System;
using System.Collections.Generic;
using System.Linq;
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

        public static IReadOnlyList<string> GetConfiguredTypes() =>
            ComponentTypeIdentity.Parse(EnsureRegistered().Get(SuppressedTypes));

        public static IReadOnlyList<string> GetAvailableTypes() => TypeCache.GetTypesDerivedFrom<UnityEngine.MonoBehaviour>()
            .Where(ComponentTypeIdentity.IsSelectable).Select(ComponentTypeIdentity.Create)
            .OrderBy(identity => identity, StringComparer.Ordinal).ToArray();

        public static bool IsAvailable(string identity) => ComponentTypeIdentity.Resolve(identity) != null;

        public static void SetConfiguredTypes(IEnumerable<string> identities)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode is required.");
            if (identities == null) throw new ArgumentNullException(nameof(identities));
            var proposed = identities.ToArray();
            var existing = GetConfiguredTypes();
            if (proposed.Any(identity => !existing.Contains(identity) && !IsAvailable(identity)))
                throw new ArgumentException("New entries must be loaded concrete MonoBehaviour types; existing missing types may be retained.");
            EnsureRegistered().Set(SuppressedTypes, ComponentTypeIdentity.Serialize(proposed));
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
