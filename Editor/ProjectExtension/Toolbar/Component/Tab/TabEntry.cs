using System.IO;
using _4OF.ee4v.Core.UI;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Component.Tab {
    public class TabEntry : VisualElement {
        public enum State {
            Default,
            Selected
        }

        private readonly CloseButton _closeButton;

        public TabEntry(string path, string name = null, State state = State.Default) {
            if (string.IsNullOrEmpty(name))
                name = Path.GetFileName(path);

            this.name = "ee4v-project-toolbar-tabContainer-tab";
            tooltip = path;
            userData = state;

            style.alignItems = Align.Center;
            style.flexDirection = FlexDirection.Row;
            style.height = Length.Percent(95);
            style.marginTop = 1;
            style.paddingLeft = 4;
            style.backgroundColor = ColorPreset.TabBackground;
            style.borderRightWidth = 1;
            style.borderTopRightRadius = 4;
            style.borderTopLeftRadius = 4;
            style.borderRightColor = ColorPreset.TabBorder;

            var tabLabel = new TabLabel(name);
            _closeButton = new CloseButton();

            SetState(state);

            RegisterCallback<MouseEnterEvent>(_ =>
            {
                var current = GetState();
                style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabHoveredBackground;

                _closeButton.style.opacity = 1f;
            });

            RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var current = GetState();
                style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabBackground;

                _closeButton.style.opacity = 0.7f;
            });

            Add(tabLabel);
            Add(_closeButton);

            _closeButton.clicked += () => { TabManager.Remove(this); };
        }

        public State GetState() {
            return userData is State s ? s : State.Default;
        }

        public void SetState(State state) {
            userData = state;

            switch (state) {
                case State.Selected:
                    style.backgroundColor = ColorPreset.TabSelectedBackground;
                    _closeButton.style.opacity = 1f;
                    break;

                case State.Default:
                default:
                    style.backgroundColor = ColorPreset.TabBackground;
                    _closeButton.style.opacity = 0.7f;
                    break;
            }
        }

        public static State GetState(VisualElement tabElement) {
            return tabElement is TabEntry tab ? tab.GetState() : State.Default;
        }

        public static void SetState(VisualElement tabElement, State state) {
            if (tabElement is TabEntry tab) tab.SetState(state);
        }
    }
}