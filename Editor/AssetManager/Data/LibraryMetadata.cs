using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Data {
    public class LibraryMetadata {
        private readonly List<FolderInfo> _folderInfo = new();
        private long _modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private string _libraryVersion = "1";
        
        public LibraryMetadata() { }

        [JsonConstructor]
        public LibraryMetadata(List<FolderInfo> folderInfo, long modificationTime, string libraryVersion) {
            _folderInfo = folderInfo ?? new List<FolderInfo>();
            _modificationTime = modificationTime;
            _libraryVersion = libraryVersion;
        }

        public IReadOnlyList<FolderInfo> FolderInfo => _folderInfo.AsReadOnly();
        public long ModificationTime => _modificationTime;
        public string LibraryVersion => _libraryVersion;

        public void AddFolder(FolderInfo folder) {
            if (folder == null) return;
            _folderInfo.Add(folder);
            Touch();
        }

        public void RemoveFolder(Ulid folderId) {
            _folderInfo.RemoveAll(f => f.ID == folderId);
            Touch();
        }

        public FolderInfo GetFolder(Ulid folderId) => _folderInfo.Find(f => f.ID == folderId);

        public void SetLibraryVersion(string newLibraryVersion) {
            _libraryVersion = newLibraryVersion;
            Touch();
        }

        public void Touch() {
            _modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public class FolderInfo {
        private readonly Ulid _id = Ulid.Generate();
        private string _name = "";
        private string _description = "";
        private readonly List<FolderInfo> _children = new();
        private long _modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private readonly List<string> _tags = new();
        
        public FolderInfo() { }
        
        [JsonConstructor]
        public FolderInfo(Ulid id, string name, string description, List<FolderInfo> children, long modificationTime, List<string> tags) {
            _id = id;
            _name = name;
            _description = description;
            _children = children ?? new List<FolderInfo>();
            _modificationTime = modificationTime;
            _tags = tags ?? new List<string>();
        }

        public Ulid ID => _id;
        public string Name => _name;
        public string Description => _description;
        public IReadOnlyList<FolderInfo> Children => _children.AsReadOnly();
        public long ModificationTime => _modificationTime;
        public IReadOnlyList<string> Tags => _tags.AsReadOnly();

        public void UpdateName(string newName) {
            _name = newName;
            Touch();
        }

        public void UpdateDescription(string newDescription) {
            _description = newDescription;
            Touch();
        }

        public void AddChild(FolderInfo folderInfo) {
            if (folderInfo == null) return;
            _children.Add(folderInfo);
            Touch();
        }

        public void RemoveChild(FolderInfo folderInfo) {
            if (_children.Remove(folderInfo)) {
                Touch();
            }
        }
        
        public void AddTag(string tag) {
            if (string.IsNullOrEmpty(tag) || _tags.Contains(tag)) return;
            _tags.Add(tag);
            Touch();
        }
        
        public void RemoveTag(string tag) {
            if (_tags.Remove(tag)) {
                Touch();
            }
        }

        public void UpdateTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(newTag) || _tags.Contains(newTag)) return;
            if (!_tags.Remove(oldTag)) return;
            _tags.Add(newTag);
            Touch();
        }

        public void Touch() {
            _modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}