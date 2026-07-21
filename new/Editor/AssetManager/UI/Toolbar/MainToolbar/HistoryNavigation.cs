using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class HistoryNavigation : VisualElement
    {
        private const string RootClassName = "ee4v-ui-history-navigation";
        private const string NavGroupClassName = "ee4v-ui-history-navigation__nav-group";
        private const string NavDividerClassName = "ee4v-ui-history-navigation__nav-divider";
        private const string IconButtonClassName = "ee4v-ui-history-navigation__icon-button";
        private const string BreadcrumbClassName = "ee4v-ui-history-navigation__breadcrumb";
        private const string BreadcrumbItemClassName = "ee4v-ui-history-navigation__breadcrumb-item";
        private const string BreadcrumbItemCurrentClassName = "ee4v-ui-history-navigation__breadcrumb-item--current";
        private const string BreadcrumbItemLabelClassName = "ee4v-ui-history-navigation__breadcrumb-item-label";
        private const string BreadcrumbItemLabelCurrentClassName = "ee4v-ui-history-navigation__breadcrumb-item-label--current";
        private const string BreadcrumbSeparatorClassName = "ee4v-ui-history-navigation__breadcrumb-separator";
        private readonly Button _backButton;
        private readonly Button _forwardButton;
        private readonly VisualElement _breadcrumb;

        public HistoryNavigation()
        {
            AddToClassList(RootClassName);

            var navGroup = new VisualElement();
            navGroup.AddToClassList(NavGroupClassName);

            _backButton = CreateNavigationButton("‹", I18N.Get("assetManager.mainToolbar.history.back"));
            _backButton.clicked += () => BackClicked?.Invoke();
            navGroup.Add(_backButton);

            var divider = new VisualElement();
            divider.AddToClassList(NavDividerClassName);
            navGroup.Add(divider);

            _forwardButton = CreateNavigationButton("›", I18N.Get("assetManager.mainToolbar.history.forward"));
            _forwardButton.clicked += () => ForwardClicked?.Invoke();
            navGroup.Add(_forwardButton);

            Add(navGroup);

            _breadcrumb = new VisualElement();
            _breadcrumb.AddToClassList(BreadcrumbClassName);
            Add(_breadcrumb);
        }

        public event Action BackClicked;

        public event Action ForwardClicked;

        public event Action<int> BreadcrumbClicked;

        public void SetState(AssetItemGridHistoryState state)
        {
            state = state ?? new AssetItemGridHistoryState(null, false, false);
            _backButton.SetEnabled(state.CanGoBack);
            _forwardButton.SetEnabled(state.CanGoForward);
            RefreshBreadcrumb(state.Current);
        }

        private void RefreshBreadcrumb(AssetItemGridHistoryEntry current)
        {
            _breadcrumb.Clear();
            if (current == null)
            {
                return;
            }

            var breadcrumbs = current.Breadcrumbs;
            for (var i = 0; i < breadcrumbs.Count; i++)
            {
                if (i > 0)
                {
                    var separator = UiTextFactory.Create("/", BreadcrumbSeparatorClassName);
                    separator.SetColor(UiColorTokens.TextMuted);
                    _breadcrumb.Add(separator);
                }

                var index = i;
                var item = CreateBreadcrumbButton(
                    breadcrumbs[i],
                    index,
                    index + 1 >= breadcrumbs.Count);
                _breadcrumb.Add(item);
            }
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

            var label = UiTextFactory.Create(text, BreadcrumbItemLabelClassName);
            label.EnableInClassList(BreadcrumbItemLabelCurrentClassName, current);
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            label.pickingMode = PickingMode.Ignore;
            if (current)
            {
                label.SetColor(UiColorTokens.TextOnState);
            }

            button.Add(label);
            return button;
        }
    }
}
