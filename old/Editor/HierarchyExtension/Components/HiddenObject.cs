using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Wraps;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HiddenObject : IHierarchyExtensionComponent {
        public int Priority => 0;
        public string Name => "Hidden Object";
        public string Description => I18N.Get("_System.HierarchyExtension.HiddenObject.Description");
        public string Trigger => I18N.Get("_System.HierarchyExtension.HiddenObject.Trigger");

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject != null) return;

            DrawHiddenObjectButton(fullRect);
        }

        private static void DrawHiddenObjectButton(Rect selectionRect) {
            var windowWidth = EditorGUIUtility.currentViewWidth;
            if (SceneHierarchyWindowWrap.LastInteractedWindow?.IsScrollbarVisible ?? false) windowWidth -= 14;

            var sceneRect = new Rect(48, selectionRect.y, windowWidth - 94, 16);
            var hiddenRect = new Rect(sceneRect.xMax, selectionRect.y, 24, 16);
            var hiddenIconRect = new Rect(hiddenRect.x + 4, selectionRect.y, 16, 16);

            var hiddenIcon = EditorGUIUtility.IconContent("scenevis_hidden_hover").image;
            GUI.DrawTexture(hiddenIconRect, hiddenIcon);

            var e = Event.current;
            if (e.type != EventType.MouseDown || !hiddenRect.Contains(e.mousePosition)) return;
            HiddenObjectListWindow.Open(GUIUtility.GUIToScreenPoint(e.mousePosition));
            e.Use();
        }
    }
}