using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class StepperSlider : VisualElement
    {
        private readonly SliderInt _slider;

        public StepperSlider(
            int value,
            int minimum,
            int maximum,
            params string[] classNames)
        {
            var safeMinimum = Math.Min(minimum, maximum);
            var safeMaximum = Math.Max(minimum, maximum);
            AddToClassList("ee4v-ui-stepper-slider");
            foreach (var className in classNames) { AddToClassList(className); }

            hierarchy.Add(CreateEndpointButton(
                UiLocalization.Get("ui.decrease"),
                "subtract.png",
                -1));
            _slider = UiTextFactory.CreateSliderInt(lowValue: safeMinimum, highValue: safeMaximum);
            _slider.showInputField = false;
            _slider.AddToClassList(
                "ee4v-ui-stepper-slider__slider");
            _slider.SetValueWithoutNotify(Mathf.Clamp(
                value,
                safeMinimum,
                safeMaximum));
            _slider.RegisterValueChangedCallback(evt =>
                ValueChanged?.Invoke(evt.newValue));
            hierarchy.Add(_slider);
            hierarchy.Add(CreateEndpointButton(
                UiLocalization.Get("ui.increase"),
                "add.png",
                1));
        }

        public event Action<int> ValueChanged;
        public int Value => _slider.value;
        public int Minimum => _slider.lowValue;
        public int Maximum => _slider.highValue;

        public void SetRangeWithoutNotify(int minimum, int maximum)
        {
            var safeMinimum = Math.Min(minimum, maximum);
            var safeMaximum = Math.Max(minimum, maximum);
            _slider.lowValue = safeMinimum;
            _slider.highValue = safeMaximum;
            SetValueWithoutNotify(_slider.value);
        }

        public void SetValueWithoutNotify(int value)
        {
            _slider.SetValueWithoutNotify(Mathf.Clamp(
                value,
                _slider.lowValue,
                _slider.highValue));
        }

        private void SetValue(int value)
        {
            _slider.value = Mathf.Clamp(
                value,
                _slider.lowValue,
                _slider.highValue);
        }

        private UiButton CreateEndpointButton(
            string tooltip,
            string iconFileName,
            int delta)
        {
            var button = new UiButton(string.Empty, () => SetValue(_slider.value + delta), tooltip,
                FluentUiIcons.CreateState(iconFileName, UiSizeTokens.Size12), UiButtonVariant.Ghost);
            button.AddToClassList("ee4v-ui-stepper-slider__endpoint");
            return button;
        }
    }

}
