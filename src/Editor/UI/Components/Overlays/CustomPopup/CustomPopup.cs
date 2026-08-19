using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
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

            var header = new VisualElement();
            header.AddToClassList(HeaderClassName);
            _title = UiTextFactory.Create(
                title ?? string.Empty,
                UiClassNames.SectionTitle,
                TitleClassName);
            HeaderActions = new VisualElement();
            HeaderActions.AddToClassList(HeaderActionsClassName);
            header.Add(_title);
            header.Add(HeaderActions);
            hierarchy.Add(header);

            Content = new VisualElement();
            Content.AddToClassList(ContentClassName);
            hierarchy.Add(Content);

            Footer = new VisualElement();
            Footer.AddToClassList(FooterClassName);
            hierarchy.Add(Footer);
            SetFooterVisible(showFooter);
        }

        public VisualElement HeaderActions { get; }

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
