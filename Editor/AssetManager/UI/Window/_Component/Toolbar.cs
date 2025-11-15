using System;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Toolbar : VisualElement {
        public Action requestRefresh;
        public Toolbar() {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.height = 28;

            var refreshButton = new Button(() => requestRefresh?.Invoke()) {
                text = "Refresh",
                style = {
                    marginRight = 6
                }
            };
            Add(refreshButton);
        }
    }
}
