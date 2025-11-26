using System;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public class RenameFolderDialog {
        private Label _errorLabel;
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

            _errorLabel = new Label {
                style = {
                    color = ColorPreset.WarningText,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 5,
                    display = DisplayStyle.None
                }
            };
            content.Add(_errorLabel);

            textField.RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode) {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        AttemptRename(textField.value, oldName, folderId, content);
                        evt.StopPropagation();
                        break;
                    case KeyCode.Escape:
                        CloseDialog(content);
                        evt.StopPropagation();
                        break;
                }
            });
            textField.RegisterCallback<InputEvent>(_ => HideError());

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
            okBtn.clicked += () => AttemptRename(textField.value, oldName, folderId, content);

            content.schedule.Execute(() =>
            {
                textField.Focus();
                textField.SelectAll();
            });

            return content;
        }

        private void AttemptRename(string newName, string oldName, Ulid folderId, VisualElement content) {
            if (!AssetValidationService.IsValidAssetName(newName)) {
                ShowError("無効なフォルダ名です。");
                return;
            }

            if (newName != oldName)
                OnFolderRenamed?.Invoke(folderId, newName);
            CloseDialog(content);
        }

        private void ShowError(string message) {
            if (_errorLabel == null) return;
            _errorLabel.text = message;
            _errorLabel.style.display = DisplayStyle.Flex;
        }

        private void HideError() {
            if (_errorLabel == null) return;
            _errorLabel.text = "";
            _errorLabel.style.display = DisplayStyle.None;
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }
    }
}