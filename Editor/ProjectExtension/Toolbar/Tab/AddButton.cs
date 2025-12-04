using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Tab {
    public class AddButton : Button {
        public AddButton() {
            name = "ee4v-project-toolbar-tabContainer-addButton";
            tooltip = I18N.Get("UI.ProjectExtension.AddNewTab");
            style.width = 20;
            style.height = 20;
            style.backgroundColor = new StyleColor(StyleKeyword.None);
            style.marginRight = 4;
            style.marginLeft = 2;
            style.marginTop = 2;
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
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;

            var icon = new Image {
                image = EditorGUIUtility.IconContent("CreateAddNew").image as Texture2D,
                style = { width = 14, height = 14 }
            };
            Add(icon);

            RegisterCallback<MouseEnterEvent>(_ =>
                style.backgroundColor = ColorPreset.SMouseOverBackground);
            RegisterCallback<MouseLeaveEvent>(_ =>
                style.backgroundColor = new StyleColor(StyleKeyword.None));

            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                evt.StopPropagation();
                var screenPosition = GUIUtility.GUIToScreenPoint(evt.position);
                CreateWorkspaceWindow.Show(screenPosition);
            });
        }
    }
}