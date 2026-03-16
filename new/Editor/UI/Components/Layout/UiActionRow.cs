using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiActionRowState
    {
        public UiActionRowState(bool compact = false)
        {
            Compact = compact;
        }

        public bool Compact { get; }
    }

    internal sealed class UiActionRow : VisualElement
    {
        public UiActionRow(UiActionRowState state = null)
        {
            AddToClassList(UiClassNames.ActionRow);

            LeftSlot = new VisualElement();
            LeftSlot.AddToClassList(UiClassNames.ActionRowLeft);

            RightSlot = new VisualElement();
            RightSlot.AddToClassList(UiClassNames.ActionRowRight);

            Add(LeftSlot);
            Add(RightSlot);

            SetState(state ?? new UiActionRowState());
        }

        public VisualElement LeftSlot { get; }

        public VisualElement RightSlot { get; }

        public void SetState(UiActionRowState state)
        {
            state = state ?? new UiActionRowState();
            EnableInClassList(UiClassNames.ActionRowCompact, state.Compact);
        }
    }
}
