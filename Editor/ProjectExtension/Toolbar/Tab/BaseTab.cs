using _4OF.ee4v.Core.UI;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Tab {
    public abstract class BaseTab : VisualElement {
        public enum State {
            Default,
            Selected
        }

        protected BaseTab(string tooltipPath) {
            tooltip = tooltipPath;
            userData = State.Default;
            style.alignItems = Align.Center;
            style.flexDirection = FlexDirection.Row;
            style.height = Length.Percent(95);
            style.marginTop = 1;
            style.paddingLeft = 4;
            style.backgroundColor = ColorPreset.SDefaultBackground;
            style.borderRightWidth = 1;
            style.borderTopRightRadius = 4;
            style.borderTopLeftRadius = 4;
            style.borderRightColor = ColorPreset.SWindowBorder;
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        public State GetState() {
            return userData is State s ? s : State.Default;
        }

        public virtual void SetState(State state) {
            userData = state;
            UpdateAppearance();
        }

        protected virtual void UpdateAppearance() {
            var state = GetState();
            style.backgroundColor = state == State.Selected
                ? ColorPreset.TabSelectedBackground
                : ColorPreset.SDefaultBackground;
        }

        protected virtual void OnMouseEnter(MouseEnterEvent evt) {
            if (GetState() != State.Selected) style.backgroundColor = ColorPreset.SMouseOverBackground;
        }

        protected virtual void OnMouseLeave(MouseLeaveEvent evt) {
            UpdateAppearance();
        }

        public static void SetState(VisualElement tabElement, State state) {
            if (tabElement is BaseTab tab) tab.SetState(state);
        }
    }
}