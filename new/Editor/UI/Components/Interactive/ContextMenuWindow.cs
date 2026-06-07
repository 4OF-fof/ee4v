using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum ContextMenuItemKind
    {
        Action,
        Separator
    }

    internal sealed class ContextMenuItemState
    {
        public ContextMenuItemState(
            string id,
            string label,
            Action action = null,
            bool enabled = true,
            IconState iconState = null,
            string shortcut = null)
        {
            Kind = ContextMenuItemKind.Action;
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Action = action;
            Enabled = enabled;
            IconState = iconState;
            Shortcut = shortcut ?? string.Empty;
        }

        private ContextMenuItemState()
        {
            Kind = ContextMenuItemKind.Separator;
            Id = string.Empty;
            Label = string.Empty;
            Enabled = false;
            Shortcut = string.Empty;
        }

        public ContextMenuItemKind Kind { get; }

        public string Id { get; }

        public string Label { get; }

        public Action Action { get; }

        public bool Enabled { get; }

        public IconState IconState { get; }

        public string Shortcut { get; }

        public static ContextMenuItemState Separator()
        {
            return new ContextMenuItemState();
        }
    }

    internal sealed class ContextMenuState
    {
        public ContextMenuState(IReadOnlyList<ContextMenuItemState> items, float width = 0f)
        {
            Items = items ?? Array.Empty<ContextMenuItemState>();
            Width = Mathf.Max(0f, width);
        }

        public IReadOnlyList<ContextMenuItemState> Items { get; }

        public float Width { get; }
    }

    internal sealed class ContextMenu : VisualElement
    {
        private const string RootClassName = "ee4v-ui-context-menu";
        private const string ItemClassName = "ee4v-ui-context-menu__item";
        private const string ItemDisabledClassName = "ee4v-ui-context-menu__item--disabled";
        private const string SeparatorClassName = "ee4v-ui-context-menu__separator";
        private const string IconSlotClassName = "ee4v-ui-context-menu__icon-slot";
        private const string LabelClassName = "ee4v-ui-context-menu__label";
        private const string ShortcutClassName = "ee4v-ui-context-menu__shortcut";
        private readonly Action<ContextMenuItemState> _onSelect;

        public ContextMenu(ContextMenuState state, Action<ContextMenuItemState> onSelect)
        {
            _onSelect = onSelect;
            AddToClassList(RootClassName);
            SetState(state);
        }

        public void SetState(ContextMenuState state)
        {
            state = state ?? new ContextMenuState(null);
            Clear();

            for (var i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item == null)
                {
                    continue;
                }

                Add(item.Kind == ContextMenuItemKind.Separator ? CreateSeparator() : CreateItem(item));
            }
        }

        private VisualElement CreateItem(ContextMenuItemState item)
        {
            var button = new Button(() => Select(item));
            button.AddToClassList(ItemClassName);
            button.EnableInClassList(ItemDisabledClassName, !item.Enabled);
            button.SetEnabled(item.Enabled);

            var iconSlot = new VisualElement();
            iconSlot.AddToClassList(IconSlotClassName);
            if (item.IconState != null)
            {
                iconSlot.Add(new Icon(item.IconState));
            }

            var label = UiTextFactory.Create(item.Label);
            label.AddToClassList(LabelClassName);
            label.pickingMode = PickingMode.Ignore;
            label.SetWhiteSpace(WhiteSpace.NoWrap);

            var shortcut = UiTextFactory.Create(item.Shortcut);
            shortcut.AddToClassList(ShortcutClassName);
            shortcut.pickingMode = PickingMode.Ignore;
            shortcut.SetWhiteSpace(WhiteSpace.NoWrap);
            shortcut.style.display = string.IsNullOrWhiteSpace(item.Shortcut) ? DisplayStyle.None : DisplayStyle.Flex;

            button.Add(iconSlot);
            button.Add(label);
            button.Add(shortcut);
            return button;
        }

        private static VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.AddToClassList(SeparatorClassName);
            return separator;
        }

        private void Select(ContextMenuItemState item)
        {
            if (item == null || !item.Enabled)
            {
                return;
            }

            _onSelect?.Invoke(item);
        }
    }

    internal sealed class ContextMenuWindow : EditorWindow
    {
        private ContextMenuState _state;
        private Vector2 _size;
        private bool _closeOnLostFocus = true;

        public static ContextMenuWindow Show(VisualElement target, Vector2 panelPosition, ContextMenuState state)
        {
            if (target == null)
            {
                return Show(panelPosition, state);
            }

            var root = target.panel != null ? target.panel.visualTree : null;
            var rootOffset = root != null ? root.worldBound.position : Vector2.zero;
            var localPosition = panelPosition - rootOffset;
            var ownerWindow = FindOwnerWindow(target);
            var screenPosition = ownerWindow != null
                ? ownerWindow.position.position + localPosition
                : GUIUtility.GUIToScreenPoint(localPosition);
            return Show(screenPosition, state);
        }

        public static ContextMenuWindow Show(Vector2 screenPosition, ContextMenuState state)
        {
            state = state ?? new ContextMenuState(null);
            var window = CreateInstance<ContextMenuWindow>();
            window.Initialize(state);
            window.position = ContextMenuLayout.CalculateWindowRect(screenPosition, window._size);
            window.ShowPopup();
            window.Focus();
            return window;
        }

        private void Initialize(ContextMenuState state)
        {
            _state = state;
            _size = ContextMenuLayout.CalculateSize(state);
            minSize = _size;
            maxSize = _size;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ee4v-ui");
            root.style.width = _size.x;
            root.style.height = _size.y;
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/context-menu-window.uss");

            root.Add(new ContextMenu(_state, Select));
            root.Focus();
        }

        private void OnLostFocus()
        {
            if (_closeOnLostFocus)
            {
                Close();
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            evt.StopPropagation();
            Close();
        }

        private void Select(ContextMenuItemState item)
        {
            _closeOnLostFocus = false;
            try
            {
                Close();
                item.Action?.Invoke();
            }
            finally
            {
                _closeOnLostFocus = true;
            }
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

    internal static class ContextMenuLayout
    {
        private const float ItemHeight = 19f;
        private const float SeparatorHeight = 6f;
        private const float MenuPadding = 10f;
        private const float MinimumWidth = 100f;
        private const float MaximumWidth = 360f;
        private const float IconSlotWidth = 22f;
        private const float LabelRightPadding = 24f;
        private const float ShortcutGap = 24f;

        public static Vector2 CalculateSize(ContextMenuState state)
        {
            state = state ?? new ContextMenuState(null);
            var width = state.Width > 0f ? state.Width : CalculateAutoWidth(state);
            var height = MenuPadding;

            for (var i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item == null)
                {
                    continue;
                }

                height += item.Kind == ContextMenuItemKind.Separator ? SeparatorHeight : ItemHeight;
            }

            return new Vector2(Mathf.Ceil(width), Mathf.Ceil(height));
        }

        public static Rect CalculateWindowRect(Vector2 screenPosition, Vector2 size)
        {
            return new Rect(screenPosition, size);
        }

        private static float CalculateAutoWidth(ContextMenuState state)
        {
            var width = MinimumWidth;
            var labelStyle = EditorStyles.label;

            for (var i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item == null || item.Kind != ContextMenuItemKind.Action)
                {
                    continue;
                }

                var labelWidth = labelStyle.CalcSize(new GUIContent(item.Label)).x;
                var shortcutWidth = string.IsNullOrWhiteSpace(item.Shortcut)
                    ? 0f
                    : labelStyle.CalcSize(new GUIContent(item.Shortcut)).x + ShortcutGap;
                width = Mathf.Max(width, MenuPadding + IconSlotWidth + labelWidth + LabelRightPadding + shortcutWidth + MenuPadding);
            }

            return Mathf.Clamp(width, MinimumWidth, MaximumWidth);
        }
    }
}
