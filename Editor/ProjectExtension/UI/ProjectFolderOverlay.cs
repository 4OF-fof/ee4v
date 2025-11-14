using System.IO;
using System.Linq;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using _4OF.ee4v.ProjectExtension.Data;
using _4OF.ee4v.ProjectExtension.Service;
using _4OF.ee4v.ProjectExtension.UI.Window;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.UI {
    public static class ProjectFolderOverlay {
        public static void Draw(string path, Rect selectionRect) {
            var e = Event.current;
            if (EditorPrefsManager.EnableStyledFolder && selectionRect.Contains(e.mousePosition) && e.alt) {
                var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(selectionRect.xMax, selectionRect.y));
                var selectedPathList = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath)
                    .Where(AssetDatabase.IsValidFolder).ToList();
                if (selectedPathList.Count <= 1)
                    FolderStyleSelector.Open(path, anchorScreen);
                else
                    FolderStyleSelector.Open(selectedPathList, anchorScreen);
            }

            var backgroundColor = ColorPreset.ProjectBackground;
            Rect imageRect;
            if (selectionRect.height > 20) {
                imageRect = new Rect(selectionRect.x - 1, selectionRect.y - 1, selectionRect.width + 2,
                    selectionRect.width + 2);
            }
            else if (selectionRect.x > 20) {
                imageRect = new Rect(selectionRect.x - 1, selectionRect.y - 1, selectionRect.height + 2,
                    selectionRect.height + 2);
                backgroundColor = ColorPreset.DefaultBackground;
            }
            else {
                imageRect = new Rect(selectionRect.x + 2, selectionRect.y - 1, selectionRect.height + 2,
                    selectionRect.height + 2);
            }

            var isDrawIcon = false;
            if (EditorPrefsManager.EnableStyledFolder) isDrawIcon = DrawStyledFolder(path, imageRect, backgroundColor);

            if (!EditorPrefsManager.ShowFolderOverlayIcon || isDrawIcon) return;
            imageRect.height -= imageRect.height * 0.05f;
            DrawOverlayIcon(path, imageRect);
        }

        private static bool DrawStyledFolder(string path, Rect imageRect, Color backgroundColor) {
            var style = FolderStyleList.instance.Contents.FirstOrDefault(s =>
                s.path == FileUtility.NormalizePath(path));
            var color = style?.color ?? Color.clear;
            var icon = style?.icon;
            if (color == Color.clear && icon == null) return false;
            EditorGUI.DrawRect(imageRect, backgroundColor);
            if (icon != null) {
                GUI.DrawTexture(imageRect, icon, ScaleMode.ScaleToFit);
                return true;
            }

            if (color == Color.clear) return false;
            var prevColor = GUI.color;
            GUI.color = color;
            var isEmpty = !Directory.EnumerateFileSystemEntries(path).Any();
            var folderIcon = isEmpty
                ? EditorGUIUtility.IconContent("FolderEmpty Icon").image
                : EditorGUIUtility.IconContent("Folder Icon").image;
            GUI.DrawTexture(imageRect, folderIcon, ScaleMode.ScaleToFit);
            GUI.color = prevColor;
            return false;
        }

        private static void DrawOverlayIcon(string path, Rect imageRect) {
            var overlayRect = new Rect((imageRect.x + imageRect.xMax) / 2, (imageRect.y + imageRect.yMax) / 2,
                imageRect.width / 2, imageRect.height / 2);
            var overlayIcon = GetFolderContent.GetMostIconInFolder(path);
            if (overlayIcon == null) return;
            var outlineOffsetsOuter = new[]
                { new Vector2(-1f, 0), new Vector2(1f, 0), new Vector2(0, -1f), new Vector2(0, 1f) };

            var prevColor = GUI.color;
            GUI.color = ColorPreset.IconBorder;
            foreach (var off in outlineOffsetsOuter) {
                var r = new Rect(overlayRect.x + off.x, overlayRect.y + off.y, overlayRect.width, overlayRect.height);
                GUI.DrawTexture(r, overlayIcon, ScaleMode.ScaleToFit);
            }

            GUI.color = prevColor;
            GUI.DrawTexture(overlayRect, overlayIcon, ScaleMode.ScaleToFit);
        }
    }
}