using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class EnumFormInput : FormInput
    {
        public EnumFormInput(string label, Enum value, Func<Enum, string> formatter = null,
            params string[] classNames) : base(label, CreateField(value, formatter))
        {
            AddToClassList("ee4v-ui-enum-form-input");
            foreach (var className in classNames) { AddToClassList(className); }
        }

        public Enum value => ((BaseField<Enum>)Input).value;

        public void SetValueWithoutNotify(Enum value)
        {
            ((BaseField<Enum>)Input).SetValueWithoutNotify(value);
        }

        private static BaseField<Enum> CreateField(
            Enum value,
            Func<Enum, string> formatter)
        {
            if (formatter == null)
            {
                return UiTextFactory.CreateEnumField(string.Empty, value,
                    "ee4v-ui-enum-form-input__input");
            }

            var values = Enum.GetValues(value.GetType());
            var choices = new List<Enum>(values.Length);
            var selectedIndex = 0;
            for (var index = 0; index < values.Length; index++)
            {
                var choice = (Enum)values.GetValue(index);
                choices.Add(choice);
                if (choice.Equals(value)) { selectedIndex = index; }
            }
            return UiTextFactory.CreatePopupField(string.Empty, choices, selectedIndex,
                formatter, formatter, "ee4v-ui-enum-form-input__input");
        }
    }
}
