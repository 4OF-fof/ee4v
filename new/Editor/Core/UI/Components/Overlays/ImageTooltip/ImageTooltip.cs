using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ImageTooltipState
    {
        public ImageTooltipState(Texture texture, string fileName)
        {
            Texture = texture;
            FileName = fileName ?? string.Empty;
        }

        public Texture Texture { get; }

        public string FileName { get; }
    }

    internal sealed class ImageTooltip : VisualElement
    {
        private const string RootClassName = "ee4v-ui-image-tooltip";
        private const string ImageClassName = "ee4v-ui-image-tooltip__image";
        private readonly Image _image;
        private readonly UiTextElement _fileName;

        public ImageTooltip(ImageTooltipState state = null)
        {
            AddToClassList(RootClassName);
            pickingMode = PickingMode.Ignore;

            _image = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            _image.AddToClassList(ImageClassName);

            _fileName = UiTextFactory.Create(string.Empty, UiClassNames.ImageTooltipFileName);
            _fileName.SetWhiteSpace(WhiteSpace.NoWrap);
            _fileName.pickingMode = PickingMode.Ignore;

            Add(_image);
            Add(_fileName);
            SetState(state ?? new ImageTooltipState(null, string.Empty));
        }

        public void SetState(ImageTooltipState state)
        {
            state = state ?? new ImageTooltipState(null, string.Empty);
            _image.image = state.Texture;
            _fileName.SetText(state.FileName);

            var contentSize = ImageTooltipLayout.CalculateImageSize(state.Texture);
            _image.style.width = contentSize.x;
            _image.style.height = contentSize.y;
            _image.style.display = state.Texture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    internal sealed class ImageTooltipWindow : EditorWindow
    {
        private ImageTooltipState _state;
        private Vector2 _size;
        private bool _hasDesktopBounds;
        private Rect _desktopBounds;

        public static ImageTooltipWindow Show(VisualElement target, Vector2 panelPosition, ImageTooltipState state)
        {
            if (target == null || state == null || state.Texture == null)
            {
                return null;
            }

            var root = target.panel != null ? target.panel.visualTree : null;
            var rootOffset = root != null ? root.worldBound.position : Vector2.zero;
            var localPosition = panelPosition - rootOffset;
            var ownerWindow = FindOwnerWindow(target);
            var screenPosition = ownerWindow != null
                ? ownerWindow.position.position + localPosition
                : GUIUtility.GUIToScreenPoint(localPosition);

            var window = CreateInstance<ImageTooltipWindow>();
            window.Initialize(state, screenPosition);
            window.ShowPopup();
            window.SetNativeBackground(UiColorTokens.SurfaceRaised);
            return window;
        }

        public void SetPointerPosition(VisualElement target, Vector2 panelPosition)
        {
            if (target == null)
            {
                return;
            }

            var root = target.panel != null ? target.panel.visualTree : null;
            var rootOffset = root != null ? root.worldBound.position : Vector2.zero;
            var localPosition = panelPosition - rootOffset;
            var ownerWindow = FindOwnerWindow(target);
            var screenPosition = ownerWindow != null
                ? ownerWindow.position.position + localPosition
                : GUIUtility.GUIToScreenPoint(localPosition);
            position = _hasDesktopBounds
                ? ImageTooltipLayout.CalculateWindowRect(screenPosition, _size, _desktopBounds)
                : ImageTooltipLayout.CalculateWindowRect(screenPosition, _size);
        }

        private void Initialize(ImageTooltipState state, Vector2 screenPosition)
        {
            _state = state;
            _size = ImageTooltipLayout.CalculateWindowSize(state);
            minSize = _size;
            maxSize = _size;
            _hasDesktopBounds = EditorPopupWindow.TryGetDesktopBounds(screenPosition, out _desktopBounds);
            position = _hasDesktopBounds
                ? ImageTooltipLayout.CalculateWindowRect(screenPosition, _size, _desktopBounds)
                : ImageTooltipLayout.CalculateWindowRect(screenPosition, _size);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ee4v-ui");
            root.style.width = _size.x;
            root.style.height = _size.y;
            root.style.backgroundColor = new StyleColor(UiColorTokens.SurfaceRaised);
            root.pickingMode = PickingMode.Ignore;

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Overlays/ImageTooltip/image-tooltip.uss");
            root.Add(new ImageTooltip(_state));
        }

        private void SetNativeBackground(Color color)
        {
            EditorPopupWindow.TrySetBackgroundColor(this, color);
        }

        private static EditorWindow FindOwnerWindow(VisualElement target)
        {
            if (target == null || target.panel == null)
            {
                return null;
            }

            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window != null && window.rootVisualElement != null && window.rootVisualElement.panel == target.panel)
                {
                    return window;
                }
            }

            return EditorWindow.mouseOverWindow != null
                ? EditorWindow.mouseOverWindow
                : EditorWindow.focusedWindow;
        }
    }

    internal static class ImageTooltipLayout
    {
        internal const float MaximumImageWidth = 300f;
        internal const float MaximumImageHeight = 240f;
        private const float MinimumWidth = 140f;
        private const float HorizontalPadding = 8f;
        private const float VerticalPadding = 8f;
        private const float FileNameHeight = 22f;
        private const float ImageToFileNameGap = 6f;
        private const float PointerOffset = 16f;
        private const float OppositeSideGap = 12f;

        public static Vector2 CalculateImageSize(Texture texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return Vector2.zero;
            }

            var scale = Mathf.Min(1f, Mathf.Min(MaximumImageWidth / texture.width, MaximumImageHeight / texture.height));
            return new Vector2(
                Mathf.Max(1f, Mathf.Round(texture.width * scale)),
                Mathf.Max(1f, Mathf.Round(texture.height * scale)));
        }

        public static Vector2 CalculateWindowSize(ImageTooltipState state)
        {
            var imageSize = CalculateImageSize(state != null ? state.Texture : null);
            var width = Mathf.Max(MinimumWidth, imageSize.x + HorizontalPadding * 2f);
            var height = VerticalPadding * 2f + imageSize.y + FileNameHeight;
            if (imageSize.y > 0f)
            {
                height += ImageToFileNameGap;
            }

            return new Vector2(width, height);
        }

        public static Rect CalculateWindowRect(Vector2 pointerPosition, Vector2 size)
        {
            return new Rect(pointerPosition + new Vector2(PointerOffset, PointerOffset), size);
        }

        public static Rect CalculateWindowRect(Vector2 pointerPosition, Vector2 size, Rect desktopBounds)
        {
            var x = pointerPosition.x + PointerOffset;
            var y = pointerPosition.y + PointerOffset;
            if (x + size.x > desktopBounds.xMax)
            {
                x = pointerPosition.x - size.x - OppositeSideGap;
            }

            if (y + size.y > desktopBounds.yMax)
            {
                y = pointerPosition.y - size.y - OppositeSideGap;
            }

            x = Mathf.Clamp(x, desktopBounds.xMin, Mathf.Max(desktopBounds.xMin, desktopBounds.xMax - size.x));
            y = Mathf.Clamp(y, desktopBounds.yMin, Mathf.Max(desktopBounds.yMin, desktopBounds.yMax - size.y));
            return new Rect(x, y, size.x, size.y);
        }
    }
}
