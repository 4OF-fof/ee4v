using System.Collections.Generic;
using System.IO;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.ItemStyle;
using UnityEditor;
using UnityEngine;

namespace Ee4v.ProjectStyle
{
    internal static class ProjectStyleWindow
    {
        private const float ColorAlpha = 0.7f;

        private static readonly Color[] DarkColors =
        {
            new Color(0.7f, 0f, 0f, ColorAlpha),
            new Color(0.7f, 0.35f, 0f, ColorAlpha),
            new Color(0.7f, 0.7f, 0f, ColorAlpha),
            new Color(0.35f, 0.7f, 0f, ColorAlpha),
            new Color(0f, 0.7f, 0f, ColorAlpha),
            new Color(0f, 0.7f, 0.35f, ColorAlpha),
            new Color(0f, 0.7f, 0.7f, ColorAlpha),
            new Color(0f, 0.35f, 0.7f, ColorAlpha),
            new Color(0f, 0f, 0.7f, ColorAlpha),
            new Color(0.35f, 0f, 0.7f, ColorAlpha),
            new Color(0.7f, 0f, 0.7f, ColorAlpha),
            new Color(0.7f, 0f, 0.35f, ColorAlpha)
        };

        private static readonly Color[] LightColors =
        {
            new Color(1f, 0.2f, 0.2f, ColorAlpha),
            new Color(1f, 0.55f, 0.2f, ColorAlpha),
            new Color(1f, 1f, 0.2f, ColorAlpha),
            new Color(0.55f, 1f, 0.2f, ColorAlpha),
            new Color(0.2f, 1f, 0.2f, ColorAlpha),
            new Color(0.2f, 1f, 0.55f, ColorAlpha),
            new Color(0.2f, 1f, 1f, ColorAlpha),
            new Color(0.2f, 0.55f, 1f, ColorAlpha),
            new Color(0.2f, 0.2f, 1f, ColorAlpha),
            new Color(0.55f, 0.2f, 1f, ColorAlpha),
            new Color(1f, 0.2f, 1f, ColorAlpha),
            new Color(1f, 0.2f, 0.55f, ColorAlpha)
        };

        public static void ShowAt(
            IReadOnlyList<string> folderGuids,
            Vector2 screenPosition,
            ItemStyleService service)
        {
            if (folderGuids == null ||
                folderGuids.Count == 0 ||
                service == null)
            {
                return;
            }

            var request = new ItemStyleWindowRequest(
                service,
                folderGuids,
                screenPosition,
                CreateTitle(folderGuids),
                CreateTargetTooltip(folderGuids),
                GetColors(),
                typeof(Texture),
                EditorGUIUtility.IconContent("Folder Icon").image,
                () => InjectorApi.Repaint(
                    InjectionChannel.ProjectItem))
            {
                CloseTooltip = I18N.Get("window.closeTooltip"),
                ColorLabel = I18N.Get("editor.color.label"),
                ColorTooltip = I18N.Get("editor.color.tooltip"),
                CustomColorLabel = I18N.Get(
                    "editor.color.customLabel"),
                ClearColorLabel = I18N.Get(
                    "editor.color.clearLabel"),
                IconLabel = I18N.Get("editor.icon.label"),
                IconTooltip = I18N.Get("editor.icon.tooltip"),
                ChooseIconLabel = I18N.Get(
                    "editor.icon.chooseLabel"),
                ClearIconLabel = I18N.Get(
                    "editor.icon.clearLabel"),
                RecentIconsLabel = I18N.Get(
                    "editor.icon.recentLabel")
            };
            ItemStyleWindow.ShowAt(request);
        }

        private static string CreateTitle(
            IReadOnlyList<string> folderGuids)
        {
            if (folderGuids.Count > 1)
            {
                return I18N.Get(
                    "window.multipleTitle",
                    folderGuids.Count);
            }

            var path = AssetDatabase.GUIDToAssetPath(folderGuids[0]);
            var name = string.IsNullOrEmpty(path)
                ? I18N.Get("window.unknownFolder")
                : Path.GetFileName(path);
            return I18N.Get("window.singleTitle", name);
        }

        private static IReadOnlyList<Color> GetColors()
        {
            return EditorGUIUtility.isProSkin
                ? DarkColors
                : LightColors;
        }

        private static string CreateTargetTooltip(
            IReadOnlyList<string> folderGuids)
        {
            var paths = new List<string>();
            for (var i = 0; i < folderGuids.Count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(
                    folderGuids[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            return string.Join("\n", paths);
        }
    }
}
