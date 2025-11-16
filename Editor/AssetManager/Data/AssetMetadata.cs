using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetMetadata {
        private readonly List<string> _tags = new();

        public AssetMetadata() {
        }

        [JsonConstructor]
        public AssetMetadata(Ulid id, string name, string description, long size, string ext, Ulid? folder,
            List<string> tags, bool isDeleted, long modificationTime) {
            Name = name;
            ID = id;
            Description = description;
            Size = size;
            Ext = ext;
            Folder = folder;
            _tags = tags ?? new List<string>();
            IsDeleted = isDeleted;
            ModificationTime = modificationTime;
        }

        public Ulid ID { get; } = Ulid.Generate();

        public string Name { get; private set; } = "";

        public string Description { get; private set; } = "";

        public long Size { get; private set; }

        public string Ext { get; private set; } = "";

        public Ulid? Folder { get; private set; }
        public IReadOnlyList<string> Tags => _tags.AsReadOnly();
        public bool IsDeleted { get; private set; }

        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public void UpdateName(string newName) {
            Name = newName;
            Touch();
        }

        public void UpdateDescription(string newDescription) {
            Description = newDescription;
            Touch();
        }

        public void UpdateSize(long newSize) {
            Size = newSize;
            Touch();
        }

        public void UpdateExt(string newExt) {
            Ext = newExt;
            Touch();
        }

        public void UpdateFolder(Ulid? newFolder) {
            Folder = newFolder;
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

        public void SetDeleted(bool deleted) {
            IsDeleted = deleted;
            Touch();
        }

        public void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}