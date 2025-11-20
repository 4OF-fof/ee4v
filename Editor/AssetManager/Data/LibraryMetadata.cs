using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Data {
    public class LibraryMetadata {
        private readonly List<BaseFolder> _folderInfo = new();

        public LibraryMetadata() {
        }

        public LibraryMetadata(LibraryMetadata metadata) {
            _folderInfo = metadata.FolderList.ToList();
            ModificationTime = metadata.ModificationTime;
            LibraryVersion = metadata.LibraryVersion;
        }

        [JsonConstructor]
        public LibraryMetadata(List<BaseFolder> folderList, long modificationTime, string libraryVersion) {
            _folderInfo = folderList ?? new List<BaseFolder>();
            ModificationTime = modificationTime;
            LibraryVersion = libraryVersion;
        }

        public IReadOnlyList<BaseFolder> FolderList => _folderInfo.AsReadOnly();
        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public string LibraryVersion { get; private set; } = "1";

        public void AddFolder(BaseFolder folder) {
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

                if (_folderInfo[i] is not Folder folderWithChildren) continue;
                if (!folderWithChildren.RemoveChild(folderId)) continue;
                return;
            }
        }

        public BaseFolder GetFolder(Ulid folderId) {
            if (folderId == default) return null;
            foreach (var current in _folderInfo) {
                if (current.ID == folderId) return current;
                if (current is not Folder folderWithChildren) continue;
                var found = folderWithChildren.GetChild(folderId);
                if (found != null) return found;
            }

            return null;
        }

        public void SetLibraryVersion(string newLibraryVersion) {
            LibraryVersion = newLibraryVersion;
            Touch();
        }

        private void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public class BaseFolder {
        private readonly List<string> _tags = new();

        public BaseFolder() {
        }

        public BaseFolder(BaseFolder baseFolder) {
            ID = baseFolder.ID;
            Name = baseFolder.Name;
            Description = baseFolder.Description;
            ModificationTime = baseFolder.ModificationTime;
            _tags = new List<string>(baseFolder.Tags);
        }

        [JsonConstructor]
        public BaseFolder(Ulid id, string name, string description, long modificationTime, List<string> tags) {
            ID = id;
            Name = name;
            Description = description;
            ModificationTime = modificationTime;
            _tags = tags ?? new List<string>();
        }

        public Ulid ID { get; } = Ulid.Generate();
        public string Name { get; private set; } = "";
        public string Description { get; private set; } = "";
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

        public void AddTag(string tag) {
            if (string.IsNullOrEmpty(tag) || _tags.Contains(tag)) return;
            _tags.Add(tag);
            Touch();
        }

        public void RemoveTag(string tag) {
            if (_tags.Remove(tag)) Touch();
        }

        protected void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public class Folder : BaseFolder {
        private readonly List<BaseFolder> _children = new();

        public Folder() {
        }

        public Folder(Folder folderInfo) : base(folderInfo) {
            _children = folderInfo.Children
                .Select(child => child switch {
                    Folder f          => new Folder(f),
                    BoothItemFolder b => new BoothItemFolder(b),
                    _                 => new BaseFolder(child)
                })
                .ToList();
        }

        [JsonConstructor]
        public Folder(Ulid id, string name, string description, long modificationTime, List<string> tags,
            List<BaseFolder> children)
            : base(id, name, description, modificationTime, tags) {
            _children = children ?? new List<BaseFolder>();
        }

        public IReadOnlyList<BaseFolder> Children => _children.AsReadOnly();

        public void AddChild(BaseFolder folderInfo) {
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

                if (_children[i] is not Folder childFolder || !childFolder.RemoveChild(folderId)) continue;
                Touch();
                return true;
            }

            return false;
        }

        public BaseFolder GetChild(Ulid folderId) {
            if (ID == folderId) return this;
            foreach (var c in _children) {
                if (c.ID == folderId) return c;
                if (c is not Folder childFolder) continue;
                var found = childFolder.GetChild(folderId);
                if (found != null) return found;
            }

            return null;
        }
    }

    public class BoothItemFolder : BaseFolder {
        public BoothItemFolder() {
        }

        public BoothItemFolder(BoothItemFolder boothItemFolder) : base(boothItemFolder) {
            ItemId = boothItemFolder.ItemId;
            ShopDomain = boothItemFolder.ShopDomain;
            ShopName = boothItemFolder.ShopName;
        }

        [JsonConstructor]
        public BoothItemFolder(Ulid id, string name, string description, long modificationTime, List<string> tags,
            string itemId,
            string shopDomain, string shopName) : base(id, name, description, modificationTime, tags) {
            ItemId = itemId ?? "";
            ShopDomain = shopDomain ?? "";
            ShopName = shopName ?? "";
        }

        public string ItemId { get; private set; } = "";
        public string ShopDomain { get; private set; } = "";
        public string ShopName { get; private set; } = "";

        public void SetItemId(string itemId) {
            ItemId = itemId ?? "";
            Touch();
        }

        public void SetShopDomain(string domain) {
            ShopDomain = domain ?? "";
            Touch();
        }

        public void SetShopName(string name) {
            ShopName = name ?? "";
            Touch();
        }
    }
}