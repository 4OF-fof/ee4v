using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class PreviewUpdateScheduler : IDisposable
    {
        private readonly Dictionary<object, Action> _pendingUpdates =
            new Dictionary<object, Action>();
        private readonly Action _refreshBounds;
        private readonly Action _repaint;
        private bool _boundsDirty;
        private bool _repaintPending;
        private bool _scheduled;
        private bool _disposed;

        public PreviewUpdateScheduler(
            Action refreshBounds,
            Action repaint)
        {
            _refreshBounds = refreshBounds;
            _repaint = repaint;
        }

        public void Enqueue(
            object key,
            Action update,
            bool refreshBounds = false)
        {
            if (_disposed)
            {
                return;
            }
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            _pendingUpdates[key] = update;
            _boundsDirty |= refreshBounds;
            _repaintPending = true;
            Schedule();
        }

        public void RequestRepaint()
        {
            if (_disposed)
            {
                return;
            }

            _repaintPending = true;
            Schedule();
        }

        public void FlushNow()
        {
            if (_disposed)
            {
                return;
            }

            Unschedule();
            var updates = new Action[_pendingUpdates.Count];
            _pendingUpdates.Values.CopyTo(updates, 0);
            _pendingUpdates.Clear();
            var refreshBounds = _boundsDirty;
            var repaint = _repaintPending;
            _boundsDirty = false;
            _repaintPending = false;

            try
            {
                foreach (var update in updates)
                {
                    update();
                }
            }
            finally
            {
                if (refreshBounds)
                {
                    _refreshBounds?.Invoke();
                }
                if (repaint)
                {
                    _repaint?.Invoke();
                }
                if (_pendingUpdates.Count > 0 ||
                    _boundsDirty ||
                    _repaintPending)
                {
                    Schedule();
                }
            }
        }

        public void CancelPending()
        {
            Unschedule();
            _pendingUpdates.Clear();
            _boundsDirty = false;
            _repaintPending = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CancelPending();
            _disposed = true;
        }

        private void Schedule()
        {
            if (_scheduled)
            {
                return;
            }

            _scheduled = true;
            EditorApplication.delayCall += FlushScheduled;
        }

        private void Unschedule()
        {
            if (!_scheduled)
            {
                return;
            }

            EditorApplication.delayCall -= FlushScheduled;
            _scheduled = false;
        }

        private void FlushScheduled()
        {
            _scheduled = false;
            FlushNow();
        }
    }

    public sealed class ScenePreviewViewport : VisualElement, IDisposable
    {
        private const int GridTextureSize = 64;
        private const int GridCellSize = 16;
        private const string LightBackgroundClassName =
            "ee4v-ui-scene-preview-viewport__background--light";

        private readonly Action<Rect> _drawPreview;
        private readonly Action _resetView;
        private readonly PreviewContainer _surface;
        private readonly IMGUIContainer _renderElement;
        private readonly VisualElement _placeholder;
        private readonly UiButton _backgroundToggle;
        private readonly UiButton _resetButton;
        private Texture2D _gridTexture;
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
            RegisterCallback<AttachToPanelEvent>(_ => EnsureGridTexture());
            RegisterCallback<DetachFromPanelEvent>(_ => DestroyGridTexture());
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
            DestroyGridTexture();
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
                DrawGrid(rect);
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
            DestroyGridTexture();
            EnsureGridTexture();
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

        private void DrawGrid(Rect rect)
        {
            EnsureGridTexture();
            if (_gridTexture == null)
            {
                return;
            }

            GUI.DrawTextureWithTexCoords(
                rect,
                _gridTexture,
                new Rect(
                    0f,
                    0f,
                    rect.width / GridTextureSize,
                    rect.height / GridTextureSize),
                false);
        }

        private void EnsureGridTexture()
        {
            if (_gridTexture != null)
            {
                return;
            }

            var baseColor = _lightBackground
                ? new Color32(96, 100, 111, 255)
                : new Color32(31, 33, 36, 255);
            var minorColor = _lightBackground
                ? new Color32(109, 113, 123, 255)
                : new Color32(43, 46, 51, 255);
            var majorColor = _lightBackground
                ? new Color32(136, 139, 150, 255)
                : new Color32(61, 65, 72, 255);
            var pixels = new Color32[GridTextureSize * GridTextureSize];
            for (var y = 0; y < GridTextureSize; y++)
            {
                for (var x = 0; x < GridTextureSize; x++)
                {
                    var major = x == 0 || y == 0;
                    var minor = x % GridCellSize == 0 ||
                                y % GridCellSize == 0;
                    pixels[(y * GridTextureSize) + x] = major
                        ? majorColor
                        : minor
                            ? minorColor
                            : baseColor;
                }
            }

            _gridTexture = new Texture2D(
                GridTextureSize,
                GridTextureSize,
                TextureFormat.RGBA32,
                false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            _gridTexture.SetPixels32(pixels);
            _gridTexture.Apply(false, true);
        }

        private void DestroyGridTexture()
        {
            if (_gridTexture == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(_gridTexture);
            _gridTexture = null;
        }
    }
}
