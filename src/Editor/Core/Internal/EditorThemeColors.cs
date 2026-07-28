using UnityEditor;
using UnityEngine;

namespace Ee4v.Core
{
    internal static class EditorThemeColors
    {
        private static readonly Color32 DarkHierarchyDepthLine =
            new Color32(104, 104, 104, 255);
        private static readonly Color32 LightHierarchyDepthLine =
            new Color32(142, 142, 142, 255);

        public static Color HierarchyDepthLine
        {
            get
            {
                return ResolveHierarchyDepthLine(
                    EditorGUIUtility.isProSkin);
            }
        }

        internal static Color32 ResolveHierarchyDepthLine(
            bool isProSkin)
        {
            return isProSkin
                ? DarkHierarchyDepthLine
                : LightHierarchyDepthLine;
        }
    }
}
