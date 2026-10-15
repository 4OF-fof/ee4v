using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class SearchFieldState
    {
        public SearchFieldState(
            string value = null,
            string placeholder = null,
            string searchTooltip = null,
            string clearTooltip = null,
            IconState searchIconState = null,
            IconState clearIconState = null,
            bool searchActionEnabled = false)
        {
            Value = value ?? string.Empty;
            Placeholder = placeholder ?? string.Empty;
            SearchTooltip = searchTooltip ?? string.Empty;
            ClearTooltip = clearTooltip ?? string.Empty;
            SearchIconState =
                searchIconState ??
                FluentUiIcons.CreateState(
                    "search.png",
                    size: UiSizeTokens.Size14,
                    tooltip: SearchTooltip);
            ClearIconState =
                clearIconState ??
                FluentUiIcons.CreateState(
                    "dismiss.png",
                    size: UiSizeTokens.Size10,
                    tooltip: ClearTooltip);
            SearchActionEnabled = searchActionEnabled;
        }

        public string Value { get; }

        public string Placeholder { get; }

        public string SearchTooltip { get; }

        public string ClearTooltip { get; }

        public IconState SearchIconState { get; }

        public IconState ClearIconState { get; }

        public bool SearchActionEnabled { get; }
    }

    public sealed class SearchField : InputField
    {
        private const string RootClassName = "ee4v-ui-search-field";
        private const string HasValueClassName = "ee4v-ui-search-field--has-value";
        private const string FocusedClassName = "ee4v-ui-search-field--focused";
        private const string SearchActionClassName =
            "ee4v-ui-search-field__search-action";
        private const string SearchActionInteractiveClassName =
            "ee4v-ui-search-field__search-action--interactive";
        private const string InputHostClassName = "ee4v-ui-search-field__input-host";
        private const string InputClassName = "ee4v-ui-search-field__input";
        private const string PlaceholderClassName = "ee4v-ui-search-field__placeholder";
        private const string ClearClassName = "ee4v-ui-search-field__clear";
        private readonly UiButton _searchAction;
        private readonly UiButton _clearButton;
        private bool _searchActionEnabled;

        public SearchField(SearchFieldState state = null) : base(null, true)
        {
            state = state ?? new SearchFieldState();
            AddToClassList(RootClassName);

            _searchAction = new UiButton(
                string.Empty,
                RequestSearchAction,
                state.SearchTooltip,
                state.SearchIconState,
                UiButtonVariant.Ghost);
            _searchAction.AddToClassList(SearchActionClassName);

            FieldContainer.AddToClassList(InputHostClassName);
            TextInput.AddToClassList(InputClassName);
            PlaceholderLabel.AddToClassList(PlaceholderClassName);

            _clearButton = new UiButton(
                string.Empty,
                ClearValue,
                state.ClearTooltip,
                state.ClearIconState,
                UiButtonVariant.Ghost);
            _clearButton.AddToClassList(ClearClassName);

            Insert(0, _searchAction);
            Add(_clearButton);

            SetState(state);
        }

        public event Action SearchActionRequested;

        public VisualElement SearchActionAnchor
        {
            get { return _searchAction; }
        }

        public void SetState(SearchFieldState state)
        {
            state = state ?? new SearchFieldState();
            _searchAction.SetIcon(state.SearchIconState);
            _clearButton.SetIcon(state.ClearIconState);
            _searchActionEnabled = state.SearchActionEnabled;
            _searchAction.focusable = _searchActionEnabled;
            _searchAction.pickingMode = _searchActionEnabled
                ? PickingMode.Position
                : PickingMode.Ignore;
            _searchAction.EnableInClassList(
                SearchActionInteractiveClassName,
                _searchActionEnabled);
            _searchAction.tooltip = state.SearchTooltip;
            _clearButton.tooltip = state.ClearTooltip;
            SetValueWithoutNotify(state.Value);
            SetPlaceholder(state.Placeholder);
        }

        public void ClearValue()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return;
            }

            Value = string.Empty;
        }

        private void RequestSearchAction()
        {
            if (_searchActionEnabled)
            {
                SearchActionRequested?.Invoke();
            }
        }

        protected override bool HasValue => !string.IsNullOrWhiteSpace(Value);

        protected override void RefreshVisualState()
        {
            base.RefreshVisualState();
            EnableInClassList(HasValueClassName, HasValue);
            EnableInClassList(FocusedClassName, IsInputFocused);
            if (_clearButton != null)
            {
                _clearButton.style.display = HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
