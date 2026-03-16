using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ReferenceRowState
    {
        public ReferenceRowState(
            string primaryText,
            string secondaryText = "",
            string actionLabel = "",
            Action onAction = null,
            bool actionEnabled = true)
        {
            PrimaryText = primaryText ?? string.Empty;
            SecondaryText = secondaryText ?? string.Empty;
            ActionLabel = actionLabel ?? string.Empty;
            OnAction = onAction;
            ActionEnabled = actionEnabled;
        }

        public string PrimaryText { get; }

        public string SecondaryText { get; }

        public string ActionLabel { get; }

        public Action OnAction { get; }

        public bool ActionEnabled { get; }
    }

    internal sealed class ReferenceRow : VisualElement
    {
        private readonly Button _actionButton;
        private readonly UiTextElement _primaryLabel;
        private readonly UiTextElement _secondaryLabel;
        private Action _currentAction;

        public ReferenceRow(ReferenceRowState state = null)
        {
            AddToClassList(UiClassNames.ReferenceRow);

            _actionButton = new Button();
            _actionButton.AddToClassList(UiClassNames.ReferenceAction);
            _actionButton.style.minWidth = UiTokens.ReferenceActionWidth;

            var textStack = new VisualElement();
            textStack.AddToClassList(UiClassNames.ReferenceText);

            _primaryLabel = UiTextFactory.Create(string.Empty, UiClassNames.ReferencePrimary);

            _secondaryLabel = UiTextFactory.Create(string.Empty, UiClassNames.ReferenceSecondary);

            textStack.Add(_primaryLabel);
            textStack.Add(_secondaryLabel);

            Add(_actionButton);
            Add(textStack);

            SetState(state ?? new ReferenceRowState(string.Empty));
        }

        public void SetState(ReferenceRowState state)
        {
            state = state ?? new ReferenceRowState(string.Empty);

            if (_currentAction != null)
            {
                _actionButton.clicked -= _currentAction;
            }

            _currentAction = state.OnAction;
            if (_currentAction != null)
            {
                _actionButton.clicked += _currentAction;
            }

            _actionButton.text = state.ActionLabel;
            _actionButton.style.display = string.IsNullOrWhiteSpace(state.ActionLabel) ? DisplayStyle.None : DisplayStyle.Flex;
            _actionButton.SetEnabled(state.ActionEnabled && _currentAction != null);

            _primaryLabel.SetText(state.PrimaryText);
            _secondaryLabel.SetText(state.SecondaryText);
        }
    }
}
