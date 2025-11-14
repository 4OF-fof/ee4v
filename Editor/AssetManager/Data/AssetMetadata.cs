using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetMetadata {
        private string name = "";
        private string id = Ulid.Generate().ToString();
        private string description = "";
        private long size;
        private string ext = "";
        private string folder = "";
        private List<string> tags = new();
        private bool isDeleted;
        private long modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public Ulid ID => Ulid.Parse(id);
        public string Name => name;
        public string Description => description;
        public long Size => size;
        public string Ext => ext;
        public Ulid? Folder => Ulid.TryParse(folder, out var ulid) ? ulid : null;
        public IReadOnlyList<string> Tags => tags.AsReadOnly();
        public bool IsDeleted => isDeleted;
        public long ModificationTime => modificationTime;

        public void UpdateName(string newName) {
            name = newName;
            Touch();
        }

        public void UpdateDescription(string newDescription) {
            description = newDescription;
            Touch();
        }

        public void UpdateSize(long newSize) {
            size = newSize;
            Touch();
        }

        public void UpdateExt(string newExt) {
            ext = newExt;
            Touch();
        }

        public void UpdateFolder(Ulid newFolder) {
            folder = newFolder.ToString();
            Touch();
        }

        public void AddTag(string tag) {
            if (string.IsNullOrEmpty(tag) || tags.Contains(tag)) return;
            tags.Add(tag);
            Touch();
        }

        public void RemoveTag(string tag) {
            if (tags.Remove(tag)) Touch();
        }
        
        public void UpdateTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(newTag) || tags.Contains(newTag)) return;
            if (!tags.Remove(oldTag)) return;
            tags.Add(newTag);
            Touch();
        }

        public void SetDeleted(bool deleted) {
            isDeleted = deleted;
            Touch();
        }

        public void Touch() {
            modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}