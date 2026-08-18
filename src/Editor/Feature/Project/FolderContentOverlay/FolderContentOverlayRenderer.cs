using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderContentOverlay
{
    internal static class FolderContentOverlayRenderer
    {
        private const float OverlayScale = 0.5f;
        private const float IconPadding = 1f;
        private const float OneColumnLeftInset = 2f;
        private const float IconHeightScale = 0.95f;

        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f)
        };

        public static void Draw(
            ItemInjectionContext context,
            FolderContentOverlayIconCache cache)
        {
            if (context == null ||
                Event.current == null ||
                Event.current.type != EventType.Repaint ||
                string.IsNullOrEmpty(context.Guid) ||
                context.SuppressProjectItemIconOverlay)
            {
                return;
            }

            var folderPath = AssetDatabase.GUIDToAssetPath(context.Guid);
            if (string.IsNullOrEmpty(folderPath) ||
                !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var contentIcon = cache.Get(folderPath);
            if (contentIcon == null)
            {
                return;
            }

            var folderIconRect = GetFolderIconRect(
                context.SelectionRect,
                context.ProjectViewMode,
                context.ProjectOrientation);
            DrawOutlinedIcon(
                GetOverlayRect(folderIconRect),
                contentIcon);
        }

        private static Rect GetFolderIconRect(
            Rect itemRect,
            ProjectItemViewMode viewMode,
            ProjectItemOrientation orientation)
        {
            Rect iconRect;
            if (orientation == ProjectItemOrientation.Vertical ||
                itemRect.height >
                EditorGUIUtility.singleLineHeight * 1.5f)
            {
                iconRect = new Rect(
                    itemRect.x - IconPadding,
                    itemRect.y - IconPadding,
                    itemRect.width + IconPadding * 2f,
                    itemRect.width + IconPadding * 2f);
            }
            else
            {
                var x = viewMode == ProjectItemViewMode.OneColumn
                    ? itemRect.x + OneColumnLeftInset
                    : itemRect.x - IconPadding;
                var size = itemRect.height + IconPadding * 2f;
                iconRect = new Rect(
                    x,
                    itemRect.y - IconPadding,
                    size,
                    size);
            }

            iconRect.height *= IconHeightScale;
            return iconRect;
        }

        private static Rect GetOverlayRect(Rect folderIconRect)
        {
            var width = folderIconRect.width * OverlayScale;
            var height = folderIconRect.height * OverlayScale;
            return new Rect(
                folderIconRect.xMax - width,
                folderIconRect.yMax - height,
                width,
                height);
        }

        private static void DrawOutlinedIcon(Rect rect, Texture icon)
        {
            var previousColor = GUI.color;
            GUI.color = Color.black;
            for (var i = 0; i < OutlineOffsets.Length; i++)
            {
                var offset = OutlineOffsets[i];
                GUI.DrawTexture(
                    new Rect(
                        rect.x + offset.x,
                        rect.y + offset.y,
                        rect.width,
                        rect.height),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
            }

            GUI.color = previousColor;
            GUI.DrawTexture(
                rect,
                icon,
                ScaleMode.ScaleToFit,
                true);
        }
    }
}
