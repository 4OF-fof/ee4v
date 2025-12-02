using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.Wraps;
using _4OF.ee4v.HierarchyExtension.SceneSwitcher;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class SceneSwitcher : IHierarchyExtensionComponent {
        public int Priority => 0;
        public string Name => "Scene Switcher";
        public string Description => I18N.Get("_System.HierarchyExtension.SceneSwitcher.Description");
        public string Trigger => I18N.Get("_System.HierarchyExtension.SceneSwitcher.Trigger");

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject != null) return;
            if (!SettingSingleton.I.enableSceneSwitcher) return;
            DrawSwitcherArea(fullRect);
        }

        private static void DrawSwitcherArea(Rect selectionRect) {
            var windowWidth = EditorGUIUtility.currentViewWidth;
            if (SceneHierarchyWindowWrap.LastInteractedWindow?.IsScrollbarVisible ?? false) windowWidth -= 14;
            var sceneRect = new Rect(48, selectionRect.y, windowWidth - 94, 16);

            var e = Event.current;
            if (e.type != EventType.MouseDown || !sceneRect.Contains(e.mousePosition)) return;
            var screenSceneRect = new Rect(GUIUtility.GUIToScreenPoint(sceneRect.position), sceneRect.size);

            SceneListService.SceneListRegister();
            SceneSwitcherWindow.Open(screenSceneRect);
            e.Use();
        }
    }
}