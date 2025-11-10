using System.IO;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.ProjectExtension.Data;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab {
    public static class Tab {
        public enum State {
            Default,
            Selected
        }

        public static VisualElement Element(string path, string name = null, State state = State.Default) {
            if (string.IsNullOrEmpty(name)) name = Path.GetFileName(path);
            var tabLabel = TabLabel.Draw(name, path);
            var closeButton = CloseButton.Element();
            var tab = new VisualElement {
                name = "ee4v-project-toolbar-tabContainer-tab",
                tooltip = path,
                style = {
                    alignItems = Align.Center,
                    flexDirection = FlexDirection.Row,
                    height = Length.Percent(95),
                    marginTop = 1,
                    paddingLeft = 4,
                    backgroundColor = ColorPreset.TabBackground,
                    borderRightWidth = 1,
                    borderTopRightRadius = 4, borderTopLeftRadius = 4,
                    borderRightColor = ColorPreset.TabBorder
                },
                userData = state
            };
            SetState(tab, state);
            tab.RegisterCallback<MouseEnterEvent>(_ =>
            {
                var current = GetState(tab);
                tab.style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabHoveredBackground;
                closeButton.style.opacity = 1f;
            });
            tab.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var current = GetState(tab);
                tab.style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabBackground;
                closeButton.style.opacity = 0.7f;
            });

            tab.Add(tabLabel);
            tab.Add(closeButton);

            closeButton.clicked += () => { TabListController.Remove(tab); };

            return tab;
        }

        private static State GetState(VisualElement tab) {
            return tab.userData is State s ? s : State.Default;
        }

        public static void SetState(VisualElement tab, State state) {
            tab.userData = state;
            var closeButton = tab.Q<VisualElement>("ee4v-project-toolbar-tabContainer-tab-close");
            switch (state) {
                case State.Selected:
                    tab.style.backgroundColor = ColorPreset.TabSelectedBackground;
                    if (closeButton != null) closeButton.style.opacity = 1f;
                    break;
                case State.Default:
                default:
                    tab.style.backgroundColor = ColorPreset.TabBackground;
                    if (closeButton != null) closeButton.style.opacity = 0.7f;
                    break;
            }
        }
    }
}