using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Data {
    public class LibraryMetadata {
        private readonly List<FolderInfo> _folderInfo = new();
        private readonly List<BoothItem> _boothItem = new();

        public LibraryMetadata() {
        }

        public LibraryMetadata(LibraryMetadata metadata) {
            _folderInfo = metadata.FolderInfo.ToList();
            _boothItem = metadata.BoothItem.ToList();
            ModificationTime = metadata.ModificationTime;
            LibraryVersion = metadata.LibraryVersion;
        }

        [JsonConstructor]
        public LibraryMetadata(List<FolderInfo> folderInfo, List<BoothItem> boothItem, long modificationTime, string libraryVersion) {
            _folderInfo = folderInfo ?? new List<FolderInfo>();
            _boothItem = boothItem ?? new List<BoothItem>();
            ModificationTime = modificationTime;
            LibraryVersion = libraryVersion;
        }

        public IReadOnlyList<FolderInfo> FolderInfo => _folderInfo.AsReadOnly();
        public IReadOnlyList<BoothItem> BoothItem => _boothItem.AsReadOnly();
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
        
        public void AddBoothItem(BoothItem boothItem) {
            if (boothItem == null) return;
            _boothItem.Add(boothItem);
            Touch();
        }
        
        public void RemoveBoothItem(Ulid boothItemId) {
            if (boothItemId == default) return;
            
            for (var i = 0; i < _boothItem.Count; i++) {
                if (_boothItem[i].ID != boothItemId) continue;
                _boothItem.RemoveAt(i);
                Touch();
                return;
            }
        }
        
        public BoothItem GetBoothItem(Ulid boothItemId) {
            return boothItemId == default
                ? null
                : _boothItem.FirstOrDefault(b => b.ID == boothItemId);
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

        public FolderInfo() {
        }

        public FolderInfo(FolderInfo folderInfo) {
            ID = folderInfo.ID;
            Name = folderInfo.Name;
            Description = folderInfo.Description;
            _children = folderInfo.Children.ToList();
            ModificationTime = folderInfo.ModificationTime;
        }

        [JsonConstructor]
        public FolderInfo(Ulid id, string name, string description, List<FolderInfo> children, long modificationTime) {
            ID = id;
            Name = name ?? "";
            Description = description ?? "";
            _children = children ?? new List<FolderInfo>();
            ModificationTime = modificationTime;
        }

        public Ulid ID { get; } = Ulid.Generate();

        public string Name { get; private set; } = "";

        public string Description { get; private set; } = "";

        public IReadOnlyList<FolderInfo> Children => _children.AsReadOnly();
        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

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

        private void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public class BoothItem {
        public BoothItem() {
        }

        public BoothItem(BoothItem boothItem) {
            ID = boothItem.ID;
            Name = boothItem.Name;
            Description = boothItem.Description;
            ShopDomain = boothItem.ShopDomain;
            ItemID = boothItem.ItemID;
            ShopName = boothItem.ShopName;
            ModificationTime = boothItem.ModificationTime;
        }

        [JsonConstructor]
        public BoothItem(Ulid id, string name, string description, long modificationTime,
            string shopDomain, string itemID) {
            ID = id;
            Name = name ?? "";
            Description = description ?? "";
            ShopDomain = shopDomain ?? "";
            ItemID = itemID ?? "";
            ModificationTime = modificationTime;
        }

        public Ulid ID { get; } = Ulid.Generate();
        public string Name { get; private set; } = "";
        public string Description { get; private set; } = "";
        public string ShopDomain { get; private set; } = "";
        public string ItemID { get; private set; } = "";
        public string ShopName { get; private set; } = "";
        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        [JsonIgnore] public string ShopURL => string.IsNullOrEmpty(ShopDomain) ? "" : $"https://{ShopDomain}.booth.pm";

        [JsonIgnore]
        public string ItemURL => string.IsNullOrEmpty(ShopDomain) || string.IsNullOrEmpty(ItemID)
            ? ""
            : $"https://{ShopDomain}.booth.pm/items/{ItemID}";

        public void SetName(string newName) {
            Name = newName;
            Touch();
        }

        public void SetDescription(string newDescription) {
            Description = newDescription;
            Touch();
        }

        public void SetShopDomain(string shopDomain) {
            ShopDomain = shopDomain;
            Touch();
        }

        public void SetItemID(string itemID) {
            ItemID = itemID;
            Touch();
        }

        public void SetShopName(string shopName) {
            ShopName = shopName;
            Touch();
        }

        private void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}