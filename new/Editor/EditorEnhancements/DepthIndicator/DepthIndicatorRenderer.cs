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

            if (!DepthIndicatorHierarchy.HasVisibleChild(
                    transform) &&
                parent != null)
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
                DepthIndicatorHierarchy.IsLastVisibleSibling(
                    transform)
                    ? DepthIndicatorGeometry.GetBranchEndVerticalLine(cellRect)
                    : DepthIndicatorGeometry.GetVerticalLine(cellRect),
                color);

            var ancestor = parent;
            while (ancestor.parent != null)
            {
                cellRect = DepthIndicatorGeometry.MoveToParentCell(cellRect);
                if (!DepthIndicatorHierarchy
                        .IsLastVisibleSibling(ancestor))
                {
                    DrawRect(
                        DepthIndicatorGeometry.GetVerticalLine(cellRect),
                        color);
                }

                ancestor = ancestor.parent;
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            if (rect.width > 0f && rect.height > 0f)
            {
                EditorGUI.DrawRect(rect, color);
            }
        }
    }

    internal static class DepthIndicatorHierarchy
    {
        public static bool HasVisibleChild(
            Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            for (var i = 0;
                 i < transform.childCount;
                 i++)
            {
                if (!IsHidden(
                        transform.GetChild(i).gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLastVisibleSibling(
            Transform transform)
        {
            if (transform == null ||
                transform.parent == null)
            {
                return true;
            }

            var parent = transform.parent;
            for (var i = transform.GetSiblingIndex() + 1;
                 i < parent.childCount;
                 i++)
            {
                if (!IsHidden(
                        parent.GetChild(i).gameObject))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsHidden(
            GameObject gameObject)
        {
            return gameObject == null ||
                (gameObject.hideFlags &
                 HideFlags.HideInHierarchy) != 0;
        }
    }
}
