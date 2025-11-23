using System;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public class RenameFolderDialog {
        public event Action<Ulid, string> OnFolderRenamed;

        public VisualElement CreateContent(Ulid folderId, string oldName) {
            var content = new VisualElement();

            var title = new Label("Rename Folder") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label("New folder name:") {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var textField = new TextField { value = oldName, style = { marginBottom = 10 } };
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

            var okBtn = new Button {
                text = "OK"
            };
            buttonRow.Add(okBtn);

            content.Add(buttonRow);

            cancelBtn.clicked += () => CloseDialog(content);
            okBtn.clicked += () =>
            {
                var newName = textField.value;
                if (newName != oldName || string.IsNullOrWhiteSpace(newName))
                    OnFolderRenamed?.Invoke(folderId, newName);
                CloseDialog(content);
            };

            content.schedule.Execute(() =>
            {
                textField.Focus();
                textField.SelectAll();
            });

            return content;
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }
    }
}