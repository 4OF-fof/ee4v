using System;
using _4OF.ee4v.AssetManager.Booth;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Component.Dialog {
    public class EditBoothInfoDialog {
        public event Action<string, string> OnBoothInfoUpdated;

        public VisualElement CreateContent(string currentUrl) {
            var content = new VisualElement();

            var title = new Label(I18N.Get("UI.AssetManager.Dialog.EditBoothInfo.Title")) {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label(I18N.Get("UI.AssetManager.Dialog.EditBoothInfo.BoothUrlLabel")) {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var hint = new Label(I18N.Get("UI.AssetManager.Dialog.EditBoothInfo.BoothURLHint")) {
                style = {
                    fontSize = 10,
                    color = ColorPreset.InActiveItem,
                    marginBottom = 5
                }
            };
            content.Add(hint);

            var textField = new TextField { value = currentUrl, style = { marginBottom = 10 } };
            content.Add(textField);

            textField.RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode) {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        Commit(content, textField.value);
                        evt.StopPropagation();
                        break;
                    case KeyCode.Escape:
                        CloseDialog(content);
                        evt.StopPropagation();
                        break;
                }
            });

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

            cancelBtn.clicked += () => CloseDialog(content);
            okBtn.clicked += () => Commit(content, textField.value);

            content.schedule.Execute(() =>
            {
                textField.Focus();
                textField.SelectAll();
            });

            return content;
        }

        private void Commit(VisualElement content, string url) {
            var shopDomain = "";
            var itemId = "";

            if (!string.IsNullOrWhiteSpace(url)) BoothUtility.TryParseShopItemUrl(url, out shopDomain, out itemId);

            OnBoothInfoUpdated?.Invoke(shopDomain, itemId);
            CloseDialog(content);
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }
    }
}