using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Components {
    public class ProjectContentOverlay : IProjectExtensionComponent {
        public int Priority => 10;

        public void OnGUI(ref Rect currentRect, string guid, Rect fullRect) {
            if (!SettingSingleton.I.showFolderOverlayIcon) return;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return;

            Draw(path, fullRect);
        }

        private static void Draw(string path, Rect rect) {
            Rect imageRect;
            if (rect.height > 20)
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.width + 2);
            else if (rect.x > 20)
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.height + 2, rect.height + 2);
            else
                imageRect = new Rect(rect.x + 2, rect.y - 1, rect.height + 2, rect.height + 2);

            imageRect.height -= imageRect.height * 0.05f;

            var overlayRect = new Rect(
                (imageRect.x + imageRect.xMax) / 2,
                (imageRect.y + imageRect.yMax) / 2,
                imageRect.width / 2,
                imageRect.height / 2
            );

            var overlayIcon = FolderContentService.GetMostIconInFolder(path);
            if (overlayIcon == null) return;

            var prevColor = GUI.color;
            GUI.color = ColorPreset.IconBorder;
            var outlineOffsets = new[]
                { new Vector2(-1f, 0), new Vector2(1f, 0), new Vector2(0, -1f), new Vector2(0, 1f) };

            foreach (var off in outlineOffsets) {
                var r = new Rect(overlayRect.x + off.x, overlayRect.y + off.y, overlayRect.width, overlayRect.height);
                GUI.DrawTexture(r, overlayIcon, ScaleMode.ScaleToFit);
            }

            GUI.color = prevColor;
            GUI.DrawTexture(overlayRect, overlayIcon, ScaleMode.ScaleToFit);
        }
    }
}