using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum MessageSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class MessagePanelState
    {
        public MessagePanelState(string title, string message = null,
            MessageSeverity severity = MessageSeverity.Error, IReadOnlyList<string> details = null)
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
            Details = details?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
                ?? Array.Empty<string>();
        }

        public string Title { get; }
        public string Message { get; }
        public MessageSeverity Severity { get; }
        public IReadOnlyList<string> Details { get; }
    }

    public sealed class MessagePanel : VisualElement
    {
        private const string RootClassName = "ee4v-ui-message-panel";
        private readonly Icon _icon;
        private readonly ScrollView _details;

        public MessagePanel(MessagePanelState state = null)
        {
            AddToClassList(RootClassName);
            _icon = new Icon();
            _icon.AddToClassList(RootClassName + "__icon");
            var content = new VisualElement();
            content.AddToClassList(RootClassName + "__content");
            TitleText = UiTextFactory.Create(string.Empty, UiClassNames.SectionTitle,
                RootClassName + "__title");
            TitleText.SetWhiteSpace(WhiteSpace.Normal);
            MessageText = UiTextFactory.Create(string.Empty, RootClassName + "__message");
            MessageText.SetWhiteSpace(WhiteSpace.Normal);
            _details = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            _details.AddToClassList(RootClassName + "__details");
            content.Add(TitleText);
            content.Add(MessageText);
            content.Add(_details);
            Add(_icon);
            Add(content);
            SetState(state);
        }

        public UiTextElement TitleText { get; }
        public UiTextElement MessageText { get; }

        public void SetState(MessagePanelState state)
        {
            state = state ?? new MessagePanelState(string.Empty);
            var hasTitle = !string.IsNullOrWhiteSpace(state.Title);
            var hasMessage = !string.IsNullOrWhiteSpace(state.Message);
            EnableInClassList(RootClassName + "--info", state.Severity == MessageSeverity.Info);
            EnableInClassList(RootClassName + "--warning", state.Severity == MessageSeverity.Warning);
            EnableInClassList(RootClassName + "--error", state.Severity == MessageSeverity.Error);
            var color = state.Severity == MessageSeverity.Error ? UiColorTokens.StatusFailedText
                : state.Severity == MessageSeverity.Warning ? UiColorTokens.StatusRunningText
                : UiColorTokens.StatusSkippedText;
            _icon.SetState(FluentUiIcons.CreateState("info.png", UiSizeTokens.Size18, tintColor: color));
            TitleText.SetColor(color);
            TitleText.SetText(state.Title);
            TitleText.style.display = hasTitle ? DisplayStyle.Flex : DisplayStyle.None;
            MessageText.SetText(state.Message);
            MessageText.style.display = hasMessage ? DisplayStyle.Flex : DisplayStyle.None;
            _details.Clear();
            foreach (var detail in state.Details)
            {
                var text = UiTextFactory.Create(detail, RootClassName + "__detail");
                text.SetWhiteSpace(WhiteSpace.Normal);
                _details.Add(text);
            }
            _details.style.display = state.Details.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _details.scrollOffset = Vector2.zero;
            style.display = hasTitle || hasMessage || state.Details.Count > 0
                ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
