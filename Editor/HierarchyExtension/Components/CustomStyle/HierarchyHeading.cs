using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components.CustomStyle {
    public class HierarchyHeading : IHierarchyExtensionComponent {
        public int Priority => -100;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;
            if (!SettingSingleton.I.enableCustomStyleItem) return;

            if (gameObject.GetCustomStyleType() != HierarchyItemType.Heading) return;

            var backRect = new Rect(32, fullRect.y, EditorGUIUtility.currentViewWidth - 32, fullRect.height);
            EditorGUI.DrawRect(backRect, ColorPreset.WindowHeader);

            var labelText = gameObject.name.Replace(SettingSingleton.I.headingPrefix, string.Empty).TrimStart();
            var labelStyle = new GUIStyle(EditorStyles.label) {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUI.LabelField(backRect, labelText, labelStyle);
        }
    }
}