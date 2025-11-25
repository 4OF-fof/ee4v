using System;
using _4OF.ee4v.AssetManager.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public class EditBoothInfoDialog {
        public event Action<string, string> OnBoothInfoUpdated;

        public VisualElement CreateContent(string currentUrl) {
            var content = new VisualElement();

            var title = new Label("Edit Booth Info") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label("Booth URL:") {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var hint = new Label("Format: [shopname].booth.pm/items/[itemid]") {
                style = {
                    fontSize = 10,
                    color = Color.gray,
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