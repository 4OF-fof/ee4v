using System;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
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
        private readonly HistoryNavigation _historyNavigation;
        private readonly NumericSlider _itemSizeSlider;
        private readonly SearchField _searchField;
        private readonly int _configuredMinimumGridSize;
        private readonly int _maximumGridSize;

        public MainToolbar(
            int initialGridSize = 7,
            int historyOverlayMaximumItems = 5,
            AssetItemGridHistoryState historyState = null)
        {
            AddToClassList(RootClassName);

            Content = new VisualElement();
            Content.AddToClassList(ContentClassName);
            Add(Content);

            var leading = new VisualElement();
            leading.AddToClassList(LeadingClassName);
            _historyNavigation = new HistoryNavigation(historyOverlayMaximumItems);
            _historyNavigation.BackClicked += () => BackClicked?.Invoke();
            _historyNavigation.ForwardClicked += () => ForwardClicked?.Invoke();
            _historyNavigation.BackHistoryClicked += steps => BackHistoryClicked?.Invoke(steps);
            _historyNavigation.ForwardHistoryClicked += steps => ForwardHistoryClicked?.Invoke(steps);
            _historyNavigation.BreadcrumbClicked += index => BreadcrumbClicked?.Invoke(index);
            _historyNavigation.SetState(historyState);
            leading.Add(_historyNavigation);
            Content.Add(leading);

            var preferences = AssetManagerUiDependencies.Preferences;
            _configuredMinimumGridSize = preferences.MinimumItemsPerRow;
            _maximumGridSize = preferences.MaximumItemsPerRow;
            _itemSizeSlider = new NumericSlider(new NumericSliderState(
                initialGridSize,
                _configuredMinimumGridSize,
                _maximumGridSize,
                1f));
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
                UiFluentIcon.Filter,
                I18N.Get("assetManager.mainToolbar.filter"),
                () => FilterClicked?.Invoke()));
            actions.Add(CreateIconButton(
                UiFluentIcon.Options,
                I18N.Get("assetManager.mainToolbar.sort"),
                () => SortClicked?.Invoke()));

            var searchTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.search.tooltip");
            var clearTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.clear.tooltip");
            _searchField = new SearchField(
                new SearchFieldState(
                    placeholder: I18N.Get(
                        "assetManager.mainToolbar.searchPlaceholder"),
                    searchTooltip: searchTooltip,
                    clearTooltip: clearTooltip,
                    searchIconState:
                    IconState.FromFluentIcon(
                        UiFluentIcon.Search,
                        UiSizeTokens.Size14,
                        searchTooltip),
                    clearIconState:
                    IconState.FromFluentIcon(
                        UiFluentIcon.Dismiss,
                        UiSizeTokens.Size10,
                        clearTooltip)));
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

        public event Action BackClicked;

        public event Action ForwardClicked;

        public event Action<int> BackHistoryClicked;

        public event Action<int> ForwardHistoryClicked;

        public event Action<int> BreadcrumbClicked;

        internal int GridSizeValue
        {
            get { return Mathf.RoundToInt(_itemSizeSlider.Value); }
        }

        internal int MinimumGridSizeValue
        {
            get { return Mathf.RoundToInt(_itemSizeSlider.MinValue); }
        }

        internal void SetGridSizeValue(int value)
        {
            _itemSizeSlider.SetValueWithoutNotify(value);
        }

        internal void SetMinimumGridSize(int value)
        {
            var minimumGridSize = Mathf.Clamp(
                value,
                _configuredMinimumGridSize,
                _maximumGridSize);
            _itemSizeSlider.SetState(new NumericSliderState(
                _itemSizeSlider.Value,
                minimumGridSize,
                _maximumGridSize,
                1f));
        }

        internal void SetGridSizeVisible(bool visible)
        {
            _itemSizeSlider.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        internal void SetHistoryState(AssetItemGridHistoryState state)
        {
            _historyNavigation.SetState(state);
        }

        internal void SetHistoryOverlayMaximumItems(int value)
        {
            _historyNavigation.SetMaximumVisibleRows(value);
        }

        private static UiButton CreateIconButton(
            UiFluentIcon fluentIcon,
            string tooltip,
            Action clicked)
        {
            var button = new UiButton(
                new UiButtonState(
                    tooltip: tooltip,
                    iconState: IconState.FromFluentIcon(
                        fluentIcon,
                        size: UiSizeTokens.Size14,
                        tooltip: tooltip),
                    variant: UiButtonVariant.Ghost,
                    size: UiButtonSize.Compact),
                clicked);
            button.focusable = false;
            button.AddToClassList(IconButtonClassName);
            return button;
        }
    }
}
