using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetLibrary {
        private readonly Dictionary<Ulid, AssetMetadata> _assetMetadataDict = new();
        private readonly Dictionary<Ulid, HashSet<Ulid>> _folderIndex = new();
        private readonly Dictionary<string, HashSet<Ulid>> _tagIndex = new();

        public IReadOnlyCollection<AssetMetadata> Assets => _assetMetadataDict.Values;
        public LibraryMetadata Libraries { get; private set; }

        public void AddAsset(AssetMetadata assetMetadata) {
            if (!_assetMetadataDict.TryAdd(assetMetadata.ID, assetMetadata)) return;
            RegisterIndex(assetMetadata);
        }

        public void RemoveAsset(Ulid assetId) {
            if (_assetMetadataDict.TryGetValue(assetId, out var asset)) UnregisterIndex(asset);
            _assetMetadataDict.Remove(assetId);
        }

        public void UpdateAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            if (!_assetMetadataDict.TryGetValue(assetMetadata.ID, out var oldAssetMetadata)) return;
            UnregisterIndex(oldAssetMetadata);
            _assetMetadataDict[assetMetadata.ID] = assetMetadata;
            RegisterIndex(assetMetadata);
        }

        public AssetMetadata GetAsset(Ulid assetId) {
            return _assetMetadataDict.GetValueOrDefault(assetId);
        }

        public void UpsertAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            if (_assetMetadataDict.TryGetValue(assetMetadata.ID, out var oldAssetMetadata))
                UnregisterIndex(oldAssetMetadata);
            _assetMetadataDict[assetMetadata.ID] = assetMetadata;
            RegisterIndex(assetMetadata);
        }

        public List<AssetMetadata> GetAssetsByTag(string tag) {
            if (string.IsNullOrEmpty(tag) || !_tagIndex.TryGetValue(tag, out var idSet))
                return new List<AssetMetadata>();

            return idSet
                .Where(id => _assetMetadataDict.ContainsKey(id))
                .Select(GetAsset)
                .Where(asset => asset != null)
                .ToList();
        }

        public List<BaseFolder> GetFoldersByTag(string tag) {
            if (string.IsNullOrEmpty(tag) || !_tagIndex.TryGetValue(tag, out var idSet))
                return new List<BaseFolder>();

            if (Libraries == null) return new List<BaseFolder>();

            return idSet
                .Select(id => Libraries.GetFolder(id))
                .Where(f => f != null)
                .ToList();
        }

        public List<AssetMetadata> GetAssetsByFolder(Ulid folderId) {
            return !_folderIndex.TryGetValue(folderId, out var idSet)
                ? new List<AssetMetadata>()
                : idSet.Select(GetAsset).Where(asset => asset != null).ToList();
        }

        public List<string> GetAllTags() {
            return _tagIndex.Keys.ToList();
        }

        public List<Ulid> GetAllFolders() {
            return _folderIndex.Keys.ToList();
        }

        public void RenameTag(string tag, string newTag) {
            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(newTag) || tag == newTag) return;
            if (!_tagIndex.TryGetValue(tag, out var idSet)) return;

            var ids = idSet.ToList();
            foreach (var id in ids) {
                var asset = GetAsset(id);
                if (asset != null) {
                    asset.AddTag(newTag);
                    asset.RemoveTag(tag);
                }
                else {
                    var folder = Libraries?.GetFolder(id);
                    if (folder != null) {
                        folder.AddTag(newTag);
                        folder.RemoveTag(tag);
                    }
                }

                if (!_tagIndex.TryGetValue(newTag, out var newSet)) {
                    newSet = new HashSet<Ulid>();
                    _tagIndex[newTag] = newSet;
                }

                newSet.Add(id);
            }

            _tagIndex.Remove(tag);
        }

        public void SetLibrary(LibraryMetadata libraryMetadata) {
            if (Libraries != null)
                foreach (var folder in Libraries.FolderList)
                    UnregisterFolderIndexRecursively(folder);

            Libraries = libraryMetadata;

            if (Libraries == null) return;
            foreach (var folder in Libraries.FolderList) RegisterFolderIndexRecursively(folder);
        }

        public void UnloadAssetLibrary() {
            _assetMetadataDict.Clear();
            ClearIndex();
            Libraries = null;
        }

        private void RegisterIndex(AssetMetadata asset) {
            RegisterTags(asset.Tags, asset.ID);
            RegisterFolder(asset);
        }

        private void UnregisterIndex(AssetMetadata asset) {
            UnregisterTags(asset.Tags, asset.ID);
            UnregisterFolder(asset);
        }

        private void ClearIndex() {
            _tagIndex.Clear();
            _folderIndex.Clear();
        }

        private void RegisterTags(IReadOnlyList<string> tags, Ulid id) {
            if (tags == null) return;
            foreach (var tag in tags) {
                if (string.IsNullOrEmpty(tag)) continue;
                if (!_tagIndex.TryGetValue(tag, out var set)) {
                    set = new HashSet<Ulid>();
                    _tagIndex[tag] = set;
                }

                set.Add(id);
            }
        }

        private void UnregisterTags(IReadOnlyList<string> tags, Ulid id) {
            if (tags == null) return;
            foreach (var tag in tags) {
                if (string.IsNullOrEmpty(tag)) continue;
                if (!_tagIndex.TryGetValue(tag, out var set)) continue;
                set.Remove(id);
                if (set.Count == 0) _tagIndex.Remove(tag);
            }
        }

        private void RegisterFolder(AssetMetadata asset) {
            if (asset == null) return;
            var folderId = asset.Folder;
            if (!_folderIndex.TryGetValue(folderId, out var set)) {
                set = new HashSet<Ulid>();
                _folderIndex[folderId] = set;
            }

            set.Add(asset.ID);
        }

        private void UnregisterFolder(AssetMetadata asset) {
            if (asset == null) return;
            var folderId = asset.Folder;
            if (!_folderIndex.TryGetValue(folderId, out var set)) return;
            set.Remove(asset.ID);
            if (set.Count == 0) _folderIndex.Remove(folderId);
        }

        private void RegisterFolderIndexRecursively(BaseFolder folder) {
            RegisterTags(folder.Tags, folder.ID);
            if (folder is not Folder f) return;
            foreach (var child in f.Children) RegisterFolderIndexRecursively(child);
        }

        private void UnregisterFolderIndexRecursively(BaseFolder folder) {
            UnregisterTags(folder.Tags, folder.ID);
            if (folder is not Folder f) return;
            foreach (var child in f.Children) UnregisterFolderIndexRecursively(child);
        }
    }
}