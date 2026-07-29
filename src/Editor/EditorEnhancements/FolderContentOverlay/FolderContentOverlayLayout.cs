using Ee4v.Core.Injector;
using UnityEngine;

namespace Ee4v.FolderContentOverlay
{
    internal static class FolderContentOverlayLayout
    {
        private const float OverlayScale = 0.5f;

        public static Rect GetFolderIconRect(
            Rect itemRect,
            ProjectItemViewMode viewMode,
            ProjectItemOrientation orientation)
        {
            return ProjectItemLayout.GetIconRect(
                itemRect,
                viewMode,
                orientation);
        }

        public static Rect GetOverlayRect(Rect folderIconRect)
        {
            var width = folderIconRect.width * OverlayScale;
            var height = folderIconRect.height * OverlayScale;
            return new Rect(
                folderIconRect.xMax - width,
                folderIconRect.yMax - height,
                width,
                height);
        }
    }
}
