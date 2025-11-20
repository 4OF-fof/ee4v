using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetLibrary {
        private readonly Dictionary<Ulid, AssetMetadata> _assetMetadataDict = new();
        private readonly Dictionary<Ulid, HashSet<Ulid>> _folderIndex = new();
        private readonly Dictionary<string, HashSet<Ulid>> _folderTagIndex = new();
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
            return idSet.Select(GetAsset).Where(asset => asset != null).ToList();
        }

        public List<BaseFolder> GetFoldersByTag(string tag) {
            if (string.IsNullOrEmpty(tag) || !_folderTagIndex.TryGetValue(tag, out var idSet))
                return new List<BaseFolder>();

            var result = new List<BaseFolder>();
            if (Libraries == null) return result;

            result.AddRange(idSet.Select(id => Libraries.GetFolder(id)).Where(folder => folder != null));
            return result;
        }

        public List<AssetMetadata> GetAssetsByFolder(Ulid folderId) {
            return !_folderIndex.TryGetValue(folderId, out var idSet)
                ? new List<AssetMetadata>()
                : idSet.Select(GetAsset).Where(asset => asset != null).ToList();
        }

        public List<string> GetAllTags() {
            return _tagIndex.Keys.Union(_folderTagIndex.Keys).Distinct().ToList();
        }

        public List<Ulid> GetAllFolders() {
            return _folderIndex.Keys.ToList();
        }

        public void RenameTag(string tag, string newTag) {
            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(newTag) || tag == newTag) return;

            if (_tagIndex.TryGetValue(tag, out var idSet)) {
                var ids = idSet.ToList();
                foreach (var id in ids) {
                    var asset = GetAsset(id);
                    if (asset == null) continue;
                    asset.AddTag(newTag);
                    asset.RemoveTag(tag);
                    if (!_tagIndex.TryGetValue(newTag, out var newSet)) {
                        newSet = new HashSet<Ulid>();
                        _tagIndex[newTag] = newSet;
                    }

                    newSet.Add(id);
                }

                _tagIndex.Remove(tag);
            }

            if (!_folderTagIndex.TryGetValue(tag, out var folderIdSet)) return;
            {
                var ids = folderIdSet.ToList();
                foreach (var id in ids) {
                    var folder = Libraries?.GetFolder(id);
                    if (folder == null) continue;
                    folder.AddTag(newTag);
                    folder.RemoveTag(tag);

                    if (!_folderTagIndex.TryGetValue(newTag, out var newSet)) {
                        newSet = new HashSet<Ulid>();
                        _folderTagIndex[newTag] = newSet;
                    }

                    newSet.Add(id);
                }

                _folderTagIndex.Remove(tag);
            }
        }

        public void SetLibrary(LibraryMetadata libraryMetadata) {
            Libraries = libraryMetadata;
            RebuildFolderIndex();
        }

        public void UnloadAssetLibrary() {
            _assetMetadataDict.Clear();
            ClearIndex();
            Libraries = null;
        }

        private void RebuildFolderIndex() {
            _folderTagIndex.Clear();
            if (Libraries == null) return;
            foreach (var folder in Libraries.FolderList) RegisterFolderRecursively(folder);
        }

        private void RegisterFolderRecursively(BaseFolder folder) {
            foreach (var tag in folder.Tags) {
                if (string.IsNullOrEmpty(tag)) continue;
                if (!_folderTagIndex.TryGetValue(tag, out var set)) {
                    set = new HashSet<Ulid>();
                    _folderTagIndex[tag] = set;
                }

                set.Add(folder.ID);
            }

            if (folder is not Folder f) return;
            foreach (var child in f.Children)
                RegisterFolderRecursively(child);
        }

        private void RegisterIndex(AssetMetadata asset) {
            RegisterTags(asset);
            RegisterFolder(asset);
        }

        private void UnregisterIndex(AssetMetadata asset) {
            UnregisterTags(asset);
            UnregisterFolder(asset);
        }

        private void ClearIndex() {
            _tagIndex.Clear();
            _folderIndex.Clear();
            _folderTagIndex.Clear();
        }

        private void RegisterTags(AssetMetadata asset) {
            if (asset == null) return;
            foreach (var tag in asset.Tags) {
                if (string.IsNullOrEmpty(tag)) continue;
                if (!_tagIndex.TryGetValue(tag, out var set)) {
                    set = new HashSet<Ulid>();
                    _tagIndex[tag] = set;
                }

                set.Add(asset.ID);
            }
        }

        private void UnregisterTags(AssetMetadata asset) {
            if (asset == null) return;
            foreach (var tag in asset.Tags) {
                if (string.IsNullOrEmpty(tag)) continue;
                if (!_tagIndex.TryGetValue(tag, out var set)) continue;
                set.Remove(asset.ID);
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
    }
}