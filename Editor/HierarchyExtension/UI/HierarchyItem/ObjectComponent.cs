using UnityEditor;
using UnityEngine;

using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.HierarchyExtension.UI.HierarchyItem.Window;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyItem {
    public static class ObjectComponent {
        public static void Draw(GameObject obj, Rect componentRect) {
            if (obj == null) return;

            var iconPosition = componentRect;
            iconPosition.x = iconPosition.xMax - 16f;
            iconPosition.width = iconPosition.height = 16f;

            foreach (var component in obj.GetComponents<Component>()) {
                if (iconPosition.x <= componentRect.x) break;
                var componentType = component.GetType();
                if (componentType.Name == "ObjectStyleComponent") continue;
                if (EditorPrefsManager.IgnoreComponentNameList.Contains(componentType.Name)) continue;

                Texture image = AssetPreview.GetMiniThumbnail(component);
                if (image == null) continue;

                var iconRect = new Rect(iconPosition.x, iconPosition.y - 1f, iconPosition.width, iconPosition.height);

                var behaviourComponent = component as Behaviour;
                var prevColor = GUI.color;
                if (behaviourComponent != null && !behaviourComponent.enabled) {
                    GUI.color = ColorPreset.InActiveItem;
                }

                var tooltipContent = new GUIContent(string.Empty, null, componentType.Name);
                GUI.DrawTexture(iconRect, image);
                GUI.Label(iconRect, tooltipContent, GUIStyle.none);

                GUI.color = prevColor;

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && iconRect.Contains(e.mousePosition)) {
                    var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(componentRect.xMax + 2 * (componentRect.xMax - e.mousePosition.x), componentRect.y));
                    ComponentInspector.Open(component, obj, anchorScreen);
                    e.Use();
                }

                iconPosition.x -= 16f;
            }
        }
    }
}