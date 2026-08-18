using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.DepthIndicator
{
    internal static class DepthIndicatorRenderer
    {
        private const float IndentWidth = 14f;
        private const float FirstCellOffset = 16f;
        private const float CellWidth = 16f;
        private const float LineWidth = 2f;

        private static readonly Color32 DarkLine =
            new Color32(104, 104, 104, 255);
        private static readonly Color32 LightLine =
            new Color32(142, 142, 142, 255);

        public static void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                !context.IsHierarchyGameObject ||
                !(context.Target is GameObject gameObject) ||
                Event.current == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            var transform = gameObject.transform;
            var parent = transform.parent;
            var cellRect = GetFirstCell(context.SelectionRect);
            var color = EditorGUIUtility.isProSkin
                ? DarkLine
                : LightLine;

            if (!HasVisibleChild(transform) && parent != null)
            {
                DrawRect(GetLeafLine(cellRect), color);
            }

            if (parent == null)
            {
                return;
            }

            cellRect = MoveToParentCell(cellRect);
            DrawRect(GetBranchHorizontalLine(cellRect), color);
            DrawRect(
                IsLastVisibleSibling(transform)
                    ? GetBranchEndVerticalLine(cellRect)
                    : GetVerticalLine(cellRect),
                color);

            var ancestor = parent;
            while (ancestor.parent != null)
            {
                cellRect = MoveToParentCell(cellRect);
                if (!IsLastVisibleSibling(ancestor))
                {
                    DrawRect(GetVerticalLine(cellRect), color);
                }

                ancestor = ancestor.parent;
            }
        }

        internal static bool HasVisibleChild(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                if (!IsHidden(transform.GetChild(i).gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsLastVisibleSibling(Transform transform)
        {
            if (transform == null || transform.parent == null)
            {
                return true;
            }

            var parent = transform.parent;
            for (var i = transform.GetSiblingIndex() + 1;
                 i < parent.childCount;
                 i++)
            {
                if (!IsHidden(parent.GetChild(i).gameObject))
                {
                    return false;
                }
            }

            return true;
        }

        private static Rect GetFirstCell(Rect itemRect)
        {
            return new Rect(
                itemRect.x - FirstCellOffset,
                itemRect.y,
                CellWidth,
                itemRect.height);
        }

        private static Rect MoveToParentCell(Rect cellRect)
        {
            cellRect.x -= IndentWidth;
            return cellRect;
        }

        private static Rect GetLeafLine(Rect cellRect)
        {
            return new Rect(
                cellRect.x,
                GetCenterLineY(cellRect),
                Mathf.Max(0f, cellRect.width - 4f),
                LineWidth);
        }

        private static Rect GetBranchHorizontalLine(Rect cellRect)
        {
            return new Rect(
                cellRect.x + cellRect.width * 0.5f,
                GetCenterLineY(cellRect),
                cellRect.width * 0.5f,
                LineWidth);
        }

        private static Rect GetVerticalLine(Rect cellRect)
        {
            return new Rect(
                cellRect.x + cellRect.width * 0.5f - LineWidth * 0.5f,
                cellRect.y,
                LineWidth,
                cellRect.height);
        }

        private static Rect GetBranchEndVerticalLine(Rect cellRect)
        {
            var centerY = cellRect.y + cellRect.height * 0.5f;
            return new Rect(
                cellRect.x + cellRect.width * 0.5f - LineWidth * 0.5f,
                cellRect.y,
                LineWidth,
                Mathf.Max(
                    0f,
                    centerY - cellRect.y + LineWidth * 0.5f));
        }

        private static float GetCenterLineY(Rect cellRect)
        {
            return cellRect.y +
                cellRect.height * 0.5f -
                LineWidth * 0.5f;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            if (rect.width > 0f && rect.height > 0f)
            {
                EditorGUI.DrawRect(rect, color);
            }
        }

        private static bool IsHidden(GameObject gameObject)
        {
            return gameObject == null ||
                (gameObject.hideFlags & HideFlags.HideInHierarchy) != 0;
        }
    }
}
