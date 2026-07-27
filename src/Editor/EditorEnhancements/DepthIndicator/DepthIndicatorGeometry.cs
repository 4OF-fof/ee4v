using UnityEngine;

namespace Ee4v.DepthIndicator
{
    internal static class DepthIndicatorGeometry
    {
        internal const float IndentWidth = 14f;
        internal const float FirstCellOffset = 16f;
        internal const float CellWidth = 16f;
        internal const float LineWidth = 2f;

        public static Rect GetFirstCell(Rect itemRect)
        {
            return new Rect(
                itemRect.x - FirstCellOffset,
                itemRect.y,
                CellWidth,
                itemRect.height);
        }

        public static Rect MoveToParentCell(Rect cellRect)
        {
            cellRect.x -= IndentWidth;
            return cellRect;
        }

        public static Rect GetLeafLine(Rect cellRect)
        {
            return new Rect(
                cellRect.x,
                GetCenterLineY(cellRect),
                Mathf.Max(0f, cellRect.width - 4f),
                LineWidth);
        }

        public static Rect GetBranchHorizontalLine(Rect cellRect)
        {
            return new Rect(
                cellRect.x + cellRect.width * 0.5f,
                GetCenterLineY(cellRect),
                cellRect.width * 0.5f,
                LineWidth);
        }

        public static Rect GetVerticalLine(Rect cellRect)
        {
            return new Rect(
                cellRect.x + cellRect.width * 0.5f - LineWidth * 0.5f,
                cellRect.y,
                LineWidth,
                cellRect.height);
        }

        public static Rect GetBranchEndVerticalLine(Rect cellRect)
        {
            var centerY = cellRect.y + cellRect.height * 0.5f;
            return new Rect(
                cellRect.x + cellRect.width * 0.5f - LineWidth * 0.5f,
                cellRect.y,
                LineWidth,
                Mathf.Max(0f, centerY - cellRect.y + LineWidth * 0.5f));
        }

        private static float GetCenterLineY(Rect cellRect)
        {
            return cellRect.y +
                cellRect.height * 0.5f -
                LineWidth * 0.5f;
        }
    }
}
