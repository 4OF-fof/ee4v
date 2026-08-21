using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class InlineMessageState
    {
        public InlineMessageState(
            string text,
            UiStatusTone tone = UiStatusTone.Idle,
            IconState icon = null)
        {
            Text = text ?? string.Empty;
            Tone = tone;
            Icon = icon;
        }

        public string Text { get; }
        public UiStatusTone Tone { get; }
        public IconState Icon { get; }
    }

    public class InlineMessage : VisualElement
    {
        public InlineMessage(InlineMessageState state = null)
        {
            AddToClassList("ee4v-ui-inline-message");
            IconElement = new Icon();
            IconElement.AddToClassList("ee4v-ui-inline-message__icon");
            TextElement = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-ui-inline-message__text");
            TextElement.SetWhiteSpace(WhiteSpace.Normal);
            Add(IconElement);
            Add(TextElement);
            SetState(state ?? new InlineMessageState(string.Empty));
        }

        public Icon IconElement { get; }
        public UiTextElement TextElement { get; }

        public void SetState(InlineMessageState state)
        {
            state = state ?? new InlineMessageState(string.Empty);
            var hasText = !string.IsNullOrWhiteSpace(state.Text);
            var hasIcon = state.Icon != null;
            if (hasIcon)
            {
                IconElement.SetState(state.Icon);
            }

            IconElement.style.display = hasIcon
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            TextElement.SetText(state.Text);
            style.display = hasText
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            EnableInClassList(
                "ee4v-ui-inline-message--failed",
                state.Tone == UiStatusTone.Failed);
            EnableInClassList(
                "ee4v-ui-inline-message--passed",
                state.Tone == UiStatusTone.Passed);
            EnableInClassList(
                "ee4v-ui-inline-message--running",
                state.Tone == UiStatusTone.Running);
        }
    }
}
