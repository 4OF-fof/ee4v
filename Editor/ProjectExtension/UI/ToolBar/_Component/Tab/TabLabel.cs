using UnityEngine;
using UnityEngine.UIElements;

using _4OF.ee4v.Core.UI;

namespace _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab {
    public static class TabLabel {
        public static Label Draw(string name, string path) {
            var openButton = new Label {
                name = "ee4v-project-toolbar-tabContainer-tab-label",
                text = name,
                style = {
                    backgroundColor = new StyleColor(StyleKeyword.None),
                    borderTopWidth = 0, borderBottomWidth = 0,
                    paddingRight = 4, paddingLeft = 8,
                    color = ColorPreset.TabText,
                    unityBackgroundImageTintColor = new StyleColor(Color.clear),
                    borderRightWidth = 4, borderLeftWidth = 4,
                    borderLeftColor = new StyleColor(Color.clear),
                    borderRightColor = new StyleColor(Color.clear)
                }
            };
            
            return openButton;
        }
    }
}