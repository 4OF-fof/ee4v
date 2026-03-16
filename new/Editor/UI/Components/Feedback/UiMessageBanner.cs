using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiBannerTone
    {
        Info,
        Warning,
        Error
    }

    internal sealed class UiMessageBannerState
    {
        public UiMessageBannerState(UiBannerTone tone, string title, string message)
        {
            Tone = tone;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public UiBannerTone Tone { get; }

        public string Title { get; }

        public string Message { get; }
    }

    internal sealed class UiMessageBanner : VisualElement
    {
        private readonly Label _titleLabel;
        private readonly Label _messageLabel;

        public UiMessageBanner(UiMessageBannerState state = null)
        {
            AddToClassList(UiClassNames.Banner);

            _titleLabel = new Label();
            _titleLabel.AddToClassList(UiClassNames.BannerTitle);

            _messageLabel = new Label();
            _messageLabel.AddToClassList(UiClassNames.BannerMessage);

            Add(_titleLabel);
            Add(_messageLabel);

            SetState(state ?? new UiMessageBannerState(UiBannerTone.Info, string.Empty, string.Empty));
        }

        public void SetState(UiMessageBannerState state)
        {
            state = state ?? new UiMessageBannerState(UiBannerTone.Info, string.Empty, string.Empty);

            _titleLabel.text = state.Title;
            _titleLabel.style.display = string.IsNullOrWhiteSpace(state.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _messageLabel.text = state.Message;
            _messageLabel.style.display = string.IsNullOrWhiteSpace(state.Message) ? DisplayStyle.None : DisplayStyle.Flex;

            EnableInClassList(UiClassNames.BannerToneInfo, state.Tone == UiBannerTone.Info);
            EnableInClassList(UiClassNames.BannerToneWarning, state.Tone == UiBannerTone.Warning);
            EnableInClassList(UiClassNames.BannerToneError, state.Tone == UiBannerTone.Error);
        }
    }
}
