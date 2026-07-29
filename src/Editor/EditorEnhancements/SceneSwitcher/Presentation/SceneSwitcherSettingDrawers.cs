using System;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.SceneSwitcher
{
    internal static class SceneSwitcherSettingDrawers
    {
        public static void Register()
        {
            SettingDrawerRegistry.Register(
                SceneSwitcherDefinitions.CreateFolder,
                CreateFolderField);
        }

        internal static VisualElement CreateFolderField(
            SettingDrawerContext<string> context)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.minWidth = 0f;

            var field = new TextField(string.Empty)
            {
                tooltip = context.Tooltip,
                value = context.Value ?? string.Empty
            };
            field.style.flexGrow = 1f;
            field.style.minWidth = 0f;
            field.RegisterValueChangedCallback(evt =>
                context.NotifyValueChanged(evt.newValue ?? string.Empty));
            root.Add(field);

            var browse = new Button(() =>
            {
                var initialFolder =
                    SceneSwitcherPolicy.NormalizeAssetFolder(field.value);
                var absoluteInitial = ToAbsolutePath(
                    string.IsNullOrEmpty(initialFolder)
                        ? "Assets"
                        : initialFolder);
                var selected = EditorUtility.OpenFolderPanel(
                    I18N.Get("settings.createFolder.dialogTitle"),
                    absoluteInitial,
                    string.Empty);
                if (string.IsNullOrEmpty(selected))
                {
                    return;
                }

                var assetPath = ToAssetPath(selected);
                if (string.IsNullOrEmpty(assetPath))
                {
                    EditorUtility.DisplayDialog(
                        I18N.Get("error.title"),
                        I18N.Get("error.invalidFolder"),
                        I18N.Get("action.ok"));
                    return;
                }

                field.value = assetPath;
            })
            {
                text = "...",
                tooltip = I18N.Get(
                    "settings.createFolder.browseTooltip")
            };
            browse.style.width = 30f;
            browse.style.marginLeft = 4f;
            root.Add(browse);
            return root;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = System.IO.Path.GetDirectoryName(
                Application.dataPath);
            return System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    projectRoot ?? string.Empty,
                    assetPath ?? "Assets"));
        }

        private static string ToAssetPath(string absolutePath)
        {
            var normalized = (absolutePath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
            var dataPath = Application.dataPath
                .Replace('\\', '/')
                .TrimEnd('/');
            if (string.Equals(
                    normalized,
                    dataPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            return normalized.StartsWith(
                dataPath + "/",
                StringComparison.OrdinalIgnoreCase)
                ? "Assets" + normalized.Substring(dataPath.Length)
                : string.Empty;
        }
    }
}
