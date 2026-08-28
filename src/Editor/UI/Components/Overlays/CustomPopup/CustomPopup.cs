using System;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public abstract class CustomPopupWindow : EditorWindow
    {
        private const float MinimumWidth = 340f;
        private const float MinimumHeight = 100f;
        private const float HeaderHeight = 24f;
        private bool _isDragging;
        private Vector2 _dragStartMouseScreen;
        private Rect _dragStartWindowPosition;
        private VisualElement _keyboardTarget;
        private Action _submit;
        private bool _isResizing;
        private int _resizeControlId;
        private ResizeEdge _resizeEdge = ResizeEdge.None;
        private Vector2 _resizeStartMouseScreen;
        private Rect _resizeStartWindowPosition;
        private bool _watchingTransientPicker;

        protected virtual void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload +=
                OnBeforeAssemblyReload;
        }

        protected virtual void OnDestroy()
        {
            AssemblyReloadEvents.beforeAssemblyReload -=
                OnBeforeAssemblyReload;
            EditorApplication.delayCall -= EvaluateFocusLoss;
            StopWatchingTransientPicker();
            _isDragging = false;
            if (GUIUtility.hotControl == _resizeControlId)
            {
                GUIUtility.hotControl = 0;
            }

            _resizeControlId = 0;
        }

        private void OnGUI()
        {
            HandleResize(Event.current);
        }

        protected void ShowAsPopup(
            VisualElement anchor,
            Vector2 size)
        {
            anchor?.Blur();
            var anchorBounds = ResolveElementAnchor(anchor);
            ShowAsPopup(
                new Vector2(anchorBounds.xMin, anchorBounds.yMax),
                size);
        }

        protected void ShowAsPopup(
            VisualElement origin,
            Vector2 panelPosition,
            Vector2 size)
        {
            origin?.Blur();
            ShowAsPopup(
                ResolveScreenPosition(origin, panelPosition),
                size);
        }

        protected void ShowAsPopup(
            Vector2 screenPosition,
            Vector2 size)
        {
            var safeSize = new Vector2(
                Mathf.Max(1f, size.x),
                Mathf.Max(1f, size.y));
            position = EditorPopupApi.TryGetDesktopBounds(
                    screenPosition,
                    out var desktopBounds)
                ? ClampToDesktop(
                    screenPosition,
                    safeSize,
                    desktopBounds)
                : new Rect(screenPosition, safeSize);
            ShowPopup();
            Focus();
            EditorPopupApi.TrySetBackgroundColor(
                this,
                UiColorTokens.PopupWindowBackground);
        }

        protected void ConfigureCloseAndSubmitKeys(
            VisualElement target,
            Action submit = null)
        {
            if (_keyboardTarget != null)
            {
                _keyboardTarget.UnregisterCallback<KeyDownEvent>(
                    OnKeyDown);
            }

            _keyboardTarget = target ??
                throw new ArgumentNullException(nameof(target));
            _submit = submit;
            _keyboardTarget.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        protected void SetPopup(CustomPopup popup)
        {
            if (popup == null)
            {
                throw new ArgumentNullException(nameof(popup));
            }

            popup.SetCloseAction(Close);
            WindowMover(popup);
            rootVisualElement.Add(popup);
        }

        protected virtual void OnLostFocus()
        {
            EditorApplication.delayCall -= EvaluateFocusLoss;
            EditorApplication.delayCall += EvaluateFocusLoss;
        }

        protected virtual void OnDisable()
        {
            EditorApplication.delayCall -= EvaluateFocusLoss;
            StopWatchingTransientPicker();
            _isDragging = false;
            if (_keyboardTarget == null)
            {
                return;
            }

            _keyboardTarget.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            _keyboardTarget = null;
            _submit = null;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
            else if ((evt.keyCode == KeyCode.Return ||
                      evt.keyCode == KeyCode.KeypadEnter) &&
                     _submit != null)
            {
                _submit();
                evt.StopPropagation();
            }
        }

        private void OnBeforeAssemblyReload()
        {
            Close();
        }

        private void WindowMover(CustomPopup popup)
        {
            var header = popup.Header;
            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 ||
                    popup.IsInteractiveHeaderTarget(evt.target))
                {
                    return;
                }

                _isDragging = true;
                header.CaptureMouse();
                _dragStartMouseScreen =
                    GUIUtility.GUIToScreenPoint(evt.mousePosition);
                _dragStartWindowPosition = position;
                evt.StopPropagation();
            });

            header.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!_isDragging)
                {
                    return;
                }

                var mouseScreen =
                    GUIUtility.GUIToScreenPoint(evt.mousePosition);
                var delta = mouseScreen - _dragStartMouseScreen;
                position = new Rect(
                    _dragStartWindowPosition.x + delta.x,
                    _dragStartWindowPosition.y + delta.y,
                    position.width,
                    position.height);
            });

            header.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!_isDragging)
                {
                    return;
                }

                _isDragging = false;
                header.ReleaseMouse();
                evt.StopPropagation();
            });
        }

        private void EvaluateFocusLoss()
        {
            if (this == null)
            {
                return;
            }

            var focused = EditorWindow.focusedWindow;
            if (focused == this)
            {
                StopWatchingTransientPicker();
                return;
            }

            if (IsTransientPicker(focused))
            {
                StartWatchingTransientPicker();
                return;
            }

            Close();
        }

        private void StartWatchingTransientPicker()
        {
            if (_watchingTransientPicker)
            {
                return;
            }

            _watchingTransientPicker = true;
            EditorApplication.update += WatchTransientPicker;
        }

        private void WatchTransientPicker()
        {
            if (this == null)
            {
                StopWatchingTransientPicker();
                return;
            }

            var focused = EditorWindow.focusedWindow;
            if (focused == this)
            {
                StopWatchingTransientPicker();
                return;
            }

            if (IsTransientPicker(focused))
            {
                return;
            }

            StopWatchingTransientPicker();
            Focus();
        }

        private void StopWatchingTransientPicker()
        {
            if (!_watchingTransientPicker)
            {
                return;
            }

            _watchingTransientPicker = false;
            EditorApplication.update -= WatchTransientPicker;
        }

        private static bool IsTransientPicker(EditorWindow window)
        {
            return EditorPopupApi.IsTransientPicker(window) ||
                   EditorPopupApi.HasOpenTransientPicker() ||
                   EditorPopupApi.IsEyeDropperOpen();
        }

        private static Rect ClampToDesktop(
            Vector2 screenPosition,
            Vector2 size,
            Rect desktopBounds)
        {
            return new Rect(
                Mathf.Clamp(
                    screenPosition.x,
                    desktopBounds.xMin,
                    Mathf.Max(
                        desktopBounds.xMin,
                        desktopBounds.xMax - size.x)),
                Mathf.Clamp(
                    screenPosition.y,
                    desktopBounds.yMin,
                    Mathf.Max(
                        desktopBounds.yMin,
                        desktopBounds.yMax - size.y)),
                size.x,
                size.y);
        }

        private void HandleResize(Event evt)
        {
            const float margin = 6f;
            const float cornerSize = 6f;
            var leftRect = new Rect(
                0f,
                HeaderHeight,
                margin,
                Mathf.Max(
                    0f,
                    position.height - HeaderHeight - cornerSize));
            var rightRect = new Rect(
                position.width - margin,
                HeaderHeight,
                margin,
                Mathf.Max(
                    0f,
                    position.height - HeaderHeight - cornerSize));
            var innerBottomWidth = Mathf.Max(
                0f,
                position.width - cornerSize * 2f);
            var bottomRect = new Rect(
                cornerSize,
                Mathf.Max(0f, position.height - margin),
                innerBottomWidth,
                margin);
            var bottomLeftRect = new Rect(
                0f,
                Mathf.Max(0f, position.height - cornerSize),
                cornerSize,
                cornerSize);
            var bottomRightRect = new Rect(
                Mathf.Max(0f, position.width - cornerSize),
                Mathf.Max(0f, position.height - cornerSize),
                cornerSize,
                cornerSize);

            EditorGUIUtility.AddCursorRect(
                leftRect,
                MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(
                rightRect,
                MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(
                bottomRect,
                MouseCursor.ResizeVertical);
            EditorGUIUtility.AddCursorRect(
                bottomLeftRect,
                MouseCursor.ResizeUpRight);
            EditorGUIUtility.AddCursorRect(
                bottomRightRect,
                MouseCursor.ResizeUpLeft);

            if (evt.type == EventType.MouseDown &&
                evt.button == 0 &&
                !_isDragging)
            {
                var detected = ResizeEdge.None;
                if (bottomLeftRect.Contains(evt.mousePosition))
                {
                    detected = ResizeEdge.Left | ResizeEdge.Bottom;
                }
                else if (bottomRightRect.Contains(evt.mousePosition))
                {
                    detected = ResizeEdge.Right | ResizeEdge.Bottom;
                }
                else if (leftRect.Contains(evt.mousePosition))
                {
                    detected = ResizeEdge.Left;
                }
                else if (rightRect.Contains(evt.mousePosition))
                {
                    detected = ResizeEdge.Right;
                }
                else if (bottomRect.Contains(evt.mousePosition))
                {
                    detected = ResizeEdge.Bottom;
                }

                if (detected != ResizeEdge.None)
                {
                    _isResizing = true;
                    _resizeEdge = detected;
                    _resizeStartMouseScreen =
                        GUIUtility.GUIToScreenPoint(evt.mousePosition);
                    _resizeStartWindowPosition = position;
                    _resizeControlId =
                        GUIUtility.GetControlID(FocusType.Passive);
                    GUIUtility.hotControl = _resizeControlId;
                    evt.Use();
                }
            }

            if (_isResizing && evt.type == EventType.MouseDrag)
            {
                var mouseScreen =
                    GUIUtility.GUIToScreenPoint(evt.mousePosition);
                var delta = mouseScreen - _resizeStartMouseScreen;
                var newPosition = _resizeStartWindowPosition;

                if ((_resizeEdge & ResizeEdge.Left) != 0)
                {
                    var newWidth =
                        _resizeStartWindowPosition.width - delta.x;
                    var newX =
                        _resizeStartWindowPosition.x + delta.x;
                    if (newWidth < MinimumWidth)
                    {
                        newWidth = MinimumWidth;
                        newX = _resizeStartWindowPosition.x +
                               (_resizeStartWindowPosition.width -
                                newWidth);
                    }

                    newPosition.x = newX;
                    newPosition.width = newWidth;
                }

                if ((_resizeEdge & ResizeEdge.Right) != 0)
                {
                    var newWidth =
                        _resizeStartWindowPosition.width + delta.x;
                    if (newWidth < MinimumWidth)
                    {
                        newWidth = MinimumWidth;
                    }

                    newPosition.width = newWidth;
                }

                if ((_resizeEdge & ResizeEdge.Bottom) != 0)
                {
                    var newHeight =
                        _resizeStartWindowPosition.height + delta.y;
                    if (newHeight < MinimumHeight)
                    {
                        newHeight = MinimumHeight;
                    }

                    newPosition.height = newHeight;
                }

                position = newPosition;
                evt.Use();
            }

            if (!_isResizing || evt.type != EventType.MouseUp)
            {
                return;
            }

            _isResizing = false;
            _resizeEdge = ResizeEdge.None;
            if (GUIUtility.hotControl == _resizeControlId)
            {
                GUIUtility.hotControl = 0;
            }

            _resizeControlId = 0;
            evt.Use();
        }

        private static Rect ResolveElementAnchor(VisualElement anchor)
        {
            if (anchor == null || anchor.panel == null)
            {
                return new Rect(
                    GUIUtility.GUIToScreenPoint(Vector2.zero),
                    Vector2.zero);
            }

            var root = anchor.panel.visualTree;
            var rootOffset = root != null
                ? root.worldBound.position
                : Vector2.zero;
            var localPosition = anchor.worldBound.position - rootOffset;
            var owner = FindOwnerWindow(anchor);
            var screenPosition = owner != null
                ? owner.position.position + localPosition
                : GUIUtility.GUIToScreenPoint(localPosition);
            return new Rect(screenPosition, anchor.worldBound.size);
        }

        private static Vector2 ResolveScreenPosition(
            VisualElement origin,
            Vector2 panelPosition)
        {
            if (origin == null || origin.panel == null)
            {
                return GUIUtility.GUIToScreenPoint(panelPosition);
            }

            var root = origin.panel.visualTree;
            var rootOffset = root != null
                ? root.worldBound.position
                : Vector2.zero;
            var localPosition = panelPosition - rootOffset;
            var owner = FindOwnerWindow(origin);
            return owner != null
                ? owner.position.position + localPosition
                : GUIUtility.GUIToScreenPoint(localPosition);
        }

        private static EditorWindow FindOwnerWindow(
            VisualElement target)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (var index = 0; index < windows.Length; index++)
            {
                var window = windows[index];
                if (window != null &&
                    window.rootVisualElement != null &&
                    window.rootVisualElement.panel == target.panel)
                {
                    return window;
                }
            }

            return EditorWindow.mouseOverWindow ??
                   EditorWindow.focusedWindow;
        }

        [Flags]
        private enum ResizeEdge
        {
            None = 0,
            Left = 1,
            Right = 2,
            Bottom = 4
        }
    }

    public sealed class CustomPopup : VisualElement
    {
        private const string RootClassName =
            "ee4v-ui-custom-popup";
        private const string HeaderClassName =
            "ee4v-ui-custom-popup__header";
        private const string TitleClassName =
            "ee4v-ui-custom-popup__title";
        private const string HeaderActionsClassName =
            "ee4v-ui-custom-popup__header-actions";
        private const string CloseButtonClassName =
            "ee4v-ui-custom-popup__close";
        private const string ContentClassName =
            "ee4v-ui-custom-popup__content";
        private const string FooterClassName =
            "ee4v-ui-custom-popup__footer";
        private const string FooterHiddenClassName =
            "ee4v-ui-custom-popup__footer--hidden";

        private readonly UiTextElement _title;
        private readonly UiButton _closeButton;
        private readonly string _closeTooltip;
        private Action _closeAction;

        public CustomPopup(
            string title = null,
            bool showFooter = false,
            string titleTooltip = null,
            string closeTooltip = null)
        {
            _closeTooltip = closeTooltip ?? string.Empty;
            AddToClassList(RootClassName);

            Header = new VisualElement();
            Header.AddToClassList(HeaderClassName);
            Header.style.flexDirection = FlexDirection.Row;
            Header.style.height = 24f;
            Header.style.flexShrink = 0f;
            Header.style.justifyContent = Justify.Center;

            var headerContent = new VisualElement();
            headerContent.style.flexDirection = FlexDirection.Row;
            headerContent.style.alignItems = Align.Center;
            headerContent.style.height = 24f;
            headerContent.style.flexGrow = 1f;
            HeaderLeading = new VisualElement();
            HeaderLeading.style.flexDirection = FlexDirection.Row;
            HeaderLeading.style.height = 24f;
            HeaderLeading.style.flexShrink = 0f;
            HeaderLeading.style.alignItems = Align.Center;
            _title = UiTextFactory.Create(
                title ?? string.Empty,
                TitleClassName);
            _title.tooltip = titleTooltip ?? string.Empty;
            _title.style.flexGrow = 1f;
            _title.style.flexShrink = 1f;
            _title.style.marginLeft = 8f;
            _title.style.marginRight = 4f;
            _title.style.fontSize = 14f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.overflow = Overflow.Hidden;
            _title.style.textOverflow = TextOverflow.Ellipsis;
            _title.SetWhiteSpace(WhiteSpace.NoWrap);
            HeaderActions = new VisualElement();
            HeaderActions.AddToClassList(HeaderActionsClassName);
            HeaderActions.style.flexDirection = FlexDirection.Row;
            HeaderActions.style.height = 24f;
            HeaderActions.style.flexShrink = 0f;
            HeaderActions.style.alignItems = Align.Center;
            headerContent.Add(HeaderLeading);
            headerContent.Add(_title);
            headerContent.Add(HeaderActions);
            Header.Add(headerContent);
            _closeButton = CreateCloseButton();
            Header.Add(_closeButton);
            hierarchy.Add(Header);

            Content = new VisualElement();
            Content.AddToClassList(ContentClassName);
            Content.style.marginRight = 4f;
            Content.style.marginLeft = 4f;
            Content.style.marginTop = 4f;
            Content.style.marginBottom = 4f;
            hierarchy.Add(Content);

            Footer = new VisualElement();
            Footer.AddToClassList(FooterClassName);
            hierarchy.Add(Footer);
            SetFooterVisible(showFooter);
        }

        public VisualElement HeaderLeading { get; }

        public VisualElement HeaderActions { get; }

        internal VisualElement Header { get; }

        public VisualElement Content { get; }

        public VisualElement Footer { get; }

        public void SetTitle(string title)
        {
            _title.SetText(title ?? string.Empty);
        }

        public void SetFooterVisible(bool visible)
        {
            Footer.EnableInClassList(FooterHiddenClassName, !visible);
        }

        internal void SetCloseAction(Action close)
        {
            _closeAction = close;
        }

        private UiButton CreateCloseButton()
        {
            var button = new UiButton(
                string.Empty,
                () => _closeAction?.Invoke(),
                _closeTooltip,
                icon: FluentUiIcons.CreateState(
                    "dismiss.png",
                    UiSizeTokens.Size16),
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(CloseButtonClassName);
            return button;
        }

        internal bool IsInteractiveHeaderTarget(IEventHandler target)
        {
            if (!(target is VisualElement element))
            {
                return false;
            }

            return HeaderActions.Contains(element) ||
                   (_closeButton != null &&
                    _closeButton.Contains(element));
        }

    }
}
