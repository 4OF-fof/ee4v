using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class NavigationItemState
    {
        public NavigationItemState(
            string title,
            string description = null,
            IconState icon = null,
            bool selected = false)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Icon = icon;
            Selected = selected;
        }

        public string Title { get; }
        public string Description { get; }
        public IconState Icon { get; }
        public bool Selected { get; }
    }

    public class NavigationItem : Button
    {
        public NavigationItem(
            NavigationItemState state = null,
            Action onClick = null)
            : base(onClick)
        {
            AddToClassList("ee4v-ui-navigation-item");
            Row = new ItemRow(
                titleTypographyClassName:
                    UiClassNames.NavigationItemLabel);
            Row.AddToClassList("ee4v-ui-navigation-item__row");
            Row.pickingMode = PickingMode.Ignore;
            hierarchy.Add(Row);
            SetState(state ?? new NavigationItemState(string.Empty));
        }

        public ItemRow Row { get; }
        public VisualElement Leading => Row.Leading;
        public VisualElement Trailing => Row.Trailing;
        public bool Selected { get; private set; }

        public void SetState(NavigationItemState state)
        {
            state = state ?? new NavigationItemState(string.Empty);
            Row.SetState(new ItemRowState(
                state.Title,
                state.Description,
                state.Icon,
                ItemRowLayout.Stacked));
            SetSelected(state.Selected);
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            EnableInClassList(
                "ee4v-ui-navigation-item--selected",
                selected);
        }
    }
}
