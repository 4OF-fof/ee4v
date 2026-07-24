using Ee4v.Core.Settings;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal static class HiddenObjectsSettingDrawers
    {
        private const float PatternFieldMinimumHeight = 64f;
        private static bool _registered;

        public static void Register()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            SettingDrawerRegistry.Register(
                HiddenObjectsDefinitions.ExcludedScenePatterns,
                CreatePatternField);
            SettingDrawerRegistry.Register(
                HiddenObjectsDefinitions.ExcludedObjectPatterns,
                CreatePatternField);
        }

        private static VisualElement CreatePatternField(
            SettingDrawerContext<string> context)
        {
            var field = new TextField(context.Label)
            {
                tooltip = context.Tooltip,
                value = context.Value ?? string.Empty,
                multiline = true
            };
            field.style.minHeight = PatternFieldMinimumHeight;
            field.RegisterValueChangedCallback(
                evt => context.NotifyValueChanged(evt.newValue));
            return field;
        }
    }
}
