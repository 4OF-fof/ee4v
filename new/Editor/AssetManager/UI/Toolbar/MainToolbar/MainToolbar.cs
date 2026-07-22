using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class MainToolbar : VisualElement
    {
        private const string RootClassName = "ee4v-ui-main-toolbar";
        private const string ContentClassName = "ee4v-ui-main-toolbar__content";
        private const string LeadingClassName = "ee4v-ui-main-toolbar__leading";
        private const string ActionsClassName = "ee4v-ui-main-toolbar__actions";
        private const string SliderClassName = "ee4v-ui-main-toolbar__slider";
        private const string IconButtonClassName = "ee4v-ui-main-toolbar__icon-button";
        private const string SearchClassName = "ee4v-ui-main-toolbar__search";
        private readonly NumericSlider _itemSizeSlider;
        private readonly SearchField _searchField;

        public MainToolbar(
            MainView mainView = null,
            int initialGridSize = 7,
            int historyOverlayMaximumItems = 5)
        {
            AddToClassList(RootClassName);

            Content = new VisualElement();
            Content.AddToClassList(ContentClassName);
            Add(Content);

            var leading = new VisualElement();
            leading.AddToClassList(LeadingClassName);
            if (mainView != null)
            {
                var historyNavigation = new HistoryNavigation(historyOverlayMaximumItems);
                historyNavigation.BackClicked += mainView.GoBack;
                historyNavigation.ForwardClicked += mainView.GoForward;
                historyNavigation.BackHistoryClicked += steps => mainView.GoBack(steps);
                historyNavigation.ForwardHistoryClicked += steps => mainView.GoForward(steps);
                historyNavigation.BreadcrumbClicked += mainView.GoToBreadcrumb;
                mainView.History.Changed += historyNavigation.SetState;
                mainView.HistoryOverlayMaximumItemsChanged += historyNavigation.SetMaximumVisibleRows;
                historyNavigation.SetState(mainView.History.State);
                leading.Add(historyNavigation);
            }
            Content.Add(leading);

            _itemSizeSlider = new NumericSlider(new NumericSliderState(initialGridSize, 1f, 12f, 1f));
            _itemSizeSlider.AddToClassList(SliderClassName);
            _itemSizeSlider.tooltip = I18N.Get("assetManager.mainToolbar.gridSize");
            _itemSizeSlider.ValueChanged += value =>
            {
                GridSizeChanged?.Invoke((int)value);
            };
            Content.Add(_itemSizeSlider);

            var actions = new VisualElement();
            actions.AddToClassList(ActionsClassName);

            actions.Add(CreateIconButton(
                UiBuiltinIcon.Filter,
                I18N.Get("assetManager.mainToolbar.filter"),
                () => FilterClicked?.Invoke()));
            actions.Add(CreateIconButton(
                UiBuiltinIcon.Sort,
                I18N.Get("assetManager.mainToolbar.sort"),
                () => SortClicked?.Invoke()));

            _searchField = new SearchField(new SearchFieldState(
                placeholder: I18N.Get("assetManager.mainToolbar.searchPlaceholder")));
            _searchField.AddToClassList(SearchClassName);
            _searchField.ValueChanged += value =>
            {
                SearchTextChanged?.Invoke(value);
            };
            actions.Add(_searchField);

            Content.Add(actions);
        }

        public VisualElement Content { get; }

        public event Action<int> GridSizeChanged;

        public event Action FilterClicked;

        public event Action SortClicked;

        public event Action<string> SearchTextChanged;

        private static Button CreateIconButton(UiBuiltinIcon builtinIcon, string tooltip, Action clicked)
        {
            var button = new Button(clicked)
            {
                tooltip = tooltip ?? string.Empty,
                focusable = false
            };
            button.AddToClassList(IconButtonClassName);
            button.Add(new Icon(IconState.FromBuiltinIcon(builtinIcon, size: 14f, tooltip: tooltip)));
            return button;
        }
    }
}
