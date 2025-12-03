using _4OF.ee4v.AssetManager.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.AssetInfo {
    public class Thumbnail : VisualElement {
        private readonly VisualElement _thumbnailContainer;

        public Thumbnail() {
            _thumbnailContainer = new VisualElement {
                style = {
                    alignSelf = Align.Center,
                    width = 150,
                    height = 150,
                    marginBottom = 10,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    overflow = Overflow.Hidden,
                    backgroundSize = new BackgroundSize(BackgroundSizeType.Contain),
                    backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat),
                    backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center),
                    backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center)
                }
            };
            Add(_thumbnailContainer);
        }

        public void SetImage(Texture2D texture, bool isFolder, bool isEmpty = false) {
            if (texture != null)
                _thumbnailContainer.style.backgroundImage = new StyleBackground(texture);
            else
                _thumbnailContainer.style.backgroundImage =
                    new StyleBackground(TextureService.GetDefaultFallback(isFolder, isEmpty));
        }
    }
}