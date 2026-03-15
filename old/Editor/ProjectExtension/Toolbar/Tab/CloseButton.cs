using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Tab {
    public class CloseButton : Button {
        public CloseButton() {
            name = "ee4v-project-toolbar-tabContainer-tab-close";
            style.width = 16;
            style.height = 16;
            style.backgroundImage =
                new StyleBackground(EditorGUIUtility.IconContent("winbtn_win_close").image as Texture2D);
            style.backgroundColor = new StyleColor(StyleKeyword.None);
            style.marginRight = 4;
            style.marginLeft = 2;
            style.paddingRight = 0;
            style.paddingLeft = 0;
            style.paddingTop = 0;
            style.paddingBottom = 0;
            style.alignSelf = Align.Center;
            style.borderTopWidth = 0;
            style.borderBottomWidth = 0;
            style.borderLeftWidth = 0;
            style.borderRightWidth = 0;
            style.opacity = 0.7f;

            RegisterCallback<MouseEnterEvent>(_ =>
                style.backgroundColor = ColorPreset.CloseIcon);

            RegisterCallback<MouseLeaveEvent>(_ =>
                style.backgroundColor = new StyleColor(StyleKeyword.None));
        }
    }
}