using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    internal static class SettingDrawerRegistry
    {
        private static readonly Dictionary<string, Func<string, string, object, string, Action<object>, VisualElement>>
            Drawers = new Dictionary<string, Func<string, string, object, string, Action<object>, VisualElement>>();

        public static void Register<T>(
            SettingDefinition<T> definition,
            Func<SettingDrawerContext<T>, VisualElement> drawer)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (drawer == null)
            {
                throw new ArgumentNullException(nameof(drawer));
            }

            Drawers[definition.Key] = (label, tooltip, value, searchContext, onValueChanged) =>
                drawer(new SettingDrawerContext<T>(
                    label,
                    tooltip,
                    value != null ? (T)value : default(T),
                    searchContext,
                    changed => onValueChanged?.Invoke(changed)));
        }

        public static VisualElement Create(
            SettingDefinitionBase definition,
            string label,
            string tooltip,
            object value,
            string searchContext,
            Action<object> onValueChanged)
        {
            if (Drawers.TryGetValue(definition.Key, out var drawer))
            {
                return drawer(label, tooltip, value, searchContext, onValueChanged);
            }

            return SettingFieldRenderer.Create(
                definition.ValueType,
                label,
                tooltip,
                value,
                onValueChanged);
        }
    }
}
