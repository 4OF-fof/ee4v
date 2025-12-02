using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.Wraps;
using _4OF.ee4v.HierarchyExtension.SceneSwitcher;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HierarchySceneHeader : IHierarchyExtensionComponent {
        public int Priority => 0;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject != null) return;
            Draw(fullRect);
        }

        private static void Draw(Rect selectionRect) {
            var windowWidth = EditorGUIUtility.currentViewWidth;
            if (SceneHierarchyWindowWrap.IsScrollbarVisible) windowWidth -= 14;
            var sceneRect = new Rect(48, selectionRect.y, windowWidth - 94, 16);
            var hiddenRect = new Rect(sceneRect.xMax, selectionRect.y, 24, 16);
            var hiddenIconRect = new Rect(hiddenRect.x + 4, selectionRect.y, 16, 16);

            var hiddenIcon = EditorGUIUtility.IconContent("scenevis_hidden_hover").image;
            GUI.DrawTexture(hiddenIconRect, hiddenIcon);

            var e = Event.current;
            if (e.type != EventType.MouseDown) return;
            if (sceneRect.Contains(e.mousePosition)) {
                if (!SettingSingleton.I.enableSceneSwitcher) return;
                var screenSceneRect = new Rect(GUIUtility.GUIToScreenPoint(sceneRect.position), sceneRect.size);
                SceneListService.SceneListRegister();
                SceneSwitcherWindow.Open(screenSceneRect);
                e.Use();
            }
            else if (hiddenRect.Contains(e.mousePosition)) {
                HiddenObjectListWindow.Open(GUIUtility.GUIToScreenPoint(e.mousePosition));
                e.Use();
            }
        }
    }
}