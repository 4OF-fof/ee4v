using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiEmptyStateState
    {
        public UiEmptyStateState(string title, string message)
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Title { get; }

        public string Message { get; }
    }

    internal sealed class UiEmptyState : VisualElement
    {
        private readonly Label _titleLabel;
        private readonly Label _messageLabel;

        public UiEmptyState(UiEmptyStateState state = null)
        {
            AddToClassList(UiClassNames.EmptyState);

            _titleLabel = new Label();
            _titleLabel.AddToClassList(UiClassNames.EmptyStateTitle);

            _messageLabel = new Label();
            _messageLabel.AddToClassList(UiClassNames.EmptyStateMessage);

            Add(_titleLabel);
            Add(_messageLabel);

            SetState(state ?? new UiEmptyStateState(string.Empty, string.Empty));
        }

        public void SetState(UiEmptyStateState state)
        {
            state = state ?? new UiEmptyStateState(string.Empty, string.Empty);

            _titleLabel.text = state.Title;
            _titleLabel.style.display = string.IsNullOrWhiteSpace(state.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _messageLabel.text = state.Message;
            _messageLabel.style.display = string.IsNullOrWhiteSpace(state.Message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
