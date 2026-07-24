using System;
using System.Collections.Generic;
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
        private readonly HistoryNavigationOverlay _overlay;
        private IVisualElementScheduledItem _pendingOverlayHide;
        private int _maximumVisibleHistoryRows;
        private AssetItemGridHistoryState _state;

        public HistoryNavigation(int maximumVisibleHistoryRows = 5)
        {
            AddToClassList(RootClassName);

            _backButton = CreateNavigationButton("\u2190");
            _backButton.clicked += () => BackClicked?.Invoke();
            _backButton.RegisterCallback<PointerEnterEvent>(_ =>
                CancelPendingOverlayHide());
            _backButton.RegisterCallback<ContextClickEvent>(evt =>
            {
                CancelPendingOverlayHide();
                ShowHistoryOverlay(_backButton, _state.BackEntries, true);
                evt.StopPropagation();
            });
            _backButton.RegisterCallback<PointerLeaveEvent>(_ => ScheduleOverlayHide());
            Add(_backButton);

            _forwardButton = CreateNavigationButton("\u2192");
            _forwardButton.clicked += () => ForwardClicked?.Invoke();
            _forwardButton.RegisterCallback<PointerEnterEvent>(_ =>
                CancelPendingOverlayHide());
            _forwardButton.RegisterCallback<ContextClickEvent>(evt =>
            {
                CancelPendingOverlayHide();
                ShowHistoryOverlay(_forwardButton, _state.ForwardEntries, false);
                evt.StopPropagation();
            });
            _forwardButton.RegisterCallback<PointerLeaveEvent>(_ => ScheduleOverlayHide());
            Add(_forwardButton);

            _breadcrumb = new VisualElement();
            _breadcrumb.AddToClassList(BreadcrumbClassName);
            Add(_breadcrumb);

            _overlay = new HistoryNavigationOverlay();
            _overlay.RegisterCallback<PointerEnterEvent>(_ => CancelPendingOverlayHide());
            _overlay.RegisterCallback<PointerLeaveEvent>(_ => ScheduleOverlayHide());
            SetMaximumVisibleRows(maximumVisibleHistoryRows);
            _state = new AssetItemGridHistoryState(null, false, false);
            RegisterCallback<DetachFromPanelEvent>(_ => RemoveOverlay());
        }

        public event Action BackClicked;

        public event Action ForwardClicked;

        public event Action<int> BackHistoryClicked;

        public event Action<int> ForwardHistoryClicked;

        public event Action<int> BreadcrumbClicked;

        public void SetMaximumVisibleRows(int value)
        {
            _maximumVisibleHistoryRows = Mathf.Clamp(value, 1, 20);
        }

        public void SetState(AssetItemGridHistoryState state)
        {
            _state = state ?? new AssetItemGridHistoryState(null, false, false);
            HideOverlay();
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
                HideOverlay();
                return;
            }

            _overlay.SetBreadcrumbs(breadcrumbs, index =>
            {
                HideOverlay();
                BreadcrumbClicked?.Invoke(index);
            });
            ShowOverlay(_breadcrumb);
        }

        private void ShowHistoryOverlay(
            VisualElement anchor,
            IReadOnlyList<AssetItemGridHistoryEntry> entries,
            bool back)
        {
            if (entries == null || entries.Count == 0)
            {
                HideOverlay();
                return;
            }

            var rows = new HistoryNavigationOverlayRowState[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var steps = i + 1;
                rows[i] = new HistoryNavigationOverlayRowState(
                    FormatHistoryLabel(entries[i]),
                    () =>
                    {
                        HideOverlay();
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

            HideOverlay();
            HistoryNavigationMenu.Show(
                anchor,
                rows,
                _maximumVisibleHistoryRows);
        }

        private static string FormatHistoryLabel(
            AssetItemGridHistoryEntry entry)
        {
            var breadcrumbs = entry != null
                ? entry.Breadcrumbs
                : null;
            return breadcrumbs != null && breadcrumbs.Count > 0
                ? breadcrumbs[breadcrumbs.Count - 1]
                : string.Empty;
        }

        private void ShowOverlay(VisualElement anchor)
        {
            var root = FindOverlayRoot();
            if (root == null || anchor == null)
            {
                return;
            }

            _overlay.Show(root, anchor);
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

        private void HideOverlay()
        {
            CancelPendingOverlayHide();
            _overlay.Hide();
        }

        private void ScheduleOverlayHide()
        {
            CancelPendingOverlayHide();
            _pendingOverlayHide = schedule.Execute(HideOverlay)
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

        private void RemoveOverlay()
        {
            _overlay.RemoveFromHierarchy();
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

        private static Button CreateNavigationButton(string text)
        {
            var button = new Button
            {
                text = text
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

}
