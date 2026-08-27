using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum UiStatusTone
    {
        Idle,
        Running,
        Passed,
        Failed,
        Skipped,
        Inconclusive
    }

    public sealed class BadgeState
    {
        public BadgeState(string text, UiStatusTone? tone = null)
        {
            Text = text ?? string.Empty;
            Tone = tone;
        }

        public string Text { get; }

        public UiStatusTone? Tone { get; }
    }

    public sealed class Badge : VisualElement
    {
        private const string StatusClassName =
            "ee4v-ui-badge--status";
        private const string IdleClassName =
            "ee4v-ui-badge--idle";
        private const string RunningClassName =
            "ee4v-ui-badge--running";
        private const string PassedClassName =
            "ee4v-ui-badge--passed";
        private const string FailedClassName =
            "ee4v-ui-badge--failed";
        private const string SkippedClassName =
            "ee4v-ui-badge--skipped";
        private const string InconclusiveClassName =
            "ee4v-ui-badge--inconclusive";
        private BadgeState _state;

        public Badge(string text = null)
            : this(new BadgeState(text))
        {
        }

        public Badge(BadgeState state)
        {
            AddToClassList("ee4v-ui-badge");
            TextElement = UiTextFactory.Create(
                string.Empty,
                UiClassNames.Badge,
                "ee4v-ui-badge__text");
            TextElement.pickingMode = PickingMode.Ignore;
            Add(TextElement);
            SetState(state);
        }

        public UiTextElement TextElement { get; }

        public void SetText(string text)
        {
            SetState(new BadgeState(text, _state?.Tone));
        }

        public void SetState(BadgeState state)
        {
            _state = state ?? new BadgeState(string.Empty);
            var tone = _state.Tone;

            TextElement.SetText(_state.Text);
            style.display = string.IsNullOrWhiteSpace(_state.Text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            EnableInClassList(StatusClassName, tone.HasValue);
            EnableInClassList(IdleClassName, tone == UiStatusTone.Idle);
            EnableInClassList(RunningClassName, tone == UiStatusTone.Running);
            EnableInClassList(PassedClassName, tone == UiStatusTone.Passed);
            EnableInClassList(FailedClassName, tone == UiStatusTone.Failed);
            EnableInClassList(SkippedClassName, tone == UiStatusTone.Skipped);
            EnableInClassList(
                InconclusiveClassName,
                tone == UiStatusTone.Inconclusive);
            TextElement.SetColor(
                tone.HasValue
                    ? ToneTextColor(tone.Value)
                    : UiColorTokens.TextMuted);
        }

        private static Color ToneTextColor(UiStatusTone tone)
        {
            switch (tone)
            {
                case UiStatusTone.Running:
                    return UiColorTokens.StatusRunningText;
                case UiStatusTone.Passed:
                    return UiColorTokens.StatusPassedText;
                case UiStatusTone.Failed:
                    return UiColorTokens.StatusFailedText;
                case UiStatusTone.Skipped:
                    return UiColorTokens.StatusSkippedText;
                case UiStatusTone.Inconclusive:
                    return UiColorTokens.StatusInconclusiveText;
                default:
                    return UiColorTokens.StatusIdleText;
            }
        }
    }
}
