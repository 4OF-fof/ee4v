using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class NumericSliderCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Inputs/NumericSlider/numeric-slider.uss");
                registry.RegisterStory(new StoryRegistration(
                    "numeric-slider",
                    "Inputs",
                    "NumericSlider",
                    "バーをドラッグして範囲内の数値を選択する小型スライダーコンポーネントです。",
                    "min/max による値の clip と任意の step 丸めを持ち、横幅の狭い toolbar や inspector の数値調整に使う想定です。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildNumericSliderStory(parent)));
            }
        }

        private void BuildNumericSliderStory(VisualElement parent)
        {
            var value = 15f;
            var minValue = 0f;
            var maxValue = 100f;
            var step = 10f;
            var enabled = true;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "ドラッグ、clip、step 丸めの挙動を確認します。");
            var valueField = AddFloatField(controls.Content, "値", value, nextValue =>
            {
                value = nextValue;
                refresh();
            });
            var minField = AddFloatField(controls.Content, "Min", minValue, nextValue =>
            {
                minValue = nextValue;
                refresh();
            });
            var maxField = AddFloatField(controls.Content, "Max", maxValue, nextValue =>
            {
                maxValue = nextValue;
                refresh();
            });
            var stepField = AddFloatField(controls.Content, "Step", step, nextValue =>
            {
                step = Mathf.Max(0f, nextValue);
                refresh();
            });
            var enabledToggle = new Toggle("Enabled")
            {
                value = enabled
            };
            enabledToggle.RegisterValueChangedCallback(evt =>
            {
                enabled = evt.newValue;
                refresh();
            });
            controls.Content.Add(enabledToggle);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            surface.style.width = 220f;
            var slider = new NumericSlider();
            surface.Add(slider);
            preview.Body.Add(surface);

            var valueCard = new InfoCard();
            preview.Body.Add(valueCard);

            slider.ValueChanged += nextValue =>
            {
                value = nextValue;
                refresh();
            };

            refresh = () =>
            {
                value = NumericSliderState.ClampValue(value, Mathf.Min(minValue, maxValue), Mathf.Max(minValue, maxValue));
                valueField.SetValueWithoutNotify(value);
                minField.SetValueWithoutNotify(minValue);
                maxField.SetValueWithoutNotify(maxValue);
                stepField.SetValueWithoutNotify(step);
                enabledToggle.SetValueWithoutNotify(enabled);
                slider.SetState(new NumericSliderState(value, minValue, maxValue, step, enabled));
                valueCard.SetState(new InfoCardState("Current Value", value.ToString("0.###"), "State"));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private static FloatField AddFloatField(VisualElement parent, string label, float value, Action<float> onChanged)
        {
            var field = new FloatField(label)
            {
                value = value
            };
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }
    }
}
