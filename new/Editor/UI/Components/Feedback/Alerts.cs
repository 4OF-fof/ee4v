using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiBannerTone
    {
        Info,
        Warning,
        Error
    }

    internal sealed class AlertsState
    {
        public AlertsState(UiBannerTone tone, string title, string message)
        {
            Tone = tone;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public UiBannerTone Tone { get; }

        public string Title { get; }

        public string Message { get; }
    }

    internal sealed class Alerts : VisualElement
    {
        private readonly UiTextElement _titleLabel;
        private readonly UiTextElement _messageLabel;

        public Alerts(AlertsState state = null)
        {
            AddToClassList(UiClassNames.Banner);

            _titleLabel = UiTextFactory.Create(string.Empty, UiClassNames.BannerTitle);

            _messageLabel = UiTextFactory.Create(string.Empty, UiClassNames.BannerMessage);

            Add(_titleLabel);
            Add(_messageLabel);

            SetState(state ?? new AlertsState(UiBannerTone.Info, string.Empty, string.Empty));
        }

        public void SetState(AlertsState state)
        {
            state = state ?? new AlertsState(UiBannerTone.Info, string.Empty, string.Empty);

            _titleLabel.SetText(state.Title);

            _messageLabel.SetText(state.Message);

            EnableInClassList(UiClassNames.BannerToneInfo, state.Tone == UiBannerTone.Info);
            EnableInClassList(UiClassNames.BannerToneWarning, state.Tone == UiBannerTone.Warning);
            EnableInClassList(UiClassNames.BannerToneError, state.Tone == UiBannerTone.Error);
        }
    }
}
