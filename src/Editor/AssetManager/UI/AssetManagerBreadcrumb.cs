using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerBreadcrumbItem
    {
        public AssetManagerBreadcrumbItem(string label, Action action = null)
        {
            Label = label ?? string.Empty;
            Action = action;
        }

        public string Label { get; }

        public Action Action { get; }
    }

    internal sealed class AssetManagerBreadcrumb : VisualElement, IDisposable
    {
        private readonly UiTextElement _current;
        private readonly AssetManagerBreadcrumbTooltip _tooltip;
        private IReadOnlyList<AssetManagerBreadcrumbItem> _items =
            Array.Empty<AssetManagerBreadcrumbItem>();

        public AssetManagerBreadcrumb()
        {
            AddToClassList("ee4v-asset-manager__breadcrumbs");
            _current = UiTextFactory.Create(
                string.Empty,
                "ee4v-asset-manager__breadcrumb-current");
            _current.pickingMode = PickingMode.Ignore;
            _current.SetColor(UiColorTokens.TextSoft);
            _current.SetWhiteSpace(WhiteSpace.NoWrap);
            Add(_current);

            _tooltip = new AssetManagerBreadcrumbTooltip();
            RegisterCallback<PointerEnterEvent>(_ => ShowTooltip());
            RegisterCallback<PointerLeaveEvent>(_ =>
                _tooltip.ScheduleHide());
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        public void SetItems(
            IReadOnlyList<AssetManagerBreadcrumbItem> items)
        {
            _tooltip.Hide();
            _items = items ?? Array.Empty<AssetManagerBreadcrumbItem>();
            _current.SetText(
                _items.Count > 0
                    ? _items[_items.Count - 1]?.Label ?? string.Empty
                    : string.Empty);
        }

        public void Dispose()
        {
            _tooltip.Dispose();
        }

        private void ShowTooltip()
        {
            if (_items.Count <= 1)
            {
                return;
            }

            var overlayRoot = FindOverlayRoot();
            if (overlayRoot != null)
            {
                _tooltip.Show(overlayRoot, this, _items);
            }
        }

        private VisualElement FindOverlayRoot()
        {
            VisualElement overlayRoot = null;
            for (var ancestor = parent;
                 ancestor != null;
                 ancestor = ancestor.parent)
            {
                if (ancestor.ClassListContains("ee4v-asset-manager"))
                {
                    overlayRoot = ancestor;
                }
            }

            return overlayRoot;
        }
    }

    internal sealed class AssetManagerBreadcrumbTooltip
        : VisualElement, IDisposable
    {
        private const long HideDelayMilliseconds = 120;
        private const float MaximumWidth = 600f;
        private VisualElement _anchor;
        private IVisualElementScheduledItem _pendingHide;

        public AssetManagerBreadcrumbTooltip()
        {
            AddToClassList(
                "ee4v-asset-manager__breadcrumb-tooltip");
            style.display = DisplayStyle.None;
            RegisterCallback<PointerEnterEvent>(_ => CancelPendingHide());
            RegisterCallback<PointerLeaveEvent>(_ => ScheduleHide());
            RegisterCallback<GeometryChangedEvent>(_ => Reposition());
        }

        public void Show(
            VisualElement root,
            VisualElement anchor,
            IReadOnlyList<AssetManagerBreadcrumbItem> items)
        {
            if (root == null || anchor == null ||
                items == null || items.Count <= 1)
            {
                Hide();
                return;
            }

            CancelPendingHide();
            Clear();
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    var separator = AssetManagerControls.CreateIcon(
                        "chevron_right.png",
                        UiSizeTokens.Size10);
                    if (separator != null)
                    {
                        separator.tintColor = UiColorTokens.TextMuted;
                        separator.AddToClassList(
                            "ee4v-asset-manager__breadcrumb-tooltip-separator");
                        Add(separator);
                    }
                }

                Add(CreateItem(items[i]));
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
            style.maxWidth = Mathf.Min(MaximumWidth, availableWidth);
            style.display = DisplayStyle.Flex;
            Reposition();
        }

        public void ScheduleHide()
        {
            CancelPendingHide();
            _pendingHide = schedule.Execute(Hide)
                .StartingIn(HideDelayMilliseconds);
        }

        public void Hide()
        {
            CancelPendingHide();
            style.display = DisplayStyle.None;
            _anchor = null;
        }

        public void Dispose()
        {
            Hide();
            RemoveFromHierarchy();
        }

        private VisualElement CreateItem(
            AssetManagerBreadcrumbItem item)
        {
            item = item ?? new AssetManagerBreadcrumbItem(string.Empty);
            if (item.Action == null)
            {
                var current = UiTextFactory.Create(
                    item.Label,
                    "ee4v-asset-manager__breadcrumb-tooltip-current");
                current.SetColor(UiColorTokens.TextSoft);
                current.SetWhiteSpace(WhiteSpace.NoWrap);
                return current;
            }

            var action = item.Action;
            var button = UiTextFactory.CreateButton(
                item.Label,
                () =>
                {
                    Hide();
                    action();
                });
            button.AddToClassList(
                "ee4v-asset-manager__breadcrumb-tooltip-button");
            button.focusable = false;
            return button;
        }

        private void Reposition()
        {
            var root = parent;
            if (root == null || _anchor == null ||
                resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            var width = resolvedStyle.width;
            if (float.IsNaN(width))
            {
                return;
            }

            var rootPosition = root.worldBound.position;
            var anchorX = _anchor.worldBound.xMin - rootPosition.x;
            style.left = Mathf.Clamp(
                anchorX,
                4f,
                Mathf.Max(4f, root.resolvedStyle.width - width - 4f));
            style.top = Mathf.Max(
                4f,
                _anchor.worldBound.yMax - rootPosition.y + 4f);
        }

        private void CancelPendingHide()
        {
            if (_pendingHide == null)
            {
                return;
            }

            _pendingHide.Pause();
            _pendingHide = null;
        }
    }
}
