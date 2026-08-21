using UnityEngine.UIElements;

namespace Ee4v.UI
{
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
        public Badge(BadgeState state = null)
        {
            AddToClassList("ee4v-ui-badge");
            TextElement = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-ui-badge__text");
            TextElement.pickingMode = PickingMode.Ignore;
            Add(TextElement);
            SetState(state ?? new BadgeState(string.Empty));
        }

        public UiTextElement TextElement { get; }

        public void SetState(BadgeState state)
        {
            state = state ?? new BadgeState(string.Empty);
            TextElement.SetText(state.Text);
            style.display = string.IsNullOrWhiteSpace(state.Text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            EnableInClassList(
                "ee4v-ui-badge--running",
                state.Tone == UiStatusTone.Running);
            EnableInClassList(
                "ee4v-ui-badge--passed",
                state.Tone == UiStatusTone.Passed);
            EnableInClassList(
                "ee4v-ui-badge--failed",
                state.Tone == UiStatusTone.Failed);
        }
    }
}
