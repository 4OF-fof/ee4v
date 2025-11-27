using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Component {
    public static class CloseButton {
        public static Button Element() {
            var closeButton = new Button {
                name = "ee4v-project-toolbar-tabContainer-tab-close",
                style = {
                    width = 16, height = 16,
                    backgroundImage =
                        new StyleBackground(EditorGUIUtility.IconContent("winbtn_win_close").image as Texture2D),
                    backgroundColor = new StyleColor(StyleKeyword.None),
                    marginRight = 4, marginLeft = 2,
                    paddingRight = 0, paddingLeft = 0, paddingTop = 0, paddingBottom = 0,
                    alignSelf = Align.Center,
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                    opacity = 0.7f
                }
            };

            closeButton.RegisterCallback<MouseEnterEvent>(_ =>
                closeButton.style.backgroundColor = ColorPreset.TabCloseButtonHover);
            closeButton.RegisterCallback<MouseLeaveEvent>(_ =>
                closeButton.style.backgroundColor = new StyleColor(StyleKeyword.None));
            return closeButton;
        }
    }
}