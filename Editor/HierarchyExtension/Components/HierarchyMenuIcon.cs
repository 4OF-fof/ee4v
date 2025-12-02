using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.HierarchyExtension.Components.CustomStyle;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HierarchyMenuIcon : IHierarchyExtensionComponent {
        public int Priority => 90;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;

            if (SettingSingleton.I.enableCustomStyleItem && gameObject.IsCustomStyleItem()) return;

            if (!SettingSingleton.I.showMenuIcon) return;

            var nameSize = EditorStyles.label.CalcSize(new GUIContent(gameObject.name));
            var componentX = fullRect.x + nameSize.x + 17;
            if (currentRect.xMax - 16 <= componentX) return;

            currentRect.xMax -= 16;
            var menuRect = new Rect(currentRect.xMax, fullRect.y, 16, fullRect.height);

            var prevColor = GUI.color;
            if (GUI.Button(menuRect, EditorGUIUtility.IconContent("_Menu"), GUIStyle.none)) {
                var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(fullRect.xMax, fullRect.y));
                var selectedGameObjectList = Selection.gameObjects;

                if (selectedGameObjectList.Length <= 1)
                    GameObjectWindow.GameObjectWindow.Open(gameObject, anchorScreen);
                else
                    GameObjectWindow.GameObjectWindow.Open(selectedGameObjectList, anchorScreen);
            }

            GUI.color = prevColor;

            currentRect.xMax -= 2;
        }
    }
}