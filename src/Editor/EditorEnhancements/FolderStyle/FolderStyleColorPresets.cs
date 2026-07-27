using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderStyle
{
    internal static class FolderStyleColorPresets
    {
        private const float Alpha = 0.7f;

        private static readonly Color[] DarkValues =
        {
            new Color(0.7f, 0f, 0f, Alpha),
            new Color(0.7f, 0.35f, 0f, Alpha),
            new Color(0.7f, 0.7f, 0f, Alpha),
            new Color(0.35f, 0.7f, 0f, Alpha),
            new Color(0f, 0.7f, 0f, Alpha),
            new Color(0f, 0.7f, 0.35f, Alpha),
            new Color(0f, 0.7f, 0.7f, Alpha),
            new Color(0f, 0.35f, 0.7f, Alpha),
            new Color(0f, 0f, 0.7f, Alpha),
            new Color(0.35f, 0f, 0.7f, Alpha),
            new Color(0.7f, 0f, 0.7f, Alpha),
            new Color(0.7f, 0f, 0.35f, Alpha)
        };

        private static readonly Color[] LightValues =
        {
            new Color(1f, 0.2f, 0.2f, Alpha),
            new Color(1f, 0.55f, 0.2f, Alpha),
            new Color(1f, 1f, 0.2f, Alpha),
            new Color(0.55f, 1f, 0.2f, Alpha),
            new Color(0.2f, 1f, 0.2f, Alpha),
            new Color(0.2f, 1f, 0.55f, Alpha),
            new Color(0.2f, 1f, 1f, Alpha),
            new Color(0.2f, 0.55f, 1f, Alpha),
            new Color(0.2f, 0.2f, 1f, Alpha),
            new Color(0.55f, 0.2f, 1f, Alpha),
            new Color(1f, 0.2f, 1f, Alpha),
            new Color(1f, 0.2f, 0.55f, Alpha)
        };

        public static IReadOnlyList<Color> GetAll()
        {
            return EditorGUIUtility.isProSkin
                ? DarkValues
                : LightValues;
        }
    }
}
