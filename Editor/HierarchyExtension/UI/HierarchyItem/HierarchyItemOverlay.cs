using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.HierarchyExtension.Service;
using _4OF.ee4v.HierarchyExtension.UI.HierarchyItem.Window;
using _4OF.ee4v.HierarchyExtension.Utility;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyItem {
    public static class HierarchyItemOverlay {
        public static void Draw(int instanceId, GameObject obj, Rect selectionRect) {
            var hierarchyViewWidth = EditorGUIUtility.currentViewWidth;
            if (ReflectionWrapper.IsHierarchyScrollbarVisible()) hierarchyViewWidth -= 14;
            var nameSize = EditorStyles.label.CalcSize(new GUIContent(obj != null ? obj.name : string.Empty));
            var componentX = selectionRect.x + nameSize.x + 17;
            var componentRect = new Rect(componentX, selectionRect.y, hierarchyViewWidth - (componentX + 32),
                selectionRect.height);
            var menuRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, selectionRect.height);

            if (EditorPrefsManager.EnableCustomStyleItem)
                if (obj != null && EditorPrefsManager.HeadingPrefix != "" &&
                    obj.name.StartsWith(EditorPrefsManager.HeadingPrefix)) {
                    DrawHeading(obj.name, selectionRect);
                    return;
                }

            if (EditorPrefsManager.ShowDepthLine) ItemDepthLine.Draw(obj, selectionRect);

            if (obj == null) return;
            var style = obj.GetComponent<ObjectStyleComponent>();
            if (style != null) {
                ReflectionWrapper.SetItemIcon(instanceId, style.icon as Texture2D);
                BackGroundColor.Draw(obj, selectionRect, style.color, style.icon);
            }

            if (EditorPrefsManager.ShowComponentIcons)
                if (!(EditorPrefsManager.CompatFaceEmo && obj.GetComponent<VRCAvatarDescriptor>() != null))
                    ObjectComponent.Draw(obj, componentRect);

            var e = Event.current;
            if (selectionRect.Contains(e.mousePosition) && e.alt) {
                var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(selectionRect.xMax, selectionRect.y));
                var selectedGameObjectList = Selection.gameObjects;
                if (selectedGameObjectList.Length <= 1)
                    GameObjectInfo.Open(obj, anchorScreen);
                else
                    GameObjectInfo.Open(selectedGameObjectList, anchorScreen);
            }

            if (!EditorPrefsManager.ShowMenuIcon) return;
            if (menuRect.x <= componentRect.x) return;
            var prevColor = GUI.color;
            if (GUI.Button(menuRect, EditorGUIUtility.IconContent("_Menu"), GUIStyle.none)) {
                var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(selectionRect.xMax, selectionRect.y));
                var selectedGameObjectList = Selection.gameObjects;
                if (selectedGameObjectList.Length <= 1)
                    GameObjectInfo.Open(obj, anchorScreen);
                else
                    GameObjectInfo.Open(selectedGameObjectList, anchorScreen);
            }

            GUI.color = prevColor;
        }

        private static void DrawHeading(string name, Rect selectionRect) {
            var backRect = new Rect(32, selectionRect.y, EditorGUIUtility.currentViewWidth - 32, selectionRect.height);
            EditorGUI.DrawRect(backRect, ColorPreset.WindowHeader);
            var labelText = name.Replace(EditorPrefsManager.HeadingPrefix, string.Empty).TrimStart();
            var labelStyle = new GUIStyle(EditorStyles.label) {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUI.LabelField(backRect, labelText, labelStyle);
        }
    }
}