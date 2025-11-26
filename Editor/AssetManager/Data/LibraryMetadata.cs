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
        private long ModificationTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private string LibraryVersion { get; set; } = "1";

        public void AddFolder(BaseFolder folder) {
            if (folder == null) return;
            _folderInfo.Add(folder);
            Touch();
        }

        public void InsertRootFolderAt(int index, BaseFolder folder) {
            if (folder == null) return;
            if (index < 0) index = 0;
            if (index > _folderInfo.Count) index = _folderInfo.Count;
            _folderInfo.Insert(index, folder);
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
            Name = string.IsNullOrWhiteSpace(baseFolder.Name) ? "New Folder" : baseFolder.Name;
            Description = baseFolder.Description;
            ModificationTime = baseFolder.ModificationTime;
            _tags = new List<string>(baseFolder.Tags);
        }

        [JsonConstructor]
        public BaseFolder(Ulid id, string name, string description, long modificationTime, List<string> tags) {
            ID = id;
            Name = string.IsNullOrWhiteSpace(name) ? "New Folder" : name;
            Description = description;
            ModificationTime = modificationTime;
            _tags = tags ?? new List<string>();
        }

        public Ulid ID { get; } = Ulid.Generate();
        public string Name { get; private set; } = "New Folder";
        public string Description { get; private set; } = "";
        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public IReadOnlyList<string> Tags => _tags.AsReadOnly();

        public void SetName(string newName) {
            if (string.IsNullOrWhiteSpace(newName)) return;
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

        public void InsertChildAt(int index, BaseFolder folderInfo) {
            if (folderInfo == null) return;
            if (index < 0) index = 0;
            if (index > _children.Count) index = _children.Count;
            _children.Insert(index, folderInfo);
            Touch();
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

        public bool ReorderFolder(Ulid folderId, int newIndex) {
            for (var i = 0; i < _children.Count; i++) {
                if (_children[i].ID != folderId) continue;
                if (newIndex < 0) return false;
                if (newIndex > _children.Count) newIndex = _children.Count;
                if (i == newIndex || (i + 1 == newIndex && newIndex == _children.Count)) return false;

                var folder = _children[i];
                _children.RemoveAt(i);
                if (i < newIndex) newIndex--;
                _children.Insert(newIndex, folder);
                Touch();
                return true;
            }

            return false;
        }
    }

    public class BackupFolder : BaseFolder {
        public BackupFolder() {
        }

        public BackupFolder(BackupFolder backupFolder) : base(backupFolder) {
            AvatarId = backupFolder.AvatarId;
        }

        [JsonConstructor]
        public BackupFolder(Ulid id, string name, string description, long modificationTime, List<string> tags,
            string avatarId) : base(id, name, description, modificationTime, tags) {
            AvatarId = avatarId ?? "";
        }

        public string AvatarId { get; private set; } = "";

        public void SetAvatarId(string avatarId) {
            AvatarId = avatarId ?? "";
            Touch();
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
            string itemId, string shopDomain, string shopName) : base(id, name, description, modificationTime, tags) {
            ItemId = itemId ?? "";
            ShopDomain = shopDomain ?? "";
            ShopName = shopName ?? "";
        }

        public string ItemId { get; private set; } = "";
        public string ShopDomain { get; private set; } = "";
        public string ShopName { get; private set; } = "";

        [JsonIgnore] public string ShopUrl => string.IsNullOrEmpty(ShopDomain) ? "" : $"https://{ShopDomain}.booth.pm";

        [JsonIgnore]
        public string ItemUrl => string.IsNullOrEmpty(ShopDomain) || string.IsNullOrEmpty(ItemId)
            ? ""
            : $"https://{ShopDomain}.booth.pm/items/{ItemId}";

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