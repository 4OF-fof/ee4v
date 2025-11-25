using System;
using System.Collections.Generic;

namespace _4OF.ee4v.Core.Utility {
    public class BindableProperty<T> {
        private readonly EqualityComparer<T> _comparer;
        private T _value;

        public BindableProperty(T initialValue = default, EqualityComparer<T> comparer = null) {
            _value = initialValue;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Value {
            get => _value;
            set {
                if (_comparer.Equals(_value, value)) return;
                _value = value;
                OnValueChanged?.Invoke(_value);
            }
        }

        public event Action<T> OnValueChanged;

        public void SetWithoutNotify(T value) {
            _value = value;
        }

        public void ForceNotify() {
            OnValueChanged?.Invoke(_value);
        }

        public static implicit operator T(BindableProperty<T> property) {
            return property.Value;
        }
    }
}