using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetLibrary {
        public static readonly AssetLibrary Instance = new();

        private readonly Dictionary<Ulid, AssetMetadata> _assetMetadataDict = new();

        private AssetLibrary() {
        }

        public IReadOnlyCollection<AssetMetadata> Assets => _assetMetadataDict.Values;
        public LibraryMetadata Libraries { get; private set; }

        public void AddAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            _assetMetadataDict[assetMetadata.ID] = assetMetadata;
        }

        public void RemoveAsset(Ulid assetId) {
            if (_assetMetadataDict.TryGetValue(assetId, out var asset)) asset?.SetDeleted(true);
        }

        public void UpdateAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            if (_assetMetadataDict.ContainsKey(assetMetadata.ID)) _assetMetadataDict[assetMetadata.ID] = assetMetadata;
        }

        public AssetMetadata GetAsset(Ulid assetId) {
            return _assetMetadataDict.TryGetValue(assetId, out var asset) ? asset : null;
        }

        public void UpsertAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            _assetMetadataDict[assetMetadata.ID] = assetMetadata;
        }

        public List<string> GetAllTags() {
            var tags = new HashSet<string>();
            foreach (var tag in _assetMetadataDict.Values.SelectMany(asset => asset.Tags)) tags.Add(tag);

            return tags.ToList();
        }

        public void RenameTag(string tag, string newTag) {
            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(newTag) || tag == newTag) return;
            var allTags = GetAllTags();
            if (!allTags.Contains(tag) || allTags.Contains(newTag)) return;
            foreach (var asset in _assetMetadataDict.Values.Where(asset => asset.Tags.Contains(tag))) {
                asset.AddTag(newTag);
                asset.RemoveTag(tag);
            }
        }

        public void SetLibrary(LibraryMetadata libraryMetadata) {
            Libraries = libraryMetadata;
        }

        public void UnloadAssetLibrary() {
            _assetMetadataDict.Clear();
            Libraries = null;
        }
    }
}