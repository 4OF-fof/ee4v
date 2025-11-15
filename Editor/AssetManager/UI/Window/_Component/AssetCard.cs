using UnityEngine;
using UnityEngine.UIElements;
using _4OF.ee4v.AssetManager.Data;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetCard : VisualElement {
        public AssetCard(AssetMetadata metadata) {
            style.width = 140;
            style.height = 160;
            style.marginRight = 8;
            style.marginBottom = 8;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;

            var nameLabel = new Label(metadata?.Name ?? "(unnamed)") {
                style = {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginTop = 8,
                    width = 128
                }
            };

            var thumb = new Thumbnail(metadata);
            Add(thumb);
            Add(nameLabel);
        }
    }
}
