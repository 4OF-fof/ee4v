using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Utility;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Service {
    public class AssetService {
        private readonly IAssetRepository _repository;

        public AssetService(IAssetRepository repository) {
            _repository = repository;
        }

        public void CreateAsset(string path) {
            _repository.CreateAssetFromFile(path);
        }

        public void DeleteAsset(Ulid assetId) {
            _repository.DeleteAsset(assetId);
        }

        public void RemoveAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;
            asset.SetDeleted(true);
            _repository.SaveAsset(asset);
        }

        public void RestoreAsset(Ulid assetId) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;
            asset.SetDeleted(false);
            _repository.SaveAsset(asset);
        }

        public void UpdateAsset(AssetMetadata newAsset) {
            if (!AssetValidationService.IsValidAssetName(newAsset.Name)) return;
            var oldAsset = _repository.GetAsset(newAsset.ID);
            if (oldAsset == null) return;

            if (oldAsset.Name != newAsset.Name) {
                _repository.RenameAssetFile(newAsset.ID, newAsset.Name);
            }
            _repository.SaveAsset(newAsset);
        }

        public void SetAssetName(Ulid assetId, string newName) {
            if (!AssetValidationService.IsValidAssetName(newName)) return;
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            _repository.RenameAssetFile(assetId, newName);
            asset.SetName(newName);
            _repository.SaveAsset(asset);
        }

        public void SetDescription(Ulid assetId, string newDescription) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;
            asset.SetDescription(newDescription);
            _repository.SaveAsset(asset);
        }

        public void SetFolder(Ulid assetId, Ulid newFolder) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;
            asset.SetFolder(newFolder);
            _repository.SaveAsset(asset);
        }

        public void AddTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;
            asset.AddTag(tag);
            _repository.SaveAsset(asset);
        }

        public void RemoveTag(Ulid assetId, string tag) {
            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;
            asset.RemoveTag(tag);
            _repository.SaveAsset(asset);
        }

        public void RenameTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(oldTag) || string.IsNullOrEmpty(newTag) || oldTag == newTag) return;

            foreach (var asset in _repository.GetAllAssets()) {
                if (!asset.Tags.Contains(oldTag)) continue;
                
                asset.RemoveTag(oldTag);
                asset.AddTag(newTag);
                _repository.SaveAsset(asset);
            }
        }

        public void SetBoothShopDomain(Ulid assetId, string shopURL) {
            if (BoothUtility.ClassifyBoothUrl(shopURL) != BoothUtility.BoothUrlType.ShopUrl) return;

            var regex = new Regex(@"https?://([^\.]+)\.booth\.pm", RegexOptions.IgnoreCase);
            var match = regex.Match(shopURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            asset.BoothData.SetShopDomain(match.Groups[1].Value);
            _repository.SaveAsset(asset);
        }

        public void SetBoothItemId(Ulid assetId, string itemURL) {
            if (BoothUtility.ClassifyBoothUrl(itemURL) != BoothUtility.BoothUrlType.ItemUrl) return;

            var regex = new Regex(@"items/(\d+)");
            var match = regex.Match(itemURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            asset.BoothData.SetItemID(match.Groups[1].Value);
            _repository.SaveAsset(asset);
        }

        public void SetBoothDownloadId(Ulid assetId, string downloadURL) {
            if (BoothUtility.ClassifyBoothUrl(downloadURL) != BoothUtility.BoothUrlType.DownloadUrl) return;

            var regex = new Regex(@"downloadables/(\d+)", RegexOptions.IgnoreCase);
            var match = regex.Match(downloadURL);
            if (!match.Success) return;

            var asset = _repository.GetAsset(assetId);
            if (asset == null) return;

            asset.BoothData.SetDownloadID(match.Groups[1].Value);
            _repository.SaveAsset(asset);
        }
    }
}