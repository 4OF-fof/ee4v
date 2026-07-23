using System;
using Ee4v.Core.I18n;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    internal static class SettingFieldRenderer
    {
        public static VisualElement Create(
            Type valueType,
            string label,
            string tooltip,
            object value,
            Action<object> onValueChanged)
        {
            if (valueType == typeof(bool))
            {
                var field = new Toggle(label)
                {
                    tooltip = tooltip,
                    value = value != null && (bool)value
                };
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(int))
            {
                var field = new IntegerField(label)
                {
                    tooltip = tooltip,
                    value = value != null ? (int)value : 0
                };
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(float))
            {
                var field = new FloatField(label)
                {
                    tooltip = tooltip,
                    value = value != null ? (float)value : 0f
                };
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(double))
            {
                var field = new DoubleField(label)
                {
                    tooltip = tooltip,
                    value = value != null ? (double)value : 0d
                };
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(string))
            {
                var field = new TextField(label)
                {
                    tooltip = tooltip,
                    value = value as string ?? string.Empty
                };
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType == typeof(Color))
            {
                var field = new ColorField(label)
                {
                    tooltip = tooltip,
                    value = value != null ? (Color)value : Color.white
                };
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            if (valueType.IsEnum)
            {
                var enumValue = value != null
                    ? (Enum)value
                    : (Enum)Enum.GetValues(valueType).GetValue(0);
                var field = new EnumField(label, enumValue)
                {
                    tooltip = tooltip
                };
                field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
                return field;
            }

            return new HelpBox(
                I18N.Get("settings.unsupportedType", new object[] { valueType.Name }),
                HelpBoxMessageType.Warning)
            {
                tooltip = tooltip
            };
        }
    }
}
