using UnityEngine;
using UnityEngine.UIElements;
using _4OF.ee4v.AssetManager.Data;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Thumbnail : VisualElement {
        public Thumbnail(AssetMetadata metadata) {
            style.width = 128;
            style.height = 96;
            style.marginTop = 8;
            style.backgroundColor = new Color(0.65f, 0.75f, 0.85f);
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
        }
    }
}
