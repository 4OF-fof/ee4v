using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class FileTreeImageTooltipWindow : EditorWindow
    {
        private const float Padding = 8f;
        private const float NameHeight = 22f;
        private const float NameGap = 6f;
        private const float PointerOffset = 16f;
        private const float OppositeSideGap = 12f;

        private Texture2D _texture;
        private string _fileName;
        private Vector2 _size;

        internal static FileTreeImageTooltipWindow Show(
            VisualElement row,
            Vector2 panelPosition,
            Texture2D texture,
            string fileName)
        {
            if (row?.panel == null || texture == null)
            {
                return null;
            }

            var window = CreateInstance<FileTreeImageTooltipWindow>();
            window._texture = texture;
            window._fileName = fileName ?? string.Empty;
            window._size = new Vector2(
                Mathf.Max(140f, texture.width + Padding * 2f),
                texture.height + Padding * 2f + NameGap + NameHeight);
            window.minSize = window._size;
            window.maxSize = window._size;
            window.SetPointerPosition(row, panelPosition);
            window.ShowPopup();
            EditorPopupApi.TrySetBackgroundColor(
                window,
                UiColorTokens.SurfaceRaised);
            return window;
        }

        internal void SetPointerPosition(
            VisualElement row,
            Vector2 panelPosition)
        {
            if (row?.panel == null)
            {
                return;
            }

            var screenPosition = ToScreenPosition(row, panelPosition);
            var x = screenPosition.x + PointerOffset;
            var y = screenPosition.y + PointerOffset;
            if (EditorPopupApi.TryGetDesktopBounds(
                    screenPosition,
                    out var desktopBounds))
            {
                if (x + _size.x > desktopBounds.xMax)
                {
                    x = screenPosition.x - _size.x - OppositeSideGap;
                }

                if (y + _size.y > desktopBounds.yMax)
                {
                    y = screenPosition.y - _size.y - OppositeSideGap;
                }

                x = Mathf.Clamp(
                    x,
                    desktopBounds.xMin,
                    Mathf.Max(desktopBounds.xMin, desktopBounds.xMax - _size.x));
                y = Mathf.Clamp(
                    y,
                    desktopBounds.yMin,
                    Mathf.Max(desktopBounds.yMin, desktopBounds.yMax - _size.y));
            }

            position = new Rect(x, y, _size.x, _size.y);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/AssetManager/UI/searchable-file-tree.uss");
            root.AddToClassList(
                "ee4v-asset-manager-file-tree__image-tooltip");
            root.pickingMode = PickingMode.Ignore;
            root.style.width = _size.x;
            root.style.height = _size.y;

            var image = new Image
            {
                image = _texture,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.AddToClassList(
                "ee4v-asset-manager-file-tree__image-tooltip-image");
            image.style.width = _texture.width;
            image.style.height = _texture.height;
            root.Add(image);

            var name = UiTextFactory.Create(
                _fileName,
                UiClassNames.SecondaryText,
                "ee4v-asset-manager-file-tree__image-tooltip-name");
            name.SetWhiteSpace(WhiteSpace.NoWrap);
            name.pickingMode = PickingMode.Ignore;
            name.tooltip = _fileName;
            root.Add(name);
        }

        private static Vector2 ToScreenPosition(
            VisualElement row,
            Vector2 panelPosition)
        {
            var root = row.panel.visualTree;
            var rootOffset = root != null
                ? root.worldBound.position
                : Vector2.zero;
            var localPosition = panelPosition - rootOffset;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window != null &&
                    window.rootVisualElement != null &&
                    window.rootVisualElement.panel == row.panel)
                {
                    return window.position.position + localPosition;
                }
            }

            return GUIUtility.GUIToScreenPoint(localPosition);
        }
    }
}
