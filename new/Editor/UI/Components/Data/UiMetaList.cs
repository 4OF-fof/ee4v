using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiMetaListItem
    {
        public UiMetaListItem(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }
    }

    internal sealed class UiMetaListState
    {
        public UiMetaListState(IReadOnlyList<UiMetaListItem> items)
        {
            Items = items ?? new UiMetaListItem[0];
        }

        public IReadOnlyList<UiMetaListItem> Items { get; }
    }

    internal sealed class UiMetaList : VisualElement
    {
        public UiMetaList(UiMetaListState state = null)
        {
            AddToClassList(UiClassNames.MetaList);
            SetState(state ?? new UiMetaListState(new UiMetaListItem[0]));
        }

        public void SetState(UiMetaListState state)
        {
            state = state ?? new UiMetaListState(new UiMetaListItem[0]);
            Clear();

            for (var i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                var row = new VisualElement();
                row.AddToClassList(UiClassNames.MetaRow);

                var label = UiTextFactory.Create(item.Label, UiClassNames.MetaLabel);

                var value = UiTextFactory.Create(item.Value, UiClassNames.MetaValue);

                row.Add(label);
                row.Add(value);
                Add(row);
            }
        }
    }
}
