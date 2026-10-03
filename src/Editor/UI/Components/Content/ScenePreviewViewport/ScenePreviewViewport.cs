using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class ScenePreviewViewport : VisualElement, IDisposable
    {
        private const string LightBackgroundClassName =
            "ee4v-ui-scene-preview-viewport__background--light";

        private readonly Action<Rect> _drawPreview;
        private readonly Action _resetView;
        private readonly PreviewContainer _surface;
        private readonly IMGUIContainer _renderElement;
        private readonly VisualElement _placeholder;
        private readonly UiButton _backgroundToggle;
        private readonly UiButton _resetButton;
        private PreviewGridBackground _gridBackground;
        private bool _lightBackground;

        public ScenePreviewViewport(
            Action<Rect> drawPreview,
            Action resetView,
            string backgroundTooltip,
            string resetTooltip)
        {
            _drawPreview = drawPreview;
            _resetView = resetView;
            _lightBackground = !EditorGUIUtility.isProSkin;

            AddToClassList("ee4v-ui-scene-preview-viewport");

            _surface = new PreviewContainer();
            _surface.AddToClassList(
                "ee4v-ui-scene-preview-viewport__surface");
            _surface.SetHasContent(true);

            _renderElement = new IMGUIContainer(Draw);
            _renderElement.AddToClassList(
                "ee4v-ui-scene-preview-viewport__render");
            _surface.Content.Add(_renderElement);

            FeatureOverlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            FeatureOverlay.AddToClassList(
                "ee4v-ui-scene-preview-viewport__feature-overlay");
            _surface.Overlay.Add(FeatureOverlay);

            _placeholder = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _placeholder.AddToClassList(
                "ee4v-ui-scene-preview-viewport__placeholder");
            _placeholder.Add(new Icon(
                FluentUiIcons.CreateState(
                    "cube.png",
                    UiSizeTokens.Size31,
                    tintColor: UiColorTokens.TextMuted)));
            _surface.Overlay.Add(_placeholder);

            var actions = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            actions.AddToClassList(
                "ee4v-ui-scene-preview-viewport__actions");

            _backgroundToggle = CreateIconButton(
                backgroundTooltip,
                "weather_sunny.png",
                ToggleBackground,
                "ee4v-ui-scene-preview-viewport__background");
            actions.Add(_backgroundToggle);

            _resetButton = CreateIconButton(
                resetTooltip,
                "arrow_clockwise.png",
                ResetView,
                "ee4v-ui-scene-preview-viewport__reset");
            actions.Add(_resetButton);
            _surface.Overlay.Add(actions);
            Add(_surface);

            RefreshBackgroundToggle();
            SetPreviewAvailable(false);
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        public VisualElement FeatureOverlay { get; }

        public Rect PreviewRect
        {
            get { return _renderElement.contentRect; }
        }

        public void SetPreviewAvailable(bool available)
        {
            _placeholder.style.display = available
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _resetButton.SetEnabled(available);
        }

        public void RequestRepaint()
        {
            _renderElement.MarkDirtyRepaint();
        }

        public void Dispose()
        {
            _gridBackground?.Dispose();
            _gridBackground = null;
        }

        private static UiButton CreateIconButton(
            string tooltip,
            string iconFileName,
            Action action,
            string className)
        {
            var icon = FluentUiIcons.CreateState(
                iconFileName,
                UiSizeTokens.Size18,
                tooltip);
            var button = new UiButton(
                icon == null ? tooltip : string.Empty,
                action,
                tooltip,
                icon,
                UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-ui-scene-preview-viewport__action");
            button.AddToClassList(className);
            return button;
        }

        private void Draw()
        {
            var rect = GUILayoutUtility.GetRect(
                1f,
                10000f,
                1f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
            {
                _gridBackground ??= new PreviewGridBackground(_lightBackground);
                _gridBackground.Draw(rect);
            }

            _drawPreview?.Invoke(rect);
        }

        private void ResetView()
        {
            _resetView?.Invoke();
            RequestRepaint();
        }

        private void ToggleBackground()
        {
            _lightBackground = !_lightBackground;
            _gridBackground?.Dispose();
            _gridBackground = null;
            RefreshBackgroundToggle();
            RequestRepaint();
        }

        private void RefreshBackgroundToggle()
        {
            _backgroundToggle.SetIcon(
                FluentUiIcons.CreateState(
                    _lightBackground
                        ? "weather_moon.png"
                        : "weather_sunny.png",
                    UiSizeTokens.Size18,
                    tintColor: UiColorTokens.TextOnState));
            _backgroundToggle.EnableInClassList(
                LightBackgroundClassName,
                _lightBackground);
        }

    }
}
