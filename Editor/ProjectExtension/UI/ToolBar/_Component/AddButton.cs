using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.ProjectExtension.UI.Window;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.UI.ToolBar._Component {
    public static class AddButton {
        public static Button Element() {
            var addButton = new Button {
                name = "ee4v-project-toolbar-tabContainer-addButton",
                tooltip = I18N.Get("UI.ProjectExtension.AddNewTab"),
                style = {
                    width = 20, height = 20,
                    backgroundColor = new StyleColor(StyleKeyword.None),
                    marginRight = 4, marginLeft = 2, marginTop = 2,
                    paddingRight = 0, paddingLeft = 0, paddingTop = 0, paddingBottom = 0,
                    alignSelf = Align.Center,
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                    opacity = 0.7f,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                }
            };

            var icon = new Image {
                image = EditorGUIUtility.IconContent("CreateAddNew").image as Texture2D,
                style = { width = 14, height = 14 }
            };
            addButton.Add(icon);

            addButton.RegisterCallback<MouseEnterEvent>(_ =>
                addButton.style.backgroundColor = ColorPreset.AddButtonHover);
            addButton.RegisterCallback<MouseLeaveEvent>(_ =>
                addButton.style.backgroundColor = new StyleColor(StyleKeyword.None));

            addButton.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                evt.StopPropagation();
                var screenPosition = GUIUtility.GUIToScreenPoint(evt.position);
                CreateWorkspaceWindow.Show(screenPosition);
            });

            return addButton;
        }
    }
}