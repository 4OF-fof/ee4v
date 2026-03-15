using System;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Settings
{
    internal static class SettingFieldRenderer
    {
        public static object Draw(Type valueType, GUIContent label, object value)
        {
            if (valueType == typeof(bool))
            {
                return EditorGUILayout.Toggle(label, value != null && (bool)value);
            }

            if (valueType == typeof(int))
            {
                return EditorGUILayout.IntField(label, value != null ? (int)value : 0);
            }

            if (valueType == typeof(float))
            {
                return EditorGUILayout.FloatField(label, value != null ? (float)value : 0f);
            }

            if (valueType == typeof(double))
            {
                return EditorGUILayout.DoubleField(label, value != null ? (double)value : 0d);
            }

            if (valueType == typeof(string))
            {
                return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            }

            if (valueType == typeof(Color))
            {
                return EditorGUILayout.ColorField(label, value != null ? (Color)value : Color.white);
            }

            if (valueType.IsEnum)
            {
                var enumValue = value != null ? (Enum)value : (Enum)Enum.GetValues(valueType).GetValue(0);
                return EditorGUILayout.EnumPopup(label, enumValue);
            }

            EditorGUILayout.LabelField(label, new GUIContent("Unsupported type: " + valueType.Name));
            return value;
        }
    }
}
