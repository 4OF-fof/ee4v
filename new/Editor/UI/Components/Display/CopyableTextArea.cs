using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class CopyableTextAreaState
    {
        public CopyableTextAreaState(string text, string copyButtonText = null)
        {
            Text = text ?? string.Empty;
            CopyButtonText = copyButtonText ?? string.Empty;
        }

        public string Text { get; }

        public string CopyButtonText { get; }
    }

    internal sealed class CopyableTextArea : VisualElement
    {
        private const int CopyFeedbackDurationMs = 1200;
        private const string RootClassName = "ee4v-ui-copyable-text-area";
        private const string CopyButtonClassName = "ee4v-ui-copyable-text-area__copy-button";
        private const string CopyButtonCopiedClassName = "ee4v-ui-copyable-text-area__copy-button--copied";
        private const string FieldClassName = "ee4v-ui-copyable-text-area__field";
        private readonly Button _copyButton;
        private readonly TextField _textField;
        private IVisualElementScheduledItem _copyFeedbackResetItem;
        private CopyableTextAreaState _state;

        public CopyableTextArea(CopyableTextAreaState state = null)
        {
            AddToClassList(RootClassName);

            _copyButton = new Button(CopyToClipboard);
            _copyButton.AddToClassList(CopyButtonClassName);

            _textField = new TextField();
            _textField.multiline = true;
            _textField.isReadOnly = true;
            _textField.AddToClassList(FieldClassName);

            Add(_textField);
            Add(_copyButton);

            SetState(state ?? new CopyableTextAreaState(string.Empty));
        }

        public string Value
        {
            get { return _textField.value ?? string.Empty; }
        }

        public void SetState(CopyableTextAreaState state)
        {
            _state = state ?? new CopyableTextAreaState(string.Empty);
            ClearCopyFeedback();

            _copyButton.text = _state.CopyButtonText;
            _copyButton.style.display = string.IsNullOrWhiteSpace(_state.CopyButtonText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _copyButton.SetEnabled(!string.IsNullOrWhiteSpace(_state.Text));
            _textField.SetValueWithoutNotify(_state.Text);
        }

        public void CopyCurrentValueToClipboard()
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = Value;
            ShowCopyFeedback();
        }

        private void CopyToClipboard()
        {
            CopyCurrentValueToClipboard();
        }

        private void ShowCopyFeedback()
        {
            _copyFeedbackResetItem?.Pause();
            _copyButton.text = "Copied";
            _copyButton.EnableInClassList(CopyButtonCopiedClassName, true);
            _copyFeedbackResetItem = schedule.Execute(ClearCopyFeedback).StartingIn(CopyFeedbackDurationMs);
        }

        private void ClearCopyFeedback()
        {
            _copyFeedbackResetItem?.Pause();
            _copyFeedbackResetItem = null;
            _copyButton.EnableInClassList(CopyButtonCopiedClassName, false);
            _copyButton.text = _state != null ? _state.CopyButtonText : string.Empty;
        }
    }
}
