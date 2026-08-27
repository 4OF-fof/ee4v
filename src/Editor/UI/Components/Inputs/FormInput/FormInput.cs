using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public class FormInput : VisualElement
    {
        public FormInput(
            VisualElement input,
            Button button = null)
            : this(string.Empty, input, button)
        {
        }

        public FormInput(
            string label,
            VisualElement input,
            Button button = null)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            AddToClassList("ee4v-ui-form-input");
            LabelText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormLabel,
                "ee4v-ui-form-input__label");
            Input = input;
            Input.AddToClassList("ee4v-ui-form-input__input");
            Button = button;
            Add(LabelText);
            Add(Input);
            if (Button != null)
            {
                Button.AddToClassList("ee4v-ui-form-input__button");
                Add(Button);
            }
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
        }
    }
}
