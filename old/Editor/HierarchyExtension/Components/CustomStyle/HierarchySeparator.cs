using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components.CustomStyle {
    public class HierarchySeparator : IHierarchyExtensionComponent {
        public int Priority => -100;
        public string Name => "CustomStyle: Separator";
        public string Description => I18N.Get("_System.HierarchyExtension.Separator.Description");
        public string Trigger => I18N.Get("_System.HierarchyExtension.Separator.Trigger");

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;
            if (!SettingSingleton.I.enableCustomStyleItem) return;

            if (gameObject.GetCustomStyleType() != HierarchyItemType.Separator) return;

            var backRect = new Rect(32, fullRect.y, EditorGUIUtility.currentViewWidth - 32, fullRect.height);
            EditorGUI.DrawRect(backRect, ColorPreset.DefaultBackground);

            var labelText = gameObject.name.Replace(SettingSingleton.I.separatorPrefix, string.Empty).TrimStart();
            var labelStyle = new GUIStyle(EditorStyles.label) {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorPreset.InactiveItem }
            };
            EditorGUI.LabelField(backRect, labelText, labelStyle);

            if (!string.IsNullOrEmpty(labelText)) {
                var content = new GUIContent(labelText);
                var size = labelStyle.CalcSize(content);
                var centerY = backRect.y + backRect.height / 2f;
                var lineColor = ColorPreset.InactiveItem;
                const float padding = 6f;

                var leftLineWidth = backRect.width / 2f - size.x / 2f - padding;
                if (leftLineWidth > 0)
                    EditorGUI.DrawRect(new Rect(backRect.x, centerY, leftLineWidth, 1), lineColor);

                var rightLineWidth = backRect.width / 2f - size.x / 2f - padding;
                if (rightLineWidth > 0) {
                    var rightLineX = backRect.x + backRect.width / 2f + size.x / 2f + padding;
                    EditorGUI.DrawRect(new Rect(rightLineX, centerY, rightLineWidth, 1), lineColor);
                }
            }
            else {
                var centerY = backRect.y + backRect.height / 2f;
                EditorGUI.DrawRect(new Rect(backRect.x, centerY, backRect.width, 1), ColorPreset.InactiveItem);
            }
        }
    }
}