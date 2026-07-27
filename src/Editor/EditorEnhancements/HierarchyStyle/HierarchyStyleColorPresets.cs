using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal static class HierarchyStyleColorPresets
    {
        private const float Alpha = 0.32f;

        private static readonly Color[] DarkValues =
        {
            new Color(0.85f, 0.18f, 0.18f, Alpha),
            new Color(0.9f, 0.48f, 0.16f, Alpha),
            new Color(0.9f, 0.78f, 0.16f, Alpha),
            new Color(0.52f, 0.8f, 0.18f, Alpha),
            new Color(0.2f, 0.75f, 0.3f, Alpha),
            new Color(0.16f, 0.76f, 0.58f, Alpha),
            new Color(0.16f, 0.72f, 0.78f, Alpha),
            new Color(0.18f, 0.5f, 0.88f, Alpha),
            new Color(0.3f, 0.3f, 0.9f, Alpha),
            new Color(0.55f, 0.26f, 0.88f, Alpha),
            new Color(0.8f, 0.22f, 0.78f, Alpha),
            new Color(0.88f, 0.2f, 0.5f, Alpha)
        };

        private static readonly Color[] LightValues =
        {
            new Color(0.95f, 0.16f, 0.16f, Alpha),
            new Color(0.95f, 0.45f, 0.12f, Alpha),
            new Color(0.88f, 0.72f, 0.08f, Alpha),
            new Color(0.45f, 0.72f, 0.1f, Alpha),
            new Color(0.12f, 0.68f, 0.22f, Alpha),
            new Color(0.08f, 0.66f, 0.48f, Alpha),
            new Color(0.08f, 0.62f, 0.72f, Alpha),
            new Color(0.12f, 0.42f, 0.82f, Alpha),
            new Color(0.22f, 0.22f, 0.85f, Alpha),
            new Color(0.5f, 0.18f, 0.82f, Alpha),
            new Color(0.75f, 0.14f, 0.72f, Alpha),
            new Color(0.85f, 0.14f, 0.44f, Alpha)
        };

        public static IReadOnlyList<Color> GetAll()
        {
            return EditorGUIUtility.isProSkin
                ? DarkValues
                : LightValues;
        }
    }
}
