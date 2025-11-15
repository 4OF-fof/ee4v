using System;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Toolbar : VisualElement {
        public Action requestRefresh;
        public Action<string> requestFilter;

        public Toolbar() {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.height = 28;

            var refreshButton = new Button() {
                text = "Refresh",
                style = {
                    marginRight = 6
                }
            };
            refreshButton.RegisterCallback<ClickEvent>(evt => requestRefresh?.Invoke());
            Add(refreshButton);

            var searchField = new TextField() {
                value = "",
                style = {
                    width = 200,
                    marginRight = 6
                }
            };
            searchField.RegisterValueChangedCallback(evt => {
                requestFilter?.Invoke(evt.newValue);
            });
            Add(searchField);
        }
    }
}
