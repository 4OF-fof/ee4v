using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class HistoryNavigationOverlayRowState
    {
        public HistoryNavigationOverlayRowState(string label, Action action = null)
        {
            Label = label ?? string.Empty;
            Action = action;
        }

        public string Label { get; }

        public Action Action { get; }
    }

    internal static class HistoryNavigationMenu
    {
        public static ContextMenuState CreateState(
            IReadOnlyList<HistoryNavigationOverlayRowState> rows,
            int maximumVisibleRows = 5)
        {
            if (rows == null || rows.Count == 0)
            {
                return new ContextMenuState(null);
            }

            var visibleCount = Math.Min(
                Mathf.Clamp(maximumVisibleRows, 1, 20),
                rows.Count);
            var items = new ContextMenuItemState[visibleCount];

            for (var i = 0; i < visibleCount; i++)
            {
                var row = rows[i] ??
                    new HistoryNavigationOverlayRowState(string.Empty);
                items[i] = new ContextMenuItemState(
                    "history-" + i,
                    row.Label,
                    row.Action,
                    row.Action != null);
            }

            return new ContextMenuState(items);
        }

        public static ContextMenuWindow Show(
            VisualElement anchor,
            IReadOnlyList<HistoryNavigationOverlayRowState> rows,
            int maximumVisibleRows = 5)
        {
            if (anchor == null || rows == null || rows.Count == 0)
            {
                return null;
            }

            var panelPosition = new Vector2(
                anchor.worldBound.xMin,
                anchor.worldBound.yMax + 2f);
            return ContextMenuWindow.Show(
                anchor,
                panelPosition,
                CreateState(rows, maximumVisibleRows));
        }
    }

    internal sealed class HistoryNavigationOverlay : VisualElement
    {
        private const float HistoryMinimumWidth = 160f;
        private const float HistoryMaximumWidth = 420f;
        private const float BreadcrumbMaximumWidth = 600f;
        private const string RootClassName = "ee4v-ui-history-navigation-overlay";
        private const string BreadcrumbClassName = "ee4v-ui-history-navigation-overlay--breadcrumb";
        private const string ItemClassName = "ee4v-ui-history-navigation-overlay__item";
        private int _maximumVisibleRows;
        private VisualElement _anchor;

        public HistoryNavigationOverlay(int maximumVisibleRows = 5)
        {
            AddToClassList(RootClassName);
            UiStyleUtility.AddPackageStyleSheet(
                this,
                "Editor/UI/Components/Navigation/history-navigation-overlay.uss");
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.None;
            SetMaximumVisibleRows(maximumVisibleRows);
            RegisterCallback<GeometryChangedEvent>(_ => Reposition());
        }

        public float MaximumWidth { get; private set; } = HistoryMaximumWidth;

        public float MinimumWidth { get; private set; } = HistoryMinimumWidth;

        public void SetMaximumVisibleRows(int value)
        {
            _maximumVisibleRows = Mathf.Clamp(value, 1, 20);
        }

        public void SetRows(IReadOnlyList<HistoryNavigationOverlayRowState> rows)
        {
            Clear();
            EnableInClassList(BreadcrumbClassName, false);
            MinimumWidth = HistoryMinimumWidth;
            MaximumWidth = HistoryMaximumWidth;
            if (rows == null)
            {
                return;
            }

            var visibleCount = Math.Min(_maximumVisibleRows, rows.Count);
            for (var i = 0; i < visibleCount; i++)
            {
                Add(CreateRow(rows[i]));
            }

            if (rows.Count > visibleCount)
            {
                Add(CreateRow(new HistoryNavigationOverlayRowState("…")));
            }
        }

        public void SetBreadcrumbs(
            IReadOnlyList<string> breadcrumbs,
            Action<int> onSelected)
        {
            Clear();
            EnableInClassList(BreadcrumbClassName, true);
            MinimumWidth = 0f;
            if (breadcrumbs == null)
            {
                MaximumWidth = BreadcrumbMaximumWidth;
                return;
            }

            for (var i = 0; i < breadcrumbs.Count; i++)
            {
                if (i > 0)
                {
                    var separator = UiTextFactory.Create(
                        "/",
                        UiClassNames.HistoryNavigationOverlaySeparator);
                    separator.SetColor(UiColorTokens.TextMuted);
                    separator.pickingMode = PickingMode.Ignore;
                    Add(separator);
                }

                var index = i;
                Add(CreateRow(new HistoryNavigationOverlayRowState(
                    breadcrumbs[i],
                    () => onSelected?.Invoke(index))));
            }

            MaximumWidth = BreadcrumbMaximumWidth;
        }

        public void Show(VisualElement root, VisualElement anchor)
        {
            if (root == null || anchor == null)
            {
                return;
            }

            if (parent != root)
            {
                RemoveFromHierarchy();
                root.Add(this);
            }

            _anchor = anchor;
            var availableWidth = Mathf.Max(
                0f,
                root.resolvedStyle.width - 8f);
            style.width = StyleKeyword.Auto;
            style.minWidth = Mathf.Min(MinimumWidth, availableWidth);
            style.maxWidth = Mathf.Min(
                MaximumWidth,
                availableWidth);
            style.display = DisplayStyle.Flex;
            Reposition();
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
            _anchor = null;
        }

        private void Reposition()
        {
            var root = parent;
            if (root == null || _anchor == null ||
                resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            var overlayWidth = resolvedStyle.width;
            if (float.IsNaN(overlayWidth))
            {
                return;
            }

            var rootPosition = root.worldBound.position;
            var anchorX = _anchor.worldBound.x - rootPosition.x;
            style.left = Mathf.Clamp(
                anchorX,
                4f,
                Mathf.Max(4f, root.resolvedStyle.width - overlayWidth - 4f));
            style.top = Mathf.Max(
                4f,
                _anchor.worldBound.yMax - rootPosition.y + 4f);
        }

        private static Button CreateRow(HistoryNavigationOverlayRowState state)
        {
            state = state ??
                new HistoryNavigationOverlayRowState(string.Empty);
            var button = new Button(state.Action);
            button.AddToClassList(ItemClassName);
            button.SetEnabled(state.Action != null);
            button.focusable = false;

            var label = UiTextFactory.Create(
                state.Label,
                UiClassNames.HistoryNavigationOverlayRow);
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            label.pickingMode = PickingMode.Ignore;
            label.SetColor(
                state.Action != null
                    ? UiColorTokens.TextPrimary
                    : UiColorTokens.TextMuted);
            button.Add(label);
            return button;
        }
    }
}
