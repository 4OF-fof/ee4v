using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.API;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.ProjectExtension.FolderStyle;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Components {
    public class ProjectStyledFolder : IProjectExtensionComponent {
        public int Priority => 0;

        public void OnGUI(ref Rect currentRect, string guid, Rect fullRect) {
            if (!SettingSingleton.I.enableStyledFolder) return;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return;

            var e = Event.current;
            if (fullRect.Contains(e.mousePosition) && e.alt) {
                var anchorScreen = GUIUtility.GUIToScreenPoint(new Vector2(fullRect.xMax, fullRect.y));
                var selectedPathList = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath)
                    .Where(AssetDatabase.IsValidFolder).ToList();

                if (selectedPathList.Count <= 1)
                    FolderStyleSelectorWindow.Open(path, anchorScreen);
                else
                    FolderStyleSelectorWindow.Open(selectedPathList, anchorScreen);
            }

            var style = FolderStyleList.instance.GetStyle(guid);

            if (style == null) return;
            if (style.color == Color.clear && style.icon == null && string.IsNullOrEmpty(style.assetUlid)) return;

            Draw(path, fullRect, style);
        }

        private void Draw(string path, Rect rect, FolderStyleList.FolderStyle style) {
            var backgroundColor = ColorPreset.ProjectBackground;
            Rect imageRect;

            if (rect.height > 20) {
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.width + 2);
            }
            else if (rect.x > 20) {
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.height + 2, rect.height + 2);
                backgroundColor = ColorPreset.DefaultBackground;
            }
            else {
                imageRect = new Rect(rect.x + 2, rect.y - 1, rect.height + 2, rect.height + 2);
            }

            EditorGUI.DrawRect(imageRect, backgroundColor);

            if (style.icon != null) {
                GUI.DrawTexture(imageRect, style.icon, ScaleMode.ScaleToFit);
                return;
            }

            if (!string.IsNullOrEmpty(style.assetUlid)) {
                var amTexture = AssetManagerAPI.GetAssetThumbnail(style.assetUlid);
                if (amTexture != null) {
                    GUI.DrawTexture(imageRect, amTexture, ScaleMode.ScaleToFit);
                    return;
                }
            }

            if (style.color == Color.clear) return;
            var prevColor = GUI.color;
            GUI.color = style.color;

            var isEmpty = !Directory.EnumerateFileSystemEntries(path).Any();
            var folderIcon = isEmpty
                ? EditorGUIUtility.IconContent("FolderEmpty Icon").image
                : EditorGUIUtility.IconContent("Folder Icon").image;

            GUI.DrawTexture(imageRect, folderIcon, ScaleMode.ScaleToFit);
            GUI.color = prevColor;
        }
    }
}