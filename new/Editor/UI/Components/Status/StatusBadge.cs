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

    internal sealed class StatusBadge : Label
    {
        public StatusBadge(StatusBadgeState state = null)
        {
            AddToClassList(UiClassNames.StatusBadge);
            SetState(state ?? new StatusBadgeState(string.Empty, UiStatusTone.Idle));
        }

        public void SetState(StatusBadgeState state)
        {
            state = state ?? new StatusBadgeState(string.Empty, UiStatusTone.Idle);
            text = state.Text;
            style.display = string.IsNullOrWhiteSpace(state.Text) ? DisplayStyle.None : DisplayStyle.Flex;

            EnableInClassList(UiClassNames.StatusIdle, state.Tone == UiStatusTone.Idle);
            EnableInClassList(UiClassNames.StatusRunning, state.Tone == UiStatusTone.Running);
            EnableInClassList(UiClassNames.StatusPassed, state.Tone == UiStatusTone.Passed);
            EnableInClassList(UiClassNames.StatusFailed, state.Tone == UiStatusTone.Failed);
            EnableInClassList(UiClassNames.StatusSkipped, state.Tone == UiStatusTone.Skipped);
            EnableInClassList(UiClassNames.StatusInconclusive, state.Tone == UiStatusTone.Inconclusive);
        }
    }
}
