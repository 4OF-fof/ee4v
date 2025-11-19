using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetCard : VisualElement {
        private readonly Label _nameLabel;

        private readonly VisualElement _thumbnail;

        public AssetCard() {
            style.width = 100;
            style.marginRight = 5;
            style.marginBottom = 5;
            style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f));
            style.borderTopLeftRadius = 5;
            style.borderTopRightRadius = 5;
            style.borderBottomLeftRadius = 5;
            style.borderBottomRightRadius = 5;
            style.overflow = Overflow.Hidden;

            _thumbnail = new VisualElement {
                style = {
                    height = 80,
                    backgroundColor = new StyleColor(Color.gray) // Placeholder color
                }
            };
            Add(_thumbnail);

            _nameLabel = new Label {
                style = {
                    whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2
                }
            };
            Add(_nameLabel);

            RegisterCallback<MouseEnterEvent>(evt =>
                style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)));
            RegisterCallback<MouseLeaveEvent>(evt =>
                style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f)));
        }

        public void SetData(string itemName) {
            _nameLabel.text = itemName;
        }

        public new class UxmlFactory : UxmlFactory<AssetCard, UxmlTraits> {
        }
    }
}