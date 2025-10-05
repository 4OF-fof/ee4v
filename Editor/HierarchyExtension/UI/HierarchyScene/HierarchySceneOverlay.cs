using UnityEditor;
using UnityEngine;

using _4OF.ee4v.Core.Data;
using _4OF.ee4v.HierarchyExtension.UI.HierarchyItem.Window;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyScene {
    public static class HierarchySceneOverlay {
        public static void Draw() {
            var windowWidth = EditorGUIUtility.currentViewWidth;
            var sceneRect = new Rect(48, 0, windowWidth - 94, 16);
            var hiddenRect = new Rect(sceneRect.xMax, 0, 24, 16);
            var hiddenIconRect = new Rect(hiddenRect.x + 4, 0, 16, 16);
            
            var hiddenIcon = EditorGUIUtility.IconContent("scenevis_hidden_hover").image;
            GUI.DrawTexture(hiddenIconRect, hiddenIcon);
            
            var e = Event.current;
            switch (e.type) {
                case EventType.MouseDown when sceneRect.Contains(e.mousePosition): {
                    var screenSceneRect = new Rect(GUIUtility.GUIToScreenPoint(sceneRect.position), sceneRect.size);
                    if (EditorPrefsManager.EnableSceneSwitcher) SceneSwitcher.Open(screenSceneRect);
                    e.Use();
                    break;
                }
                case EventType.MouseDown when hiddenRect.Contains(e.mousePosition):
                    HiddenObjectList.Open(GUIUtility.GUIToScreenPoint(e.mousePosition));
                    e.Use();
                    break;
            }
            
        }
    }
}