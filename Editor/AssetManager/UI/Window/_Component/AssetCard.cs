using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetCard : VisualElement {
        private readonly Label _nameLabel;
        private readonly VisualElement _thumbnail;

        public AssetCard() {
            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.paddingTop = 5;
            style.paddingBottom = 5;

            var innerContainer = new VisualElement {
                style = {
                    backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f)),
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5,
                    overflow = Overflow.Hidden,
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column 
                }
            };
            Add(innerContainer);

            _thumbnail = new VisualElement {
                style = {
                    flexGrow = 1,
                    width = Length.Percent(100),
                    backgroundColor = new StyleColor(Color.gray),
                    backgroundSize = new BackgroundSize(BackgroundSizeType.Contain),
                    backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat),
                    backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center),
                    backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center)
                }
            };
            innerContainer.Add(_thumbnail);

            _nameLabel = new Label {
                style = {
                    whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2,
                    height = 40,
                    flexShrink = 0,
                    fontSize = 11,
                    color = new StyleColor(new Color(0.9f, 0.9f, 0.9f))
                }
            };
            innerContainer.Add(_nameLabel);

            innerContainer.RegisterCallback<MouseEnterEvent>(_ =>
                innerContainer.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)));
            innerContainer.RegisterCallback<MouseLeaveEvent>(_ =>
                innerContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f)));
        }

        public void SetData(string itemName) {
            _nameLabel.text = itemName;
        }

        public void SetThumbnail(Texture2D texture) {
            if (texture == null) {
                _thumbnail.style.backgroundImage = null;
                _thumbnail.style.backgroundColor = new StyleColor(Color.gray);
                return;
            }
            _thumbnail.style.backgroundImage = new StyleBackground(texture);
            _thumbnail.style.backgroundColor = new StyleColor(Color.clear);
        }
    }
}