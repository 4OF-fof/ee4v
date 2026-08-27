using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public class FormField : VisualElement
    {
        private const string FocusedClassName =
            "ee4v-ui-form-field--focused";
        private const string HasLabelClassName =
            "ee4v-ui-form-field--has-label";
        private const string ToggleClassName =
            "ee4v-ui-form-field--toggle";

        public FormField(
            string label,
            VisualElement input,
            Button button = null)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            AddToClassList("ee4v-ui-form-field");
            LabelText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormLabel,
                "ee4v-ui-form-field__label");
            Input = input;
            Input.AddToClassList("ee4v-ui-form-field__input");
            EnableInClassList(ToggleClassName, Input is Toggle);
            Button = button;
            Add(LabelText);
            Add(Input);
            if (Button != null)
            {
                Button.AddToClassList("ee4v-ui-form-field__button");
                Add(Button);
            }
            RegisterCallback<FocusInEvent>(_ =>
                AddToClassList(FocusedClassName));
            RegisterCallback<FocusOutEvent>(_ =>
                RemoveFromClassList(FocusedClassName));
            SetLabel(label);
        }

        public UiTextElement LabelText { get; }
        public VisualElement Input { get; }
        public Button Button { get; }

        public void SetLabel(string label)
        {
            label = label ?? string.Empty;
            var hasLabel = !string.IsNullOrWhiteSpace(label);
            LabelText.SetText(label);
            LabelText.style.display = hasLabel
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            EnableInClassList(HasLabelClassName, hasLabel);
        }
    }
}
