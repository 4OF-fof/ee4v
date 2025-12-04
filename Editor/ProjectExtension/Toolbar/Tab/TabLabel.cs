using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Tab {
    public sealed class TabLabel : Label {
        public TabLabel(string name) {
            this.name = "ee4v-project-toolbar-tabContainer-tab-label";
            text = name;

            style.backgroundColor = new StyleColor(StyleKeyword.None);
            style.borderTopWidth = 0;
            style.borderBottomWidth = 0;
            style.color = ColorPreset.STextColor;
            style.unityBackgroundImageTintColor = new StyleColor(Color.clear);
            style.borderRightWidth = 4;
            style.borderLeftWidth = 4;
            style.borderLeftColor = new StyleColor(Color.clear);
            style.borderRightColor = new StyleColor(Color.clear);
        }
    }
}