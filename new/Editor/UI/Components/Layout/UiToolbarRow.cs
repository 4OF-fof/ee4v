using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiToolbarRowState
    {
        public UiToolbarRowState(bool quiet = false, bool visible = true)
        {
            Quiet = quiet;
            Visible = visible;
        }

        public bool Quiet { get; }

        public bool Visible { get; }
    }

    internal sealed class UiToolbarRow : VisualElement
    {
        public UiToolbarRow(UiToolbarRowState state = null)
        {
            AddToClassList(UiClassNames.Toolbar);

            LeftSlot = new VisualElement();
            LeftSlot.AddToClassList(UiClassNames.ToolbarLeft);

            RightSlot = new VisualElement();
            RightSlot.AddToClassList(UiClassNames.ToolbarRight);

            Add(LeftSlot);
            Add(RightSlot);

            SetState(state ?? new UiToolbarRowState());
        }

        public VisualElement LeftSlot { get; }

        public VisualElement RightSlot { get; }

        public void SetState(UiToolbarRowState state)
        {
            state = state ?? new UiToolbarRowState();

            EnableInClassList(UiClassNames.ToolbarQuiet, state.Quiet);
            style.display = state.Visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
