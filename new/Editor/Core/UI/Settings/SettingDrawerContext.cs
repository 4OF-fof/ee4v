using System;

namespace Ee4v.Core.Settings
{
    public struct SettingDrawerContext<T>
    {
        private readonly Action<T> _onValueChanged;

        public SettingDrawerContext(
            string label,
            string tooltip,
            T value,
            string searchContext,
            Action<T> onValueChanged)
        {
            Label = label ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            Value = value;
            SearchContext = searchContext ?? string.Empty;
            _onValueChanged = onValueChanged;
        }

        public string Label { get; }

        public string Tooltip { get; }

        public T Value { get; }

        public string SearchContext { get; }

        public void NotifyValueChanged(T value)
        {
            _onValueChanged?.Invoke(value);
        }
    }
}
