using System;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Dialog {
    public class CreateFolderDialog {
        private Label _errorLabel;
        public event Action<string> OnFolderCreated;

        public VisualElement CreateContent() {
            var content = new VisualElement();

            var title = new Label(I18N.Get("UI.AssetManager.Dialog.CreateFolder.Title")) {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label(I18N.Get("UI.AssetManager.Dialog.CreateFolder.Label")) {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var textField = new TextField { value = "", style = { marginBottom = 10 } };
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
                        AttemptCreate(textField.value, content);
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
                text = I18N.Get("UI.AssetManager.Dialog.Button.Cancel"),
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var createBtn = new Button {
                text = I18N.Get("UI.AssetManager.Dialog.Button.Create")
            };
            buttonRow.Add(createBtn);

            content.Add(buttonRow);

            cancelBtn.clicked += () => CloseDialog(content);
            createBtn.clicked += () => AttemptCreate(textField.value, content);

            content.schedule.Execute(() => textField.Focus());

            return content;
        }

        private void AttemptCreate(string folderName, VisualElement content) {
            if (!AssetValidationService.IsValidAssetName(folderName)) {
                ShowError(I18N.Get("UI.AssetManager.Dialog.RenameFolder.Error.InvalidName"));
                return;
            }

            OnFolderCreated?.Invoke(folderName);
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