using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetMetadata {
        private readonly Ulid _id = Ulid.Generate();
        private string _name = "";
        private string _description = "";
        private long _size;
        private string _ext = "";
        private string _folder = "";
        private readonly List<string> _tags = new();
        private bool _isDeleted;
        private long _modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        public AssetMetadata() { }
        
        [JsonConstructor]
        public AssetMetadata(Ulid id, string name, string description, long size, string ext, string folder, List<string> tags, bool isDeleted, long modificationTime) {
            _name = name;
            _id = id;
            _description = description;
            _size = size;
            _ext = ext;
            _folder = folder;
            _tags = tags ?? new List<string>();
            _isDeleted = isDeleted;
            _modificationTime = modificationTime;
        }

        public Ulid ID => _id;
        public string Name => _name;
        public string Description => _description;
        public long Size => _size;
        public string Ext => _ext;
        public Ulid? Folder => Ulid.TryParse(_folder, out var ulid) ? ulid : null;
        public IReadOnlyList<string> Tags => _tags.AsReadOnly();
        public bool IsDeleted => _isDeleted;
        public long ModificationTime => _modificationTime;

        public void UpdateName(string newName) {
            _name = newName;
            Touch();
        }

        public void UpdateDescription(string newDescription) {
            _description = newDescription;
            Touch();
        }

        public void UpdateSize(long newSize) {
            _size = newSize;
            Touch();
        }

        public void UpdateExt(string newExt) {
            _ext = newExt;
            Touch();
        }

        public void UpdateFolder(Ulid newFolder) {
            _folder = newFolder.ToString();
            Touch();
        }

        public void AddTag(string tag) {
            if (string.IsNullOrEmpty(tag) || _tags.Contains(tag)) return;
            _tags.Add(tag);
            Touch();
        }

        public void RemoveTag(string tag) {
            if (_tags.Remove(tag)) Touch();
        }
        
        public void UpdateTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(newTag) || _tags.Contains(newTag)) return;
            if (!_tags.Remove(oldTag)) return;
            _tags.Add(newTag);
            Touch();
        }

        public void SetDeleted(bool deleted) {
            _isDeleted = deleted;
            Touch();
        }

        public void Touch() {
            _modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}