using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiStatusTone
    {
        Idle,
        Running,
        Passed,
        Failed,
        Skipped,
        Inconclusive
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
        private const string IdleClassName = "ee4v-ui-status--idle";
        private const string RunningClassName = "ee4v-ui-status--running";
        private const string PassedClassName = "ee4v-ui-status--passed";
        private const string FailedClassName = "ee4v-ui-status--failed";
        private const string SkippedClassName = "ee4v-ui-status--skipped";
        private const string InconclusiveClassName = "ee4v-ui-status--inconclusive";
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

            _textElement.EnableInClassList(IdleClassName, state.Tone == UiStatusTone.Idle);
            _textElement.EnableInClassList(RunningClassName, state.Tone == UiStatusTone.Running);
            _textElement.EnableInClassList(PassedClassName, state.Tone == UiStatusTone.Passed);
            _textElement.EnableInClassList(FailedClassName, state.Tone == UiStatusTone.Failed);
            _textElement.EnableInClassList(SkippedClassName, state.Tone == UiStatusTone.Skipped);
            _textElement.EnableInClassList(InconclusiveClassName, state.Tone == UiStatusTone.Inconclusive);
        }
    }
}
