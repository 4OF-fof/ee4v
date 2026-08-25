using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public abstract class CustomPopupWindow : EditorWindow
    {
        private VisualElement _dragHandle;
        private VisualElement _headerActions;
        private VisualElement _keyboardTarget;
        private Action _submit;
        private int _dragPointerId = -1;
        private Vector2 _dragPointerOffset;

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

            UnregisterDragHandle();
            _dragHandle = popup.Header;
            _headerActions = popup.HeaderActions;
            _dragHandle.RegisterCallback<PointerDownEvent>(
                OnHeaderPointerDown);
            _dragHandle.RegisterCallback<PointerMoveEvent>(
                OnHeaderPointerMove);
            _dragHandle.RegisterCallback<PointerUpEvent>(
                OnHeaderPointerUp);
            _dragHandle.RegisterCallback<PointerCaptureOutEvent>(
                OnHeaderPointerCaptureOut);
            rootVisualElement.Add(popup);
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse ||
                IsHeaderAction(evt.target))
            {
                return;
            }

            EndDrag();
            _dragPointerId = evt.pointerId;
            _dragPointerOffset = new Vector2(
                evt.position.x,
                evt.position.y);
            _dragHandle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
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

        private void OnHeaderPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _dragPointerId ||
                !_dragHandle.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            var pointerScreenPosition = position.position +
                                        new Vector2(
                                            evt.position.x,
                                            evt.position.y);
            var windowPosition = position;
            windowPosition.position = pointerScreenPosition -
                                      _dragPointerOffset;
            position = windowPosition;
            evt.StopPropagation();
        }

        private void OnHeaderPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _dragPointerId)
            {
                return;
            }

            EndDrag();
            evt.StopPropagation();
        }

        private void OnHeaderPointerCaptureOut(
            PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _dragPointerId)
            {
                _dragPointerId = -1;
            }
        }

        private bool IsHeaderAction(IEventHandler target)
        {
            return target is VisualElement element &&
                   _headerActions != null &&
                   _headerActions.Contains(element);
        }

        private void EndDrag()
        {
            var pointerId = _dragPointerId;
            _dragPointerId = -1;
            if (pointerId >= 0 &&
                _dragHandle != null &&
                _dragHandle.HasPointerCapture(pointerId))
            {
                _dragHandle.ReleasePointer(pointerId);
            }
        }

        private void UnregisterDragHandle()
        {
            EndDrag();
            if (_dragHandle == null)
            {
                return;
            }

            _dragHandle.UnregisterCallback<PointerDownEvent>(
                OnHeaderPointerDown);
            _dragHandle.UnregisterCallback<PointerMoveEvent>(
                OnHeaderPointerMove);
            _dragHandle.UnregisterCallback<PointerUpEvent>(
                OnHeaderPointerUp);
            _dragHandle.UnregisterCallback<PointerCaptureOutEvent>(
                OnHeaderPointerCaptureOut);
            _dragHandle = null;
            _headerActions = null;
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
        private const string ContentClassName =
            "ee4v-ui-custom-popup__content";
        private const string FooterClassName =
            "ee4v-ui-custom-popup__footer";
        private const string FooterHiddenClassName =
            "ee4v-ui-custom-popup__footer--hidden";

        private readonly UiTextElement _title;

        public CustomPopup(
            string title = null,
            bool showFooter = false)
        {
            AddToClassList(RootClassName);
            AddToClassList(UiClassNames.PopupSurface);

            Header = new VisualElement();
            Header.AddToClassList(HeaderClassName);
            _title = UiTextFactory.Create(
                title ?? string.Empty,
                UiClassNames.SectionTitle,
                TitleClassName);
            HeaderActions = new VisualElement();
            HeaderActions.AddToClassList(HeaderActionsClassName);
            Header.Add(_title);
            Header.Add(HeaderActions);
            hierarchy.Add(Header);

            Content = new VisualElement();
            Content.AddToClassList(ContentClassName);
            hierarchy.Add(Content);

            Footer = new VisualElement();
            Footer.AddToClassList(FooterClassName);
            hierarchy.Add(Footer);
            SetFooterVisible(showFooter);
        }

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

        public static void ShowAsDropDown(
            EditorWindow window,
            VisualElement anchor,
            Vector2 size)
        {
            anchor?.Blur();
            Show(window, ResolveElementAnchor(anchor), size);
        }

        public static void ShowAtPanelPosition(
            EditorWindow window,
            VisualElement origin,
            Vector2 panelPosition,
            Vector2 size)
        {
            origin?.Blur();
            Show(
                window,
                new Rect(
                    ResolveScreenPosition(origin, panelPosition),
                    Vector2.zero),
                size);
        }

        private static void Show(
            EditorWindow window,
            Rect anchor,
            Vector2 size)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            var safeSize = new Vector2(
                Mathf.Max(1f, size.x),
                Mathf.Max(1f, size.y));
            window.minSize = safeSize;
            window.maxSize = safeSize;
            window.ShowAsDropDown(anchor, safeSize);
            window.Focus();
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
    }
}
