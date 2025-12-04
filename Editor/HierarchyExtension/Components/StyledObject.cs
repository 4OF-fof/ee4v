using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Wraps;
using _4OF.ee4v.HierarchyExtension.Components.CustomStyle;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class StyledObject : IHierarchyExtensionComponent {
        public int Priority => -200;
        public string Name => "Styled Object";
        public string Description => I18N.Get("_System.HierarchyExtension.StyledObject.Description");
        public string Trigger => I18N.Get("_System.HierarchyExtension.StyledObject.Trigger");

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;
            if (!SettingSingleton.I.enableCustomStyleItem) return;

            if (gameObject.IsCustomStyleItem()) return;

            GetEffectiveStyle(gameObject, out var color, out var icon, out var isSelf);

            if (color == Color.clear && !isSelf) return;

            if (isSelf) {
                var textureToSet = icon != null ? icon : GetDefaultIcon(gameObject);
                SceneHierarchyWindowWrap.LastInteractedWindow?.SetItemIcon(instanceID, textureToSet as Texture2D);
            }

            DrawBackGround(gameObject, fullRect, color, icon);
        }

        private static void GetEffectiveStyle(GameObject obj, out Color color, out Texture icon, out bool isSelf) {
            color = Color.clear;
            icon = null;
            isSelf = false;

            var foundColor = false;
            var foundIcon = false;

            var selfComponent = obj.GetComponent<ObjectStyleComponent>();
            if (selfComponent != null) {
                isSelf = true;
                if (selfComponent.color != Color.clear) {
                    color = selfComponent.color;
                    foundColor = true;
                }

                if (selfComponent.icon != null) {
                    icon = selfComponent.icon;
                    foundIcon = true;
                }
            }

            if (foundColor && foundIcon) return;
            var current = obj.transform.parent;

            while (current != null) {
                if (foundColor && foundIcon) break;

                var comp = current.GetComponent<ObjectStyleComponent>();
                if (comp != null) {
                    if (!foundColor && comp.color != Color.clear) {
                        color = comp.color;
                        foundColor = true;
                    }

                    if (!foundIcon && comp.icon != null) {
                        icon = comp.icon;
                        foundIcon = true;
                    }
                }

                current = current.parent;
            }
        }

        private static Texture GetDefaultIcon(GameObject obj) {
            if (PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab &&
                !PrefabUtility.IsAnyPrefabInstanceRoot(obj))
                return EditorGUIUtility.IconContent("GameObject Icon").image;

            return EditorGUIUtility.ObjectContent(obj, typeof(GameObject)).image;
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
            GUI.color = obj is GameObject { activeInHierarchy: false } ? ColorPreset.InactiveItem : Color.white;
            if (obj != null) {
                if (icon == null) icon = GetDefaultIcon(obj as GameObject);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            var displayName = obj != null ? obj.name : string.Empty;
            if (obj is GameObject go && PrefabUtility.IsAnyPrefabInstanceRoot(go))
                GUI.color = ColorPreset.PrefabRootText;
            GUI.Label(nameRect, displayName, EditorStyles.label);
            GUI.color = prevColor;
        }
    }
}