using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Data {
    public class LibraryMetadata {
        private readonly List<FolderInfo> _folderInfo = new();

        public LibraryMetadata() {
        }

        public LibraryMetadata(LibraryMetadata metadata) {
            _folderInfo = metadata.FolderInfo.ToList();
            ModificationTime = metadata.ModificationTime;
            LibraryVersion = metadata.LibraryVersion;
        }

        [JsonConstructor]
        public LibraryMetadata(List<FolderInfo> folderInfo, long modificationTime, string libraryVersion) {
            _folderInfo = folderInfo ?? new List<FolderInfo>();
            ModificationTime = modificationTime;
            LibraryVersion = libraryVersion;
        }

        public IReadOnlyList<FolderInfo> FolderInfo => _folderInfo.AsReadOnly();
        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public string LibraryVersion { get; private set; } = "1";

        public void AddFolder(FolderInfo folder) {
            if (folder == null) return;
            _folderInfo.Add(folder);
            Touch();
        }

        public void RemoveFolder(Ulid folderId) {
            if (folderId == default) return;

            for (var i = 0; i < _folderInfo.Count; i++) {
                if (_folderInfo[i].ID == folderId) {
                    _folderInfo.RemoveAt(i);
                    Touch();
                    return;
                }

                if (!_folderInfo[i].RemoveChild(folderId)) continue;
                return;
            }
        }

        public FolderInfo GetFolder(Ulid folderId) {
            return folderId == default
                ? null
                : _folderInfo.Select(f => f.GetChild(folderId)).FirstOrDefault(found => found != null);
        }

        public void SetLibraryVersion(string newLibraryVersion) {
            LibraryVersion = newLibraryVersion;
            Touch();
        }

        private void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public class FolderInfo {
        private readonly List<FolderInfo> _children = new();
        private readonly List<string> _tags = new();

        public FolderInfo() {
        }

        public FolderInfo(FolderInfo folderInfo) {
            ID = folderInfo.ID;
            Name = folderInfo.Name;
            Description = folderInfo.Description;
            _children = folderInfo.Children.ToList();
            ModificationTime = folderInfo.ModificationTime;
            _tags = folderInfo.Tags.ToList();
        }

        [JsonConstructor]
        public FolderInfo(Ulid id, string name, string description, List<FolderInfo> children, long modificationTime,
            List<string> tags) {
            ID = id;
            Name = name;
            Description = description;
            _children = children ?? new List<FolderInfo>();
            ModificationTime = modificationTime;
            _tags = tags ?? new List<string>();
        }

        public Ulid ID { get; } = Ulid.Generate();

        public string Name { get; private set; } = "";

        public string Description { get; private set; } = "";

        public IReadOnlyList<FolderInfo> Children => _children.AsReadOnly();
        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public IReadOnlyList<string> Tags => _tags.AsReadOnly();

        public void SetName(string newName) {
            Name = newName;
            Touch();
        }

        public void SetDescription(string newDescription) {
            Description = newDescription;
            Touch();
        }

        public void AddChild(FolderInfo folderInfo) {
            if (folderInfo == null) return;
            _children.Add(folderInfo);
            Touch();
        }

        public bool RemoveChild(Ulid folderId) {
            for (var i = 0; i < _children.Count; i++) {
                if (_children[i].ID == folderId) {
                    _children.RemoveAt(i);
                    Touch();
                    return true;
                }

                if (!_children[i].RemoveChild(folderId)) continue;
                Touch();
                return true;
            }

            return false;
        }

        public FolderInfo GetChild(Ulid folderId) {
            return ID == folderId
                ? this
                : _children.Select(c => c.GetChild(folderId)).FirstOrDefault(found => found != null);
        }

        public void AddTag(string tag) {
            if (string.IsNullOrEmpty(tag) || _tags.Contains(tag)) return;
            _tags.Add(tag);
            Touch();
        }

        public void RemoveTag(string tag) {
            if (_tags.Remove(tag)) Touch();
        }

        private void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}