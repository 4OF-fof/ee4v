using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Wraps;
using _4OF.ee4v.HierarchyExtension.Components.CustomStyle;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HierarchyBackgroundColor : IHierarchyExtensionComponent {
        public int Priority => -200;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;
            if (!SettingSingleton.I.enableCustomStyleItem) return;

            if (gameObject.IsCustomStyleItem()) return;

            var (style, isSelf) = GetEffectiveStyle(gameObject);
            if (style == null) return;

            if (isSelf) {
                SceneHierarchyWindowWrap.SetItemIcon(instanceID, style.icon as Texture2D);
                DrawBackGround(gameObject, fullRect, style.color, style.icon);
            }
            else {
                DrawBackGround(gameObject, fullRect, style.color);
            }
        }

        private static (ObjectStyleComponent, bool) GetEffectiveStyle(GameObject obj) {
            var current = obj.transform;
            while (current != null) {
                var style = current.GetComponent<ObjectStyleComponent>();
                if (style != null && (style.color != Color.clear || style.icon != null))
                    return (style, current.gameObject == obj);
                current = current.parent;
            }

            return (null, false);
        }

        private static void DrawBackGround(Object obj, Rect selectionRect, Color color, Texture icon = null) {
            var hierarchyViewWidth = EditorGUIUtility.currentViewWidth;
            var nameSize = EditorStyles.label.CalcSize(new GUIContent(obj != null ? obj.name : string.Empty));
            var iconSize = new Vector2(16f, 16f);

            var backRect = new Rect(32f, selectionRect.y, hierarchyViewWidth, selectionRect.height);
            var iconRect = new Rect(selectionRect.x, selectionRect.y + (selectionRect.height - iconSize.y) / 2f,
                iconSize.x, iconSize.y);
            var nameRect = new Rect(selectionRect.x + 17f, selectionRect.y, nameSize.x, nameSize.y);
            ColorPreset.DrawGradient(backRect, color, Color.clear);

            if (Mathf.Approximately(color.a, 0f)) return;

            var prevColor = GUI.color;
            GUI.color = obj is GameObject { activeInHierarchy: false } ? ColorPreset.InActiveItem : Color.white;
            if (obj != null) {
                if (icon == null) {
                    if (PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab &&
                        !PrefabUtility.IsAnyPrefabInstanceRoot(obj as GameObject))
                        icon = EditorGUIUtility.IconContent("GameObject Icon").image;
                    else
                        icon = EditorGUIUtility.ObjectContent(obj, typeof(GameObject)).image;
                }

                if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            var displayName = obj != null ? obj.name : string.Empty;
            if (obj is GameObject go && PrefabUtility.IsAnyPrefabInstanceRoot(go))
                GUI.color = ColorPreset.PrefabRootText;
            GUI.Label(nameRect, displayName, EditorStyles.label);
            GUI.color = prevColor;
        }
    }
}