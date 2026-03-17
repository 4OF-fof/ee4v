using UnityEngine;

namespace Ee4v.Core.Settings
{
    public struct SettingDrawerContext<T>
    {
        public SettingDrawerContext(GUIContent label, T value, string searchContext)
        {
            Label = label;
            Value = value;
            SearchContext = searchContext;
        }

        public GUIContent Label { get; }

        public T Value { get; }

        public string SearchContext { get; }
    }
}
