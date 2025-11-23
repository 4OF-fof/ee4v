using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public class CreateFolderDialog {
        public event Action<string> OnFolderCreated;

        public VisualElement CreateContent() {
            var content = new VisualElement();

            var title = new Label("Create New Folder") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label("Folder name:") {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var textField = new TextField { value = "", style = { marginBottom = 10 } };
            content.Add(textField);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var cancelBtn = new Button {
                text = "Cancel",
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var createBtn = new Button {
                text = "Create"
            };
            buttonRow.Add(createBtn);

            content.Add(buttonRow);

            cancelBtn.clicked += () => CloseDialog(content);
            createBtn.clicked += () =>
            {
                var folderName = textField.value;
                OnFolderCreated?.Invoke(folderName);
                CloseDialog(content);
            };

            content.schedule.Execute(() => textField.Focus());

            return content;
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }
    }
}