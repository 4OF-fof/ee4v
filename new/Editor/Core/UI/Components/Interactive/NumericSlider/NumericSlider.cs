using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class NumericSliderState
    {
        public NumericSliderState(float value = 0f, float minValue = 0f, float maxValue = 1f, float step = 0f, bool enabled = true)
        {
            MinValue = Mathf.Min(minValue, maxValue);
            MaxValue = Mathf.Max(minValue, maxValue);
            Step = Mathf.Max(0f, step);
            Value = ClampValue(value, MinValue, MaxValue);
            Enabled = enabled;
        }

        public float Value { get; }

        public float MinValue { get; }

        public float MaxValue { get; }

        public float Step { get; }

        public bool Enabled { get; }

        internal static float ClampValue(float value, float minValue, float maxValue)
        {
            return Mathf.Clamp(value, minValue, maxValue);
        }

        internal static float ClampSteppedValue(float value, float minValue, float maxValue, float step)
        {
            var nextValue = ClampValue(value, minValue, maxValue);
            if (step > 0f)
            {
                nextValue = minValue + (Mathf.Round((nextValue - minValue) / step) * step);
                nextValue = ClampValue(nextValue, minValue, maxValue);
            }

            return nextValue;
        }
    }

    internal sealed class NumericSlider : VisualElement
    {
        private const string RootClassName = "ee4v-ui-numeric-slider";
        private const string DisabledClassName = "ee4v-ui-numeric-slider--disabled";
        private const string DraggingClassName = "ee4v-ui-numeric-slider--dragging";
        private const string EndpointClassName = "ee4v-ui-numeric-slider__endpoint";
        private const string TrackHostClassName = "ee4v-ui-numeric-slider__track-host";
        private const string TrackClassName = "ee4v-ui-numeric-slider__track";
        private const string FillClassName = "ee4v-ui-numeric-slider__fill";
        private const string KnobClassName = "ee4v-ui-numeric-slider__knob";
        private readonly Label _minusButton;
        private readonly VisualElement _trackHost;
        private readonly VisualElement _track;
        private readonly VisualElement _fill;
        private readonly VisualElement _knob;
        private readonly Label _plusButton;
        private float _value;
        private float _minValue;
        private float _maxValue = 1f;
        private float _step;

        public NumericSlider(NumericSliderState state = null)
        {
            AddToClassList(RootClassName);
            focusable = true;

            _minusButton = CreateEndpointButton("-", () => AddValue(-1f));
            _plusButton = CreateEndpointButton("+", () => AddValue(1f));

            _trackHost = new VisualElement();
            _trackHost.AddToClassList(TrackHostClassName);
            _trackHost.AddManipulator(new SliderDragManipulator(this));

            _track = new VisualElement();
            _track.AddToClassList(TrackClassName);
            _fill = new VisualElement();
            _fill.AddToClassList(FillClassName);
            _knob = new VisualElement();
            _knob.AddToClassList(KnobClassName);

            _track.Add(_fill);
            _track.Add(_knob);
            _trackHost.Add(_track);

            Add(_minusButton);
            Add(_trackHost);
            Add(_plusButton);

            RegisterCallback<GeometryChangedEvent>(_ => RefreshVisuals());
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            SetState(state ?? new NumericSliderState());
        }

        public event Action<float> ValueChanged;

        public float Value
        {
            get { return _value; }
            set { SetValue(value); }
        }

        public float MinValue
        {
            get { return _minValue; }
        }

        public float MaxValue
        {
            get { return _maxValue; }
        }

        public float Step
        {
            get { return _step; }
        }

        public void SetState(NumericSliderState state)
        {
            state = state ?? new NumericSliderState();
            _minValue = state.MinValue;
            _maxValue = state.MaxValue;
            _step = state.Step;
            SetEnabled(state.Enabled);
            SetValueWithoutNotify(state.Value);
        }

        public void SetValueWithoutNotify(float value)
        {
            _value = ClipValue(value);
            RefreshVisuals();
        }

        public void SetValue(float value)
        {
            var nextValue = ClipValue(value);
            if (Mathf.Approximately(nextValue, _value))
            {
                return;
            }

            _value = nextValue;
            RefreshVisuals();
            ValueChanged?.Invoke(_value);
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            EnableInClassList(DisabledClassName, !value);
        }

        private static Label CreateEndpointButton(string text, Action onClick)
        {
            var button = new Label(text);
            button.AddToClassList(EndpointClassName);
            button.AddManipulator(new Clickable(onClick));
            return button;
        }

        private void AddValue(float delta)
        {
            if (!enabledInHierarchy)
            {
                return;
            }

            Focus();
            SetRawValue(_value + delta);
        }

        private void SetRawValue(float value)
        {
            var nextValue = Mathf.Clamp(value, _minValue, _maxValue);
            if (Mathf.Approximately(nextValue, _value))
            {
                return;
            }

            _value = nextValue;
            RefreshVisuals();
            ValueChanged?.Invoke(_value);
        }

        private float ClipValue(float value)
        {
            return NumericSliderState.ClampValue(value, _minValue, _maxValue);
        }

        private float ClipSteppedValue(float value)
        {
            return NumericSliderState.ClampSteppedValue(value, _minValue, _maxValue, _step);
        }

        private void SetSteppedValue(float value)
        {
            var nextValue = ClipSteppedValue(value);
            if (Mathf.Approximately(nextValue, _value))
            {
                return;
            }

            _value = nextValue;
            RefreshVisuals();
            ValueChanged?.Invoke(_value);
        }

        private void SetNormalizedValue(float normalizedValue)
        {
            SetSteppedValue(Mathf.Lerp(_minValue, _maxValue, Mathf.Clamp01(normalizedValue)));
        }

        private float GetNormalizedValue()
        {
            var range = _maxValue - _minValue;
            if (Mathf.Approximately(range, 0f))
            {
                return 0f;
            }

            return Mathf.Clamp01((_value - _minValue) / range);
        }

        private void RefreshVisuals()
        {
            var normalizedValue = GetNormalizedValue();
            var trackWidth = Mathf.Max(0f, _track.resolvedStyle.width);
            var knobWidth = Mathf.Max(0f, _knob.resolvedStyle.width);
            var knobOffset = Mathf.Max(0f, trackWidth - knobWidth) * normalizedValue;
            var fillWidth = knobOffset + (knobWidth * 0.5f);

            _fill.style.width = Mathf.Clamp(fillWidth, 0f, trackWidth);
            _knob.style.left = knobOffset;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!enabledInHierarchy)
            {
                return;
            }

            var increment = _step > 0f ? _step : (_maxValue - _minValue) / 100f;
            if (Mathf.Approximately(increment, 0f))
            {
                return;
            }

            if (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.DownArrow)
            {
                SetSteppedValue(_value - increment);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.RightArrow || evt.keyCode == KeyCode.UpArrow)
            {
                SetSteppedValue(_value + increment);
                evt.StopPropagation();
            }
        }

        private void BeginDrag()
        {
            EnableInClassList(DraggingClassName, true);
        }

        private void EndDrag()
        {
            EnableInClassList(DraggingClassName, false);
        }

        private void ApplyTrackPosition(float localX)
        {
            var trackWidth = Mathf.Max(1f, _trackHost.resolvedStyle.width);
            SetNormalizedValue(localX / trackWidth);
        }

        private sealed class SliderDragManipulator : PointerManipulator
        {
            private readonly NumericSlider _owner;
            private bool _active;
            private int _pointerId = -1;

            public SliderDragManipulator(NumericSlider owner)
            {
                _owner = owner;
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (_owner == null || !_owner.enabledInHierarchy || !CanStartManipulation(evt))
                {
                    return;
                }

                _active = true;
                _pointerId = evt.pointerId;
                target.CapturePointer(_pointerId);
                _owner.Focus();
                _owner.BeginDrag();
                _owner.ApplyTrackPosition(evt.localPosition.x);
                evt.StopPropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!_active || evt.pointerId != _pointerId || _owner == null)
                {
                    return;
                }

                _owner.ApplyTrackPosition(evt.localPosition.x);
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!_active || evt.pointerId != _pointerId || !CanStopManipulation(evt))
                {
                    return;
                }

                target.ReleasePointer(_pointerId);
                _active = false;
                _pointerId = -1;
                _owner.EndDrag();
                evt.StopPropagation();
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (!_active)
                {
                    return;
                }

                _active = false;
                _pointerId = -1;
                _owner.EndDrag();
            }
        }
    }
}
