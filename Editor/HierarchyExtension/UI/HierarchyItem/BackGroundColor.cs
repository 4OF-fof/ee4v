using UnityEditor;
using UnityEngine;

using _4OF.ee4v.Core.UI;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyItem {
    public static class BackGroundColor {
        public static void Draw(Object obj, Rect selectionRect, Color color, Texture icon = null) {
            var hierarchyViewWidth = EditorGUIUtility.currentViewWidth;
            var nameSize = EditorStyles.label.CalcSize(new GUIContent(obj != null ? obj.name : string.Empty));
            var iconSize = new Vector2(16f, 16f);
            
            var backRect = new Rect(32f, selectionRect.y, hierarchyViewWidth, selectionRect.height);
            var iconRect = new Rect(selectionRect.x, selectionRect.y + (selectionRect.height - iconSize.y) / 2f, iconSize.x, iconSize.y);
            var nameRect = new Rect(selectionRect.x + 17f, selectionRect.y, nameSize.x, nameSize.y);
            ColorPreset.DrawGradient(backRect, color, Color.clear);

            if (Mathf.Approximately(color.a, 0f)) return;
            
            var prevColor = GUI.color;
            GUI.color = obj is GameObject { activeInHierarchy: false } ? ColorPreset.InActiveItem : Color.white;
            if (obj != null) {
                if (icon == null) {
                    if (PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab &&
                        !PrefabUtility.IsAnyPrefabInstanceRoot(obj as GameObject)) {
                        icon = EditorGUIUtility.IconContent("GameObject Icon").image;
                    }
                    else {
                        icon = EditorGUIUtility.ObjectContent(obj, typeof(GameObject)).image;
                    }
                }
                if (icon != null) {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                }
            }

            var displayName = obj != null ? obj.name : string.Empty;
            if (obj is GameObject go && PrefabUtility.IsAnyPrefabInstanceRoot(go)) {
                GUI.color = Color.cyan;
            }
            GUI.Label(nameRect, displayName, EditorStyles.label);
            GUI.color = prevColor;
        }
    }
}