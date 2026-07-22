using System;
using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI;
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
            string shortcut = null)
        {
            Kind = ContextMenuItemKind.Action;
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Action = action;
            Enabled = enabled;
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

            var label = UiTextFactory.Create(item.Label);
            label.AddToClassList(LabelClassName);
            label.pickingMode = PickingMode.Ignore;
            label.SetWhiteSpace(WhiteSpace.NoWrap);

            var shortcut = UiTextFactory.Create(item.Shortcut);
            shortcut.AddToClassList(ShortcutClassName);
            shortcut.pickingMode = PickingMode.Ignore;
            shortcut.SetWhiteSpace(WhiteSpace.NoWrap);
            shortcut.style.display = string.IsNullOrWhiteSpace(item.Shortcut) ? DisplayStyle.None : DisplayStyle.Flex;

            ApplyItemColors(item, label, shortcut, false);
            button.RegisterCallback<PointerEnterEvent>(_ => ApplyItemColors(item, label, shortcut, true));
            button.RegisterCallback<PointerLeaveEvent>(_ => ApplyItemColors(item, label, shortcut, false));

            button.Add(label);
            button.Add(shortcut);
            return button;
        }

        private static void ApplyItemColors(ContextMenuItemState item, UiTextElement label, UiTextElement shortcut, bool hovered)
        {
            if (!item.Enabled)
            {
                label.SetColor(UiColorTokens.TextDisabled);
                shortcut.SetColor(UiColorTokens.TextDisabled);
                return;
            }

            label.SetColor(hovered ? UiColorTokens.TextOnState : UiColorTokens.TextPrimary);
            shortcut.SetColor(hovered ? UiColorTokens.TextOnState : UiColorTokens.TextMuted);
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
            var hasDesktopBounds = EditorPopupWindow.TryGetDesktopBounds(screenPosition, out var desktopBounds);
            var maximumWidth = hasDesktopBounds ? desktopBounds.width : float.PositiveInfinity;
            var window = CreateInstance<ContextMenuWindow>();
            window.Initialize(state, maximumWidth);
            window.position = hasDesktopBounds
                ? ContextMenuLayout.CalculateWindowRect(screenPosition, window._size, desktopBounds)
                : ContextMenuLayout.CalculateWindowRect(screenPosition, window._size);
            window.ShowPopup();
            EditorPopupWindow.TrySetBackgroundColor(window, UiColorTokens.Transparent);
            window.Focus();
            return window;
        }

        private void Initialize(ContextMenuState state, float maximumWidth)
        {
            _state = state;
            _size = ContextMenuLayout.CalculateSize(state, maximumWidth);
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
            root.style.backgroundColor = new StyleColor((Color)UiColorTokens.Transparent);
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Overlays/ContextMenuWindow/context-menu-window.uss");

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
        private const float ItemHeight = 26f;
        private const float SeparatorHeight = 8f;
        private const float MenuPaddingY = 4f;
        private const float MenuPaddingX = 4f;
        private const float MenuBorderWidth = 1f;
        private const float ItemPaddingX = 10f;
        private const float MinimumWidth = 100f;
        private const float ShortcutGap = 16f;
        private const float TextMeasurementAllowance = 12f;

        public static Vector2 CalculateSize(ContextMenuState state, float maximumWidth = float.PositiveInfinity)
        {
            state = state ?? new ContextMenuState(null);
            var width = state.Width > 0f ? state.Width : CalculateAutoWidth(state);
            if (!float.IsNaN(maximumWidth) && maximumWidth > 0f)
            {
                width = Mathf.Min(width, maximumWidth);
            }

            var height = (MenuPaddingY + MenuBorderWidth) * 2f;

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

        public static Rect CalculateWindowRect(Vector2 screenPosition, Vector2 size, Rect desktopBounds)
        {
            if (desktopBounds.width <= 0f || desktopBounds.height <= 0f)
            {
                return CalculateWindowRect(screenPosition, size);
            }

            var maximumX = Mathf.Max(desktopBounds.xMin, desktopBounds.xMax - size.x);
            var maximumY = Mathf.Max(desktopBounds.yMin, desktopBounds.yMax - size.y);
            var x = Mathf.Clamp(screenPosition.x, desktopBounds.xMin, maximumX);
            var y = Mathf.Clamp(screenPosition.y, desktopBounds.yMin, maximumY);
            return new Rect(new Vector2(x, y), size);
        }

        private static float CalculateAutoWidth(ContextMenuState state)
        {
            var width = MinimumWidth;
            var editorSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            var labelStyle = editorSkin != null ? editorSkin.label : GUIStyle.none;

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
                width = Mathf.Max(
                    width,
                    MenuBorderWidth + MenuPaddingX + ItemPaddingX + labelWidth + shortcutWidth +
                    TextMeasurementAllowance + ItemPaddingX + MenuPaddingX + MenuBorderWidth);
            }

            return Mathf.Max(width, MinimumWidth);
        }
    }
}
