using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class InputGroup : VisualElement
    {
        private const string FocusedClassName =
            "ee4v-ui-input-group--focused";

        public InputGroup(
            string label,
            FormInput input,
            params FormInput[] additionalInputs)
        {
            AddToClassList("ee4v-ui-input-group");
            LabelText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormLabel,
                "ee4v-ui-input-group__label");
            Content = new VisualElement();
            Content.AddToClassList("ee4v-ui-input-group__content");
            hierarchy.Add(LabelText);
            hierarchy.Add(Content);
            RegisterCallback<FocusInEvent>(_ =>
                AddToClassList(FocusedClassName));
            RegisterCallback<FocusOutEvent>(_ =>
                RemoveFromClassList(FocusedClassName));
            SetLabel(label);
            AddInput(input);

            for (var index = 0;
                 index < (additionalInputs?.Length ?? 0);
                 index++)
            {
                AddInput(additionalInputs[index]);
            }
        }

        public UiTextElement LabelText { get; }
        public VisualElement Content { get; }

        public override VisualElement contentContainer => Content;

        public void AddInput(FormInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Add(input);
        }

        public void SetLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "InputGroup requires a label.",
                    nameof(label));
            }

            LabelText.SetText(label);
        }
    }
}
