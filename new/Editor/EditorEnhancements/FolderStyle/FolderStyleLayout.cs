using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderStyle
{
    internal static class FolderStyleLayout
    {
        private const float IconPadding = 1f;
        private const float OneColumnLeftInset = 2f;
        private const float IconHeightScale = 0.95f;

        public static Rect GetIconRect(
            Rect itemRect,
            ProjectItemViewMode viewMode,
            ProjectItemOrientation orientation)
        {
            if (orientation == ProjectItemOrientation.Vertical ||
                itemRect.height >
                EditorGUIUtility.singleLineHeight * 1.5f)
            {
                return ScaleIconHeight(new Rect(
                    itemRect.x - IconPadding,
                    itemRect.y - IconPadding,
                    itemRect.width + IconPadding * 2f,
                    itemRect.width + IconPadding * 2f));
            }

            var x = viewMode == ProjectItemViewMode.OneColumn
                ? itemRect.x + OneColumnLeftInset
                : itemRect.x - IconPadding;
            var size = itemRect.height + IconPadding * 2f;
            return ScaleIconHeight(new Rect(
                x,
                itemRect.y - IconPadding,
                size,
                size));
        }

        private static Rect ScaleIconHeight(Rect iconRect)
        {
            iconRect.height *= IconHeightScale;
            return iconRect;
        }
    }
}
