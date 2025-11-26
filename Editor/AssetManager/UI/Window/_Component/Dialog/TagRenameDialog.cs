using System;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using _4OF.ee4v.Core.i18n;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public class TagRenameDialog {
        private Label _errorLabel;
        public event Action<string, string> OnTagRenamed;

        public VisualElement CreateContent(string oldTag) {
            var content = new VisualElement();

            var title = new Label(I18N.Get("UI.AssetManager.Dialog.TagRename.Title")) {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label(I18N.Get("UI.AssetManager.Dialog.TagRename.NewTagLabel")) {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var textField = new TextField { value = oldTag, style = { marginBottom = 10 } };
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
                        Commit();
                        evt.StopPropagation();
                        break;
                    case KeyCode.Escape:
                        CloseDialog();
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

            var okBtn = new Button {
                text = I18N.Get("UI.AssetManager.Dialog.Button.OK")
            };
            buttonRow.Add(okBtn);

            content.Add(buttonRow);

            cancelBtn.clicked += CloseDialog;
            okBtn.clicked += Commit;

            content.schedule.Execute(() =>
            {
                textField.Focus();
                textField.SelectAll();
            });

            return content;

            void CloseDialog() {
                var dialog = content.parent;
                var container = dialog?.parent;
                container?.RemoveFromHierarchy();
            }

            void Commit() {
                var newTag = textField.value;
                if (string.IsNullOrWhiteSpace(newTag)) {
                    ShowError(I18N.Get("UI.AssetManager.Dialog.TagRename.Error.EmptyName"));
                    return;
                }

                if (newTag != oldTag) OnTagRenamed?.Invoke(oldTag, newTag);
                CloseDialog();
            }
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
    }
}