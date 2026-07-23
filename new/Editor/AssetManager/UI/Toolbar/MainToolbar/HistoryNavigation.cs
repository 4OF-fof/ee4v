using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class HistoryNavigation : VisualElement
    {
        private const long OverlayHideDelayMilliseconds = 120;
        private const string RootClassName = "ee4v-ui-history-navigation";
        private const string IconButtonClassName = "ee4v-ui-history-navigation__icon-button";
        private const string BreadcrumbClassName = "ee4v-ui-history-navigation__breadcrumb";
        private const string BreadcrumbItemClassName = "ee4v-ui-history-navigation__breadcrumb-item";
        private const string BreadcrumbItemCurrentClassName = "ee4v-ui-history-navigation__breadcrumb-item--current";
        private const string BreadcrumbItemLabelCurrentClassName = "ee4v-ui-history-navigation__breadcrumb-item-label--current";
        private readonly Button _backButton;
        private readonly Button _forwardButton;
        private readonly VisualElement _breadcrumb;
        private readonly HistoryNavigationOverlay _hoverOverlay;
        private VisualElement _hoverOverlayAnchor;
        private IVisualElementScheduledItem _pendingOverlayHide;
        private AssetItemGridHistoryState _state;

        public HistoryNavigation(int maximumVisibleHistoryRows = 5)
        {
            AddToClassList(RootClassName);

            _backButton = CreateNavigationButton("<", I18N.Get("assetManager.mainToolbar.history.back"));
            _backButton.clicked += () => BackClicked?.Invoke();
            _backButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                CancelPendingOverlayHide();
                ShowHistoryOverlay(_backButton, _state.BackEntries, true);
            });
            _backButton.RegisterCallback<PointerLeaveEvent>(_ => ScheduleOverlayHide());
            Add(_backButton);

            _forwardButton = CreateNavigationButton(">", I18N.Get("assetManager.mainToolbar.history.forward"));
            _forwardButton.clicked += () => ForwardClicked?.Invoke();
            _forwardButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                CancelPendingOverlayHide();
                ShowHistoryOverlay(_forwardButton, _state.ForwardEntries, false);
            });
            _forwardButton.RegisterCallback<PointerLeaveEvent>(_ => ScheduleOverlayHide());
            Add(_forwardButton);

            _breadcrumb = new VisualElement();
            _breadcrumb.AddToClassList(BreadcrumbClassName);
            Add(_breadcrumb);

            _hoverOverlay = new HistoryNavigationOverlay(maximumVisibleHistoryRows);
            _hoverOverlay.RegisterCallback<PointerEnterEvent>(_ => CancelPendingOverlayHide());
            _hoverOverlay.RegisterCallback<PointerLeaveEvent>(_ => ScheduleOverlayHide());
            _hoverOverlay.RegisterCallback<GeometryChangedEvent>(_ => RepositionHoverOverlay());
            _state = new AssetItemGridHistoryState(null, false, false);
            RegisterCallback<DetachFromPanelEvent>(_ => RemoveHoverOverlay());
        }

        public event Action BackClicked;

        public event Action ForwardClicked;

        public event Action<int> BackHistoryClicked;

        public event Action<int> ForwardHistoryClicked;

        public event Action<int> BreadcrumbClicked;

        public void SetMaximumVisibleRows(int value)
        {
            _hoverOverlay.SetMaximumVisibleRows(value);
        }

        public void SetState(AssetItemGridHistoryState state)
        {
            _state = state ?? new AssetItemGridHistoryState(null, false, false);
            HideHoverOverlay();
            _backButton.SetEnabled(_state.CanGoBack);
            _forwardButton.SetEnabled(_state.CanGoForward);
            RefreshBreadcrumb(_state.Current);
        }

        private void ShowBreadcrumbOverlay()
        {
            var current = _state.Current;
            var breadcrumbs = current != null ? current.Breadcrumbs : null;
            if (breadcrumbs == null || breadcrumbs.Count <= 1)
            {
                HideHoverOverlay();
                return;
            }

            _hoverOverlay.SetBreadcrumbs(breadcrumbs, index =>
            {
                HideHoverOverlay();
                BreadcrumbClicked?.Invoke(index);
            });
            ShowHoverOverlay(_breadcrumb);
        }

        private void ShowHistoryOverlay(
            VisualElement anchor,
            IReadOnlyList<AssetItemGridHistoryEntry> entries,
            bool back)
        {
            if (entries == null || entries.Count == 0)
            {
                HideHoverOverlay();
                return;
            }

            var rows = new HistoryNavigationOverlayRowState[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var steps = i + 1;
                rows[i] = new HistoryNavigationOverlayRowState(
                    entries[i] != null ? string.Join(" / ", entries[i].Breadcrumbs) : string.Empty,
                    () =>
                    {
                        HideHoverOverlay();
                        if (back)
                        {
                            BackHistoryClicked?.Invoke(steps);
                        }
                        else
                        {
                            ForwardHistoryClicked?.Invoke(steps);
                        }
                    });
            }

            _hoverOverlay.SetRows(rows);
            ShowHoverOverlay(anchor);
        }

        private void ShowHoverOverlay(VisualElement anchor)
        {
            var root = FindOverlayRoot();
            if (root == null || anchor == null)
            {
                return;
            }

            if (_hoverOverlay.parent != root)
            {
                _hoverOverlay.RemoveFromHierarchy();
                root.Add(_hoverOverlay);
            }

            _hoverOverlayAnchor = anchor;
            _hoverOverlay.style.width = StyleKeyword.Auto;
            _hoverOverlay.style.maxWidth = Mathf.Min(
                _hoverOverlay.MaximumWidth,
                Mathf.Max(0f, root.resolvedStyle.width - 8f));
            _hoverOverlay.style.display = DisplayStyle.Flex;
            RepositionHoverOverlay();
        }

        private void RepositionHoverOverlay()
        {
            var root = _hoverOverlay.parent;
            if (root == null || _hoverOverlayAnchor == null ||
                _hoverOverlay.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            var overlayWidth = _hoverOverlay.resolvedStyle.width;
            if (float.IsNaN(overlayWidth))
            {
                return;
            }

            var rootPosition = root.worldBound.position;
            var anchorX = _hoverOverlayAnchor.worldBound.x - rootPosition.x;
            _hoverOverlay.style.left = Mathf.Clamp(
                anchorX,
                4f,
                Mathf.Max(4f, root.resolvedStyle.width - overlayWidth - 4f));
            _hoverOverlay.style.top = Mathf.Max(
                4f,
                _hoverOverlayAnchor.worldBound.yMax - rootPosition.y + 4f);
        }

        private VisualElement FindOverlayRoot()
        {
            VisualElement overlayRoot = null;
            for (var ancestor = this as VisualElement; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor.ClassListContains("ee4v-ui"))
                {
                    overlayRoot = ancestor;
                }
            }

            return overlayRoot;
        }

        private void HideHoverOverlay()
        {
            CancelPendingOverlayHide();
            _hoverOverlay.style.display = DisplayStyle.None;
            _hoverOverlayAnchor = null;
        }

        private void ScheduleOverlayHide()
        {
            CancelPendingOverlayHide();
            _pendingOverlayHide = schedule.Execute(HideHoverOverlay)
                .StartingIn(OverlayHideDelayMilliseconds);
        }

        private void CancelPendingOverlayHide()
        {
            if (_pendingOverlayHide == null)
            {
                return;
            }

            _pendingOverlayHide.Pause();
            _pendingOverlayHide = null;
        }

        private void RemoveHoverOverlay()
        {
            _hoverOverlay.RemoveFromHierarchy();
        }

        private void RefreshBreadcrumb(AssetItemGridHistoryEntry current)
        {
            _breadcrumb.Clear();
            if (current == null)
            {
                return;
            }

            var breadcrumbs = current.Breadcrumbs;
            if (breadcrumbs.Count == 0)
            {
                return;
            }

            var index = breadcrumbs.Count - 1;
            var item = CreateBreadcrumbButton(breadcrumbs[index], index, true);
            item.RegisterCallback<PointerEnterEvent>(_ =>
            {
                CancelPendingOverlayHide();
                ShowBreadcrumbOverlay();
            });
            item.RegisterCallback<PointerLeaveEvent>(_ => ScheduleOverlayHide());
            _breadcrumb.Add(item);
        }

        private static Button CreateNavigationButton(string text, string tooltip)
        {
            var button = new Button
            {
                text = text,
                tooltip = tooltip ?? string.Empty
            };
            button.AddToClassList(IconButtonClassName);
            button.focusable = false;
            return button;
        }

        private Button CreateBreadcrumbButton(string text, int index, bool current)
        {
            var button = new Button(() => BreadcrumbClicked?.Invoke(index));
            button.AddToClassList(BreadcrumbItemClassName);
            button.EnableInClassList(BreadcrumbItemCurrentClassName, current);
            button.focusable = false;

            var label = UiTextFactory.Create(text, UiClassNames.HistoryNavigationBreadcrumbItemLabel);
            label.EnableInClassList(BreadcrumbItemLabelCurrentClassName, current);
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            label.pickingMode = PickingMode.Ignore;
            if (current)
            {
                label.SetColor(UiColorTokens.TextSoft);
            }

            button.Add(label);
            return button;
        }
    }

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

    internal sealed class HistoryNavigationOverlay : VisualElement
    {
        private const float HistoryMaximumWidth = 420f;
        private const float BreadcrumbMaximumWidth = 600f;
        private const string RootClassName = "ee4v-ui-history-navigation-overlay";
        private const string BreadcrumbClassName = "ee4v-ui-history-navigation-overlay--breadcrumb";
        private const string ItemClassName = "ee4v-ui-history-navigation-overlay__item";
        private int _maximumVisibleRows;

        public HistoryNavigationOverlay(int maximumVisibleRows = 5)
        {
            AddToClassList(RootClassName);
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.None;
            SetMaximumVisibleRows(maximumVisibleRows);
        }

        public float MaximumWidth { get; private set; } = HistoryMaximumWidth;

        public void SetMaximumVisibleRows(int value)
        {
            _maximumVisibleRows = Mathf.Clamp(value, 1, 20);
        }

        public void SetRows(IReadOnlyList<HistoryNavigationOverlayRowState> rows)
        {
            Clear();
            EnableInClassList(BreadcrumbClassName, false);
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

        public void SetBreadcrumbs(IReadOnlyList<string> breadcrumbs, Action<int> onSelected)
        {
            Clear();
            EnableInClassList(BreadcrumbClassName, true);
            if (breadcrumbs == null)
            {
                MaximumWidth = BreadcrumbMaximumWidth;
                return;
            }

            for (var i = 0; i < breadcrumbs.Count; i++)
            {
                if (i > 0)
                {
                    var separator = UiTextFactory.Create("/", UiClassNames.HistoryNavigationOverlaySeparator);
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

        private static Button CreateRow(HistoryNavigationOverlayRowState state)
        {
            state = state ?? new HistoryNavigationOverlayRowState(string.Empty);
            var button = new Button(state.Action);
            button.AddToClassList(ItemClassName);
            button.SetEnabled(state.Action != null);
            button.focusable = false;

            var label = UiTextFactory.Create(state.Label, UiClassNames.HistoryNavigationOverlayRow);
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            label.pickingMode = PickingMode.Ignore;
            label.SetColor(state.Action != null ? UiColorTokens.TextPrimary : UiColorTokens.TextMuted);
            button.Add(label);
            return button;
        }
    }
}
