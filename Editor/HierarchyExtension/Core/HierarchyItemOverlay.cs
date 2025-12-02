using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using _4OF.ee4v.HierarchyExtension.ItemStyle;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace _4OF.ee4v.HierarchyExtension.Core {
    public static class HierarchyItemOverlay {
        public static void Draw(int instanceId, GameObject obj, Rect selectionRect) {
            var hierarchyViewWidth = EditorGUIUtility.currentViewWidth;
            if (ReflectionWrapper.IsHierarchyScrollbarVisible()) hierarchyViewWidth -= 14;
            var nameSize = EditorStyles.label.CalcSize(new GUIContent(obj != null ? obj.name : string.Empty));
            var componentX = selectionRect.x + nameSize.x + 17;
            var componentRect = new Rect(componentX, selectionRect.y, hierarchyViewWidth - (componentX + 32),
                selectionRect.height);
            var menuRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, selectionRect.height);

            if (EditorPrefsManager.EnableCustomStyleItem) {
                if (obj != null && EditorPrefsManager.HeadingPrefix != "" &&
                    obj.name.StartsWith(EditorPrefsManager.HeadingPrefix)) {
                    DrawHeading(obj.name, selectionRect);
                    return;
                }

                if (obj != null && EditorPrefsManager.SeparatorPrefix != "" &&
                    obj.name.StartsWith(EditorPrefsManager.SeparatorPrefix)) {
                    DrawSeparator(obj.name, selectionRect);
                    return;
                }
            }

            if (EditorPrefsManager.ShowDepthLine) ItemDepthLine.Draw(obj, selectionRect);

            if (obj == null) return;
            var (style, isSelf) = GetEffectiveStyle(obj);

            if (style != null) {
                if (isSelf) {
                    ReflectionWrapper.SetItemIcon(instanceId, style.icon as Texture2D);
                    BackGroundColor.Draw(obj, selectionRect, style.color, style.icon);
                }
                else {
                    BackGroundColor.Draw(obj, selectionRect, style.color);
                }
            }

            if (EditorPrefsManager.ShowComponentIcons)
                if (!(EditorPrefsManager.CompatFaceEmo && obj.GetComponent<VRCAvatarDescriptor>() != null))
                    ObjectComponent.Draw(obj, componentRect);

            var e = Event.current;
            if (selectionRect.Contains(e.mousePosition) && e.alt) {
                var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(selectionRect.xMax, selectionRect.y));
                var selectedGameObjectList = Selection.gameObjects;
                if (selectedGameObjectList.Length <= 1)
                    GameObjectInfoWindow.Open(obj, anchorScreen);
                else
                    GameObjectInfoWindow.Open(selectedGameObjectList, anchorScreen);
            }

            if (!EditorPrefsManager.ShowMenuIcon) return;
            if (menuRect.x <= componentRect.x) return;
            var prevColor = GUI.color;
            if (GUI.Button(menuRect, EditorGUIUtility.IconContent("_Menu"), GUIStyle.none)) {
                var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(selectionRect.xMax, selectionRect.y));
                var selectedGameObjectList = Selection.gameObjects;
                if (selectedGameObjectList.Length <= 1)
                    GameObjectInfoWindow.Open(obj, anchorScreen);
                else
                    GameObjectInfoWindow.Open(selectedGameObjectList, anchorScreen);
            }

            GUI.color = prevColor;
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

        private static void DrawSeparator(string name, Rect selectionRect) {
            var backRect = new Rect(32, selectionRect.y, EditorGUIUtility.currentViewWidth - 32, selectionRect.height);
            EditorGUI.DrawRect(backRect, ColorPreset.DefaultBackground);

            var labelText = name.Replace(EditorPrefsManager.SeparatorPrefix, string.Empty).TrimStart();
            var labelStyle = new GUIStyle(EditorStyles.label) {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorPreset.InActiveItem }
            };
            EditorGUI.LabelField(backRect, labelText, labelStyle);

            var content = new GUIContent(labelText);
            var size = labelStyle.CalcSize(content);
            var lineColor = ColorPreset.InActiveItem;
            var centerY = backRect.y + backRect.height / 2f;
            const float padding = 6f;

            if (!string.IsNullOrEmpty(labelText)) {
                var leftLineWidth = backRect.width / 2f - size.x / 2f - padding;
                if (leftLineWidth > 0) {
                    var leftLineRect = new Rect(backRect.x, centerY, leftLineWidth, 1);
                    EditorGUI.DrawRect(leftLineRect, lineColor);
                }

                var rightLineWidth = backRect.width / 2f - size.x / 2f - padding;
                if (!(rightLineWidth > 0)) return;
                var rightLineX = backRect.x + backRect.width / 2f + size.x / 2f + padding;
                var rightLineRect = new Rect(rightLineX, centerY, rightLineWidth, 1);
                EditorGUI.DrawRect(rightLineRect, lineColor);
            }
            else {
                var lineRect = new Rect(backRect.x, centerY, backRect.width, 1);
                EditorGUI.DrawRect(lineRect, lineColor);
            }
        }
    }
}