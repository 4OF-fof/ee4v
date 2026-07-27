using System;
using System.Collections.Generic;
using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderStyle
{
    internal sealed class FolderStyleRenderer
    {
        private static readonly Color DarkListBackground =
            new Color32(56, 56, 56, 255);
        private static readonly Color DarkGridBackground =
            new Color32(51, 51, 51, 255);
        private static readonly Color LightListBackground =
            new Color32(200, 200, 200, 255);
        private static readonly Color LightGridBackground =
            new Color32(189, 189, 189, 255);

        private readonly FolderStyleService _service;
        private readonly FolderStyleIconCache _iconCache;
        private readonly FolderStyleAltTrigger _altTrigger;
        private readonly Action<IReadOnlyList<string>, Vector2>
            _openEditor;

        public FolderStyleRenderer(
            FolderStyleService service,
            FolderStyleIconCache iconCache,
            FolderStyleAltTrigger altTrigger,
            Action<IReadOnlyList<string>, Vector2> openEditor)
        {
            _service = service ??
                throw new ArgumentNullException(nameof(service));
            _iconCache = iconCache ??
                throw new ArgumentNullException(nameof(iconCache));
            _altTrigger = altTrigger ??
                throw new ArgumentNullException(nameof(altTrigger));
            _openEditor = openEditor ??
                throw new ArgumentNullException(nameof(openEditor));
        }

        public void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                string.IsNullOrEmpty(context.Guid))
            {
                return;
            }

            var folderPath =
                AssetDatabase.GUIDToAssetPath(context.Guid);
            if (string.IsNullOrEmpty(folderPath) ||
                !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            HandleAltTrigger(context);

            if (Event.current == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            var folderStyle = _service.Get(context.Guid);
            if (folderStyle.IsEmpty)
            {
                return;
            }

            var iconRect = FolderStyleLayout.GetIconRect(
                context.SelectionRect,
                context.ProjectViewMode,
                context.ProjectOrientation);
            var customIcon = folderStyle.HasIcon
                ? _iconCache.Get(folderStyle.IconGuid)
                : null;
            if (customIcon != null)
            {
                ClearOriginalFolderIcon(
                    iconRect,
                    context);
                GUI.DrawTexture(
                    iconRect,
                    customIcon,
                    ScaleMode.ScaleToFit,
                    true);
                return;
            }

            if (!folderStyle.HasColor)
            {
                return;
            }

            var folderIcon =
                EditorGUIUtility.IconContent("Folder Icon").image;
            if (folderIcon == null)
            {
                return;
            }

            ClearOriginalFolderIcon(
                iconRect,
                context);
            var previousColor = GUI.color;
            GUI.color = folderStyle.Color;
            GUI.DrawTexture(
                iconRect,
                folderIcon,
                ScaleMode.ScaleToFit,
                true);
            GUI.color = previousColor;
        }

        private static void ClearOriginalFolderIcon(
            Rect iconRect,
            ItemInjectionContext context)
        {
            EditorGUI.DrawRect(
                iconRect,
                ResolveBackgroundColor(
                    context.ProjectViewMode,
                    context.ProjectOrientation,
                    EditorGUIUtility.isProSkin));
        }

        private void HandleAltTrigger(
            ItemInjectionContext context)
        {
            var currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            var pointerInside = context.SelectionRect.Contains(
                currentEvent.mousePosition);
            if (!_altTrigger.TryActivate(
                    context.Guid,
                    currentEvent.alt,
                    pointerInside))
            {
                return;
            }

            var selectedFolderGuids =
                GetSelectedFolderGuids();
            var targetGuids =
                FolderStyleSelection.ResolveTargets(
                    context.Guid,
                    selectedFolderGuids);
            var anchor = GUIUtility.GUIToScreenPoint(
                new Vector2(
                    context.SelectionRect.xMax,
                    context.SelectionRect.y));

            FocusMouseOverWindow();
            EditorApplication.delayCall += () =>
                _openEditor(targetGuids, anchor);
        }

        private static void FocusMouseOverWindow()
        {
            var window = EditorWindow.mouseOverWindow;
            if (window != null &&
                !ReferenceEquals(
                    window,
                    EditorWindow.focusedWindow))
            {
                window.Focus();
            }
        }

        private static IReadOnlyList<string>
            GetSelectedFolderGuids()
        {
            var selectedGuids = Selection.assetGUIDs;
            var result = new List<string>();
            for (var i = 0; i < selectedGuids.Length; i++)
            {
                var guid = selectedGuids[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) &&
                    AssetDatabase.IsValidFolder(path))
                {
                    result.Add(guid);
                }
            }

            return result;
        }

        internal static Color ResolveBackgroundColor(
            ProjectItemViewMode viewMode,
            ProjectItemOrientation orientation,
            bool isProSkin)
        {
            var isList =
                viewMode == ProjectItemViewMode.OneColumn ||
                orientation == ProjectItemOrientation.Horizontal;
            if (isProSkin)
            {
                return isList
                    ? DarkListBackground
                    : DarkGridBackground;
            }

            return isList
                ? LightListBackground
                : LightGridBackground;
        }
    }
}
