using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum ErrorScreenKind
    {
        Info,
        Loading,
        Error
    }

    internal sealed class ErrorScreenState
    {
        public ErrorScreenState(
            string message,
            ErrorScreenKind kind = ErrorScreenKind.Error)
        {
            Message = message ?? string.Empty;
            Kind = kind;
        }

        public string Message { get; }

        public ErrorScreenKind Kind { get; }
    }

    internal sealed class ErrorScreen : VisualElement
    {
        private const string RootClassName = "ee4v-ui-error-screen";
        private const string IconClassName =
            "ee4v-ui-error-screen__icon";
        private const string MessageClassName =
            "ee4v-ui-error-screen__message";
        private const float IconSize = 88f;
        private readonly Icon _icon;
        private readonly UiTextElement _message;

        public ErrorScreen(ErrorScreenState state = null)
        {
            AddToClassList(RootClassName);

            _icon = new Icon();
            _icon.AddToClassList(IconClassName);
            Add(_icon);

            _message = UiTextFactory.Create(
                string.Empty,
                UiClassNames.ErrorScreenMessage,
                MessageClassName);
            _message.SetWhiteSpace(WhiteSpace.Normal);
            Add(_message);

            SetState(
                state ??
                new ErrorScreenState(string.Empty));
        }

        public void SetState(ErrorScreenState state)
        {
            var nextState =
                state ??
                new ErrorScreenState(string.Empty);
            _icon.SetState(IconState.FromFluentIcon(
                ResolveIcon(nextState.Kind),
                IconSize));
            _message.SetText(nextState.Message);
        }

        private static UiFluentIcon ResolveIcon(
            ErrorScreenKind kind)
        {
            switch (kind)
            {
                case ErrorScreenKind.Info:
                    return UiFluentIcon.Info;
                case ErrorScreenKind.Loading:
                    return UiFluentIcon.ArrowClockwise;
                default:
                    return UiFluentIcon.ErrorCircle;
            }
        }
    }
}
