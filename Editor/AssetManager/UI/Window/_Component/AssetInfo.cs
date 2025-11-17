using _4OF.ee4v.AssetManager.Data;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetInfo : VisualElement {
        private readonly Label _descLabel;
        private readonly Label _extLabel;
        private readonly Label _nameLabel;
        private readonly Label _sizeLabel;
        private readonly Label _tagsLabel;

        public AssetInfo() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 8;
            style.paddingTop = 8;

            _nameLabel = new Label("Name: -");
            Add(_nameLabel);

            _descLabel = new Label("Description: -");
            Add(_descLabel);

            _sizeLabel = new Label("Size: -");
            Add(_sizeLabel);

            _extLabel = new Label("Ext: -");
            Add(_extLabel);

            _tagsLabel = new Label("Tags: -");
            Add(_tagsLabel);
        }

        public void SetAsset(AssetMetadata asset) {
            if (asset == null) {
                _nameLabel.text = "Name: -";
                _descLabel.text = "Description: -";
                _sizeLabel.text = "Size: -";
                _extLabel.text = "Ext: -";
                _tagsLabel.text = "Tags: -";
                return;
            }

            _nameLabel.text = $"Name: {asset.Name}";
            _descLabel.text = $"Description: {asset.Description}";
            _sizeLabel.text = $"Size: {asset.Size}";
            _extLabel.text = $"Ext: {asset.Ext}";
            _tagsLabel.text =
                $"Tags: {(asset.Tags != null && asset.Tags.Count > 0 ? string.Join(", ", asset.Tags) : "-")}";
        }
    }
}