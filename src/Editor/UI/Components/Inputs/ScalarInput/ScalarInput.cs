using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class ScalarInput : VisualElement
    {
        public ScalarInput(float minimum, float maximum, string resetLabel, Action reset, string unit = null)
        {
            AddToClassList("ee4v-ui-scalar-input");
            Slider = UiTextFactory.CreateSlider(lowValue: minimum, highValue: maximum);
            Slider.AddToClassList("ee4v-ui-scalar-input__slider");
            ValueField = UiTextFactory.CreateFloatField();
            ValueField.AddToClassList("ee4v-ui-scalar-input__value");
            ResetButton = new UiButton(resetLabel, reset, variant: UiButtonVariant.Ghost);
            ResetButton.AddToClassList("ee4v-ui-scalar-input__reset");
            Add(Slider);
            Add(ValueField);
            if (!string.IsNullOrEmpty(unit))
            {
                var text = UiTextFactory.Create(unit, UiClassNames.SecondaryText);
                text.AddToClassList("ee4v-ui-scalar-input__unit");
                Add(text);
            }
            Add(ResetButton);
        }

        public Slider Slider { get; }
        public FloatField ValueField { get; }
        public UiButton ResetButton { get; }
    }
}
