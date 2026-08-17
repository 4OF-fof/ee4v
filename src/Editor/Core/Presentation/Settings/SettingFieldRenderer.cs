using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    internal static class SettingFieldRenderer
    {
        public static VisualElement Create(
            Type valueType,
            string tooltip,
            object value,
            Action<object> onValueChanged)
        {
            if (valueType == typeof(bool))
            {
                var field = UiTextFactory.CreateToggle();
                field.tooltip = tooltip;
                field.value = value != null && (bool)value;
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(int))
            {
                var field = UiTextFactory.CreateIntegerField();
                field.tooltip = tooltip;
                field.value = value != null ? (int)value : 0;
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(float))
            {
                var field = UiTextFactory.CreateFloatField();
                field.tooltip = tooltip;
                field.value = value != null ? (float)value : 0f;
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(double))
            {
                var field = UiTextFactory.CreateDoubleField();
                field.tooltip = tooltip;
                field.value = value != null ? (double)value : 0d;
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(string))
            {
                var field = UiTextFactory.CreateTextField();
                field.tooltip = tooltip;
                field.value = value as string ?? string.Empty;
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(Color))
            {
                var field = UiTextFactory.CreateColorField();
                field.tooltip = tooltip;
                field.value = value != null ? (Color)value : Color.white;
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType.IsEnum)
            {
                var enumValue = value != null
                    ? (Enum)value
                    : (Enum)Enum.GetValues(valueType).GetValue(0);
                var field = UiTextFactory.CreateEnumField(string.Empty, enumValue);
                field.tooltip = tooltip;
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            var helpBox = UiTextFactory.CreateHelpBox(
                I18N.Get("settings.unsupportedType", new object[] { valueType.Name }),
                HelpBoxMessageType.Warning);
            helpBox.tooltip = tooltip;
            return helpBox;
        }
    }
}
