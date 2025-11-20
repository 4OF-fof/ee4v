using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetCard : VisualElement {
        private readonly VisualElement _innerContainer;
        private readonly Label _nameLabel;
        private readonly VisualElement _thumbnail;

        public AssetCard() {
            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.paddingTop = 5;
            style.paddingBottom = 5;

            _innerContainer = new VisualElement {
                style = {
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5,
                    overflow = Overflow.Hidden,
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new StyleColor(Color.clear),
                    borderBottomColor = new StyleColor(Color.clear),
                    borderLeftColor = new StyleColor(Color.clear),
                    borderRightColor = new StyleColor(Color.clear)
                }
            };
            Add(_innerContainer);

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
            _innerContainer.Add(_thumbnail);

            _nameLabel = new Label {
                style = {
                    whiteSpace = WhiteSpace.Normal,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2,
                    height = 38,
                    flexShrink = 0,
                    fontSize = 11,
                    color = new StyleColor(new Color(0.9f, 0.9f, 0.9f))
                }
            };
            _innerContainer.Add(_nameLabel);
        }

        public void SetData(string itemName) {
            _nameLabel.text = itemName;
            tooltip = itemName;
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

        public void SetSelected(bool selected) {
            if (selected) {
                _innerContainer.style.backgroundColor = ColorPreset.ItemSelectedBackGround;
                var borderColor = ColorPreset.ItemSelectedBorder;
                _innerContainer.style.borderTopColor = borderColor;
                _innerContainer.style.borderBottomColor = borderColor;
                _innerContainer.style.borderLeftColor = borderColor;
                _innerContainer.style.borderRightColor = borderColor;
            }
            else {
                _innerContainer.style.backgroundColor = Color.clear;
                _innerContainer.style.borderTopColor = Color.clear;
                _innerContainer.style.borderBottomColor = Color.clear;
                _innerContainer.style.borderLeftColor = Color.clear;
                _innerContainer.style.borderRightColor = Color.clear;
            }
        }
    }
}