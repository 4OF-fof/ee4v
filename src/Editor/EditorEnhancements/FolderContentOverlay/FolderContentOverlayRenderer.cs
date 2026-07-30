using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderContentOverlay
{
    internal sealed class FolderContentOverlayRenderer
    {
        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f)
        };

        private readonly IFolderContentIconProvider _iconProvider;

        public FolderContentOverlayRenderer(
            IFolderContentIconProvider iconProvider)
        {
            _iconProvider = iconProvider ??
                throw new System.ArgumentNullException(nameof(iconProvider));
        }

        public void Draw(ItemInjectionContext context)
        {
            if (Event.current.type != EventType.Repaint ||
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

            var contentIcon = _iconProvider.Get(folderPath);
            if (contentIcon == null)
            {
                return;
            }

            var folderIconRect =
                FolderContentOverlayLayout.GetFolderIconRect(
                    context.SelectionRect,
                    context.ProjectViewMode,
                    context.ProjectOrientation);
            var overlayRect =
                FolderContentOverlayLayout.GetOverlayRect(folderIconRect);
            DrawOutlinedIcon(overlayRect, contentIcon);
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
