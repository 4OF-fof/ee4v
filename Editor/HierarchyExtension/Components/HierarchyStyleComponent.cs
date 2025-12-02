using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using _4OF.ee4v.HierarchyExtension.ItemStyle;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HierarchyStyleComponent : IHierarchyExtensionComponent {
        public int Priority => -100;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;
            if (!Settings.I.enableCustomStyleItem) return;

            if (!string.IsNullOrEmpty(Settings.I.headingPrefix) &&
                gameObject.name.StartsWith(Settings.I.headingPrefix)) {
                DrawHeading(gameObject.name, fullRect);
                return;
            }

            if (!string.IsNullOrEmpty(Settings.I.separatorPrefix) &&
                gameObject.name.StartsWith(Settings.I.separatorPrefix)) {
                DrawSeparator(gameObject.name, fullRect);
                return;
            }

            var (style, isSelf) = GetEffectiveStyle(gameObject);
            if (style == null) return;
            if (isSelf) {
                ReflectionWrapper.SetItemIcon(instanceID, style.icon as Texture2D);
                BackGroundColor.Draw(gameObject, fullRect, style.color, style.icon);
            }
            else {
                BackGroundColor.Draw(gameObject, fullRect, style.color);
            }
        }

        private (ObjectStyleComponent, bool) GetEffectiveStyle(GameObject obj) {
            var current = obj.transform;
            while (current != null) {
                var style = current.GetComponent<ObjectStyleComponent>();
                if (style != null && (style.color != Color.clear || style.icon != null))
                    return (style, current.gameObject == obj);
                current = current.parent;
            }

            return (null, false);
        }

        private void DrawHeading(string name, Rect rect) {
            var backRect = new Rect(32, rect.y, EditorGUIUtility.currentViewWidth - 32, rect.height);
            EditorGUI.DrawRect(backRect, ColorPreset.WindowHeader);

            var labelText = name.Replace(Settings.I.headingPrefix, string.Empty).TrimStart();
            var labelStyle = new GUIStyle(EditorStyles.label) {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUI.LabelField(backRect, labelText, labelStyle);
        }

        private void DrawSeparator(string name, Rect rect) {
            var backRect = new Rect(32, rect.y, EditorGUIUtility.currentViewWidth - 32, rect.height);
            EditorGUI.DrawRect(backRect, ColorPreset.DefaultBackground);

            var labelText = name.Replace(Settings.I.separatorPrefix, string.Empty).TrimStart();
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
                if (leftLineWidth > 0) EditorGUI.DrawRect(new Rect(backRect.x, centerY, leftLineWidth, 1), lineColor);

                var rightLineWidth = backRect.width / 2f - size.x / 2f - padding;
                if (!(rightLineWidth > 0)) return;
                var rightLineX = backRect.x + backRect.width / 2f + size.x / 2f + padding;
                EditorGUI.DrawRect(new Rect(rightLineX, centerY, rightLineWidth, 1), lineColor);
            }
            else {
                EditorGUI.DrawRect(new Rect(backRect.x, centerY, backRect.width, 1), lineColor);
            }
        }
    }
}