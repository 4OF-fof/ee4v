using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetMetadata {
        private readonly List<string> _tags = new();

        public AssetMetadata() {
        }

        public AssetMetadata(AssetMetadata metadata) {
            ID = metadata.ID;
            Name = string.IsNullOrWhiteSpace(metadata.Name) ? "Untitled" : metadata.Name;
            Description = metadata.Description;
            Size = metadata.Size;
            Ext = metadata.Ext;
            BoothData = new BoothMetadata(metadata.BoothData);
            Folder = metadata.Folder;
            _tags = new List<string>(metadata.Tags);
            IsDeleted = metadata.IsDeleted;
            ModificationTime = metadata.ModificationTime;
        }

        [JsonConstructor]
        public AssetMetadata(Ulid id, string name, string description, long size, string ext, BoothMetadata boothData,
            Ulid folder, List<string> tags, bool isDeleted, long modificationTime) {
            ID = id;
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
            Description = description;
            Size = size;
            Ext = ext;
            BoothData = boothData;
            Folder = folder;
            _tags = tags ?? new List<string>();
            IsDeleted = isDeleted;
            ModificationTime = modificationTime;
        }

        public Ulid ID { get; } = Ulid.Generate();
        public string Name { get; private set; } = "Untitled";
        public string Description { get; private set; } = "";
        public long Size { get; private set; }
        public string Ext { get; private set; } = "";
        public BoothMetadata BoothData { get; private set; } = new();
        public Ulid Folder { get; private set; } = Ulid.Empty;
        public IReadOnlyList<string> Tags => _tags.AsReadOnly();
        public bool IsDeleted { get; private set; }
        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public void SetName(string newName) {
            if (string.IsNullOrWhiteSpace(newName)) return;
            Name = newName;
            Touch();
        }

        public void SetDescription(string newDescription) {
            Description = newDescription;
            Touch();
        }

        public void SetSize(long newSize) {
            Size = newSize;
            Touch();
        }

        public void SetExt(string newExt) {
            Ext = newExt;
            Touch();
        }

        public void SetBoothData(BoothMetadata newBoothData) {
            BoothData = newBoothData;
            Touch();
        }

        public void SetFolder(Ulid newFolder) {
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

        private void Touch() {
            ModificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public class BoothMetadata {
        public BoothMetadata() {
        }

        public BoothMetadata(BoothMetadata data) {
            ShopDomain = data?.ShopDomain ?? "";
            ItemID = data?.ItemID ?? "";
            DownloadID = data?.DownloadID ?? "";
            FileName = data?.FileName ?? "";
        }

        [JsonConstructor]
        public BoothMetadata(string shopDomain, string itemID, string downloadID, string fileName) {
            ShopDomain = shopDomain;
            ItemID = itemID;
            DownloadID = downloadID;
            FileName = fileName;
        }

        public string ShopDomain { get; private set; } = "";
        public string ItemID { get; private set; } = "";
        public string DownloadID { get; private set; } = "";
        public string FileName { get; private set; } = "";

        [JsonIgnore] public string ShopURL => string.IsNullOrEmpty(ShopDomain) ? "" : $"https://{ShopDomain}.booth.pm";

        [JsonIgnore]
        public string ItemURL => string.IsNullOrEmpty(ShopDomain) || string.IsNullOrEmpty(ItemID)
            ? ""
            : $"https://{ShopDomain}.booth.pm/items/{ItemID}";

        [JsonIgnore]
        public string DownloadURL =>
            string.IsNullOrEmpty(DownloadID) ? "" : $"https://booth.pm/downloadables/{DownloadID}";

        public void SetShopDomain(string newShopName) {
            ShopDomain = newShopName;
        }

        public void SetItemID(string newItemID) {
            ItemID = newItemID;
        }

        public void SetDownloadID(string newDownloadID) {
            DownloadID = newDownloadID;
        }

        public void SetFileName(string newFileName) {
            FileName = newFileName;
        }
    }
}