using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.OldData;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetInfo : VisualElement {
        private readonly Label _boothDownloadUrlLabel;
        private readonly Label _boothItemUrlLabel;
        private readonly Label _boothShopUrlLabel;
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

            _boothShopUrlLabel = new Label("Booth Shop URL: -");
            Add(_boothShopUrlLabel);
            _boothItemUrlLabel = new Label("Booth Item URL: -");
            Add(_boothItemUrlLabel);
            _boothDownloadUrlLabel = new Label("Booth Download URL: -");
            Add(_boothDownloadUrlLabel);
        }

        public void SetAsset(AssetMetadata asset) {
            if (asset == null) {
                _nameLabel.text = "Name: -";
                _descLabel.text = "Description: -";
                _sizeLabel.text = "Size: -";
                _extLabel.text = "Ext: -";
                _tagsLabel.text = "Tags: -";
                _boothShopUrlLabel.text = "Booth Shop URL: -";
                _boothItemUrlLabel.text = "Booth Item URL: -";
                _boothDownloadUrlLabel.text = "Booth Download URL: -";
                return;
            }

            _nameLabel.text = $"Name: {asset.Name}";
            _descLabel.text = $"Description: {asset.Description}";
            _sizeLabel.text = $"Size: {asset.Size}";
            _extLabel.text = $"Ext: {asset.Ext}";
            _tagsLabel.text =
                $"Tags: {(asset.Tags != null && asset.Tags.Count > 0 ? string.Join(", ", asset.Tags) : "-")}";

            _boothShopUrlLabel.text =
                $"Booth Shop URL: {(asset.BoothData != null && !string.IsNullOrEmpty(asset.BoothData.ShopURL) ? asset.BoothData.ShopURL : "-")}";
            _boothItemUrlLabel.text =
                $"Booth Item URL: {(asset.BoothData != null && !string.IsNullOrEmpty(asset.BoothData.ItemURL) ? asset.BoothData.ItemURL : "-")}";
            _boothDownloadUrlLabel.text =
                $"Booth Download URL: {(asset.BoothData != null && !string.IsNullOrEmpty(asset.BoothData.DownloadURL) ? asset.BoothData.DownloadURL : "-")}";
        }
    }
}