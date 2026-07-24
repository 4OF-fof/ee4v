using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.DepthIndicator
{
    internal static class DepthIndicatorRenderer
    {
        private static readonly Color DarkThemeColor =
            new Color32(104, 104, 104, 255);
        private static readonly Color LightThemeColor =
            new Color32(142, 142, 142, 255);

        public static void Draw(ItemInjectionContext context)
        {
            if (Event.current.type != EventType.Repaint ||
                !(context.Target is GameObject gameObject))
            {
                return;
            }

            var transform = gameObject.transform;
            var parent = transform.parent;
            var cellRect = DepthIndicatorGeometry.GetFirstCell(
                context.SelectionRect);
            var color = EditorGUIUtility.isProSkin
                ? DarkThemeColor
                : LightThemeColor;

            if (transform.childCount == 0 && parent != null)
            {
                DrawRect(
                    DepthIndicatorGeometry.GetLeafLine(cellRect),
                    color);
            }

            if (parent == null)
            {
                return;
            }

            cellRect = DepthIndicatorGeometry.MoveToParentCell(cellRect);
            DrawRect(
                DepthIndicatorGeometry.GetBranchHorizontalLine(cellRect),
                color);
            DrawRect(
                IsLastSibling(transform)
                    ? DepthIndicatorGeometry.GetBranchEndVerticalLine(cellRect)
                    : DepthIndicatorGeometry.GetVerticalLine(cellRect),
                color);

            var ancestor = parent;
            while (ancestor.parent != null)
            {
                cellRect = DepthIndicatorGeometry.MoveToParentCell(cellRect);
                if (!IsLastSibling(ancestor))
                {
                    DrawRect(
                        DepthIndicatorGeometry.GetVerticalLine(cellRect),
                        color);
                }

                ancestor = ancestor.parent;
            }
        }

        private static bool IsLastSibling(Transform transform)
        {
            var parent = transform.parent;
            return parent == null ||
                transform.GetSiblingIndex() == parent.childCount - 1;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            if (rect.width > 0f && rect.height > 0f)
            {
                EditorGUI.DrawRect(rect, color);
            }
        }
    }
}
