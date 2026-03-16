using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiStatusTone
    {
        Idle,
        Running,
        Passed,
        Failed
    }

    internal sealed class StatusBadgeState
    {
        public StatusBadgeState(string text, UiStatusTone tone)
        {
            Text = text ?? string.Empty;
            Tone = tone;
        }

        public string Text { get; }

        public UiStatusTone Tone { get; }
    }

    internal sealed class StatusBadge : VisualElement
    {
        private readonly UiTextElement _textElement;

        public StatusBadge(StatusBadgeState state = null)
        {
            _textElement = UiTextFactory.Create(string.Empty, UiClassNames.StatusBadge);
            Add(_textElement);
            SetState(state ?? new StatusBadgeState(string.Empty, UiStatusTone.Idle));
        }

        public void SetState(StatusBadgeState state)
        {
            state = state ?? new StatusBadgeState(string.Empty, UiStatusTone.Idle);
            style.display = string.IsNullOrWhiteSpace(state.Text) ? DisplayStyle.None : DisplayStyle.Flex;
            _textElement.SetText(state.Text);

            _textElement.EnableInClassList(UiClassNames.StatusIdle, state.Tone == UiStatusTone.Idle);
            _textElement.EnableInClassList(UiClassNames.StatusRunning, state.Tone == UiStatusTone.Running);
            _textElement.EnableInClassList(UiClassNames.StatusPassed, state.Tone == UiStatusTone.Passed);
            _textElement.EnableInClassList(UiClassNames.StatusFailed, state.Tone == UiStatusTone.Failed);
        }
    }
}
