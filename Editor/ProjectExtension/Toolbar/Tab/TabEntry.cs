using System.IO;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Tab {
    public class TabEntry : BaseTab {
        private readonly CloseButton _closeButton;

        public TabEntry(string path, string name = null, State state = State.Default) : base(path) {
            if (string.IsNullOrEmpty(name))
                name = Path.GetFileName(path);

            this.name = "ee4v-project-toolbar-tabContainer-tab";

            var tabLabel = new TabLabel(name);
            _closeButton = new CloseButton {
                style = { opacity = 0.7f }
            };

            Add(tabLabel);
            Add(_closeButton);

            _closeButton.clicked += () => { TabManager.Remove(this); };

            SetState(state);
        }

        public sealed override void SetState(State state) {
            base.SetState(state);
            _closeButton.style.opacity = state == State.Selected ? 1f : 0.7f;
        }

        protected override void OnMouseEnter(MouseEnterEvent evt) {
            base.OnMouseEnter(evt);
            _closeButton.style.opacity = 1f;
        }

        protected override void OnMouseLeave(MouseLeaveEvent evt) {
            base.OnMouseLeave(evt);
            if (GetState() != State.Selected)
                _closeButton.style.opacity = 0.7f;
        }
    }
}