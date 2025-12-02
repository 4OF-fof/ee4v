using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.HierarchyExtension.Components.CustomStyle;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components {
    public class HierarchyDepthLine : IHierarchyExtensionComponent {
        public int Priority => 0;

        public void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect) {
            if (gameObject == null) return;

            if (Settings.I.enableCustomStyleItem && gameObject.IsCustomStyleItem()) return;

            if (!Settings.I.showDepthLine) return;

            Draw(gameObject, fullRect);
        }

        private static void Draw(GameObject obj, Rect depthLineRect) {
            var parent = obj.transform.parent;
            var position = depthLineRect;
            position.x -= 16;
            position.width = 16;

            if (obj.transform.childCount == 0 && parent != null) DrawXLine(position);
            if (parent == null) return;

            position.x -= 14;
            if (parent.childCount == 1 || parent.GetChild(parent.childCount - 1) == obj.transform)
                DrawBranchEnd(position);
            else
                DrawBranch(position);

            while (parent != null) {
                var parentParent = parent.parent;
                if (parentParent == null) break;

                if (parent == parentParent.GetChild(parentParent.childCount - 1)) {
                    parent = parentParent;
                    position.x -= 14;
                    continue;
                }

                position.x -= 14;
                DrawYLine(position);
                parent = parentParent;
            }
        }

        private static void DrawXLine(Rect rect) {
            var xLineRect = rect;
            xLineRect.width -= 4;
            xLineRect.y += rect.height / 2 - 1;
            xLineRect.height = 2;
            EditorGUI.DrawRect(xLineRect, ColorPreset.DepthLine);
        }

        private static void DrawYLine(Rect rect) {
            var yLineRect = rect;
            yLineRect.x += rect.width / 2 - 1;
            yLineRect.width = 2;
            EditorGUI.DrawRect(yLineRect, ColorPreset.DepthLine);
        }

        private static void DrawBranch(Rect rect) {
            var xLineRect = rect;
            xLineRect.x += 8;
            xLineRect.width /= 2;
            xLineRect.y += rect.height / 2 - 1;
            xLineRect.height = 2;
            EditorGUI.DrawRect(xLineRect, ColorPreset.DepthLine);
            var yLineRect = rect;
            yLineRect.x += rect.width / 2 - 1;
            yLineRect.width = 2;
            EditorGUI.DrawRect(yLineRect, ColorPreset.DepthLine);
        }

        private static void DrawBranchEnd(Rect rect) {
            var xLineRect = rect;
            xLineRect.x += 8;
            xLineRect.width /= 2;
            xLineRect.y += rect.height / 2 - 1;
            xLineRect.height = 2;
            EditorGUI.DrawRect(xLineRect, ColorPreset.DepthLine);
            var yLineRect = rect;
            yLineRect.x += rect.width / 2 - 1;
            yLineRect.width = 2;
            yLineRect.height = rect.height / 2 + 1;
            EditorGUI.DrawRect(yLineRect, ColorPreset.DepthLine);
        }
    }
}