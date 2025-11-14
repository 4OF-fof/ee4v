using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Data {
    [Serializable]
    public class LibraryMetadata {
        [SerializeField] private List<FolderInfo> folderInfo = new();
        [SerializeField] private long modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        [SerializeField] private string libraryVersion = "1.0.0";

        public IReadOnlyList<FolderInfo> FolderInfo => folderInfo;
        public long ModificationTime => modificationTime;
        public string LibraryVersion => libraryVersion;

        public void AddFolder(FolderInfo folder) {
            if (folder == null) return;
            folderInfo.Add(folder);
            Touch();
        }

        public void RemoveFolder(Ulid folderId) {
            folderInfo.RemoveAll(f => f.ID == folderId);
            Touch();
        }

        public FolderInfo GetFolder(Ulid folderId) => folderInfo.Find(f => f.ID == folderId);

        public void SetLibraryVersion(string newLibraryVersion) {
            libraryVersion = newLibraryVersion;
            Touch();
        }

        public void Touch() {
            modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    [Serializable]
    public class FolderInfo {
        [SerializeField] private string id = Ulid.Generate().ToString();
        [SerializeField] private string name = "";
        [SerializeField] private string description = "";
        [SerializeField] private List<string> children = new();
        [SerializeField] private long modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        [SerializeField] private List<string> tags = new();

        public Ulid ID => Ulid.Parse(id);
        public string Name => name;
        public string Description => description;
        public IReadOnlyList<Ulid> Children => children.ConvertAll(Ulid.Parse);
        public long ModificationTime => modificationTime;
        public IReadOnlyList<string> Tags => tags.AsReadOnly();

        public void UpdateName(string newName) {
            name = newName;
            Touch();
        }

        public void UpdateDescription(string newDescription) {
            description = newDescription;
            Touch();
        }

        public void AddChild(Ulid child) {
            var childStr = child.ToString();
            if (children.Contains(childStr)) return;
            children.Add(childStr);
            Touch();
        }

        public void RemoveChild(Ulid child) {
            var childStr = child.ToString();
            if (children.Remove(childStr)) {
                Touch();
            }
        }
        
        public void AddTag(string tag) {
            if (string.IsNullOrEmpty(tag) || tags.Contains(tag)) return;
            tags.Add(tag);
            Touch();
        }
        
        public void RemoveTag(string tag) {
            if (tags.Remove(tag)) {
                Touch();
            }
        }

        public void UpdateTag(string oldTag, string newTag) {
            if (string.IsNullOrEmpty(newTag) || tags.Contains(newTag)) return;
            if (!tags.Remove(oldTag)) return;
            tags.Add(newTag);
            Touch();
        }

        public void Touch() {
            modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}