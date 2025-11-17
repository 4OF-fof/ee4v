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
            Name = metadata.Name;
            Description = metadata.Description;
            Size = metadata.Size;
            Ext = metadata.Ext;
            BoothData = metadata.BoothData;
            Folder = metadata.Folder;
            _tags = new List<string>(metadata.Tags);
            IsDeleted = metadata.IsDeleted;
            ModificationTime = metadata.ModificationTime;
        }

        [JsonConstructor]
        public AssetMetadata(Ulid id, string name, string description, long size, string ext, BoothMetadata boothData,
            Ulid folder, List<string> tags, bool isDeleted, long modificationTime) {
            ID = id;
            Name = name;
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

        public string Name { get; private set; } = "";

        public string Description { get; private set; } = "";

        public long Size { get; private set; }

        public string Ext { get; private set; } = "";

        public BoothMetadata BoothData { get; set; }

        public Ulid Folder { get; private set; } = Ulid.Empty;
        public IReadOnlyList<string> Tags => _tags.AsReadOnly();
        public bool IsDeleted { get; private set; }

        public long ModificationTime { get; private set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public void SetName(string newName) {
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
            ShopName = data?.ShopName ?? "";
            ItemID = data?.ItemID ?? "";
            DownloadID = data?.DownloadID ?? "";
        }

        [JsonConstructor]
        public BoothMetadata(string shopName, string itemID, string downloadID) {
            ShopName = shopName;
            ItemID = itemID;
            DownloadID = downloadID;
        }

        public string ShopName { get; private set; } = "";
        public string ItemID { get; private set; } = "";
        public string DownloadID { get; private set; } = "";

        [JsonIgnore] public string ShopURL => string.IsNullOrEmpty(ShopName) ? "" : $"https://{ShopName}.booth.pm";

        [JsonIgnore]
        public string ItemURL => string.IsNullOrEmpty(ShopName) || string.IsNullOrEmpty(ItemID)
            ? ""
            : $"https://{ShopName}.booth.pm/items/{ItemID}";

        [JsonIgnore]
        public string DownloadURL =>
            string.IsNullOrEmpty(DownloadID) ? "" : $"https://booth.pm/downloadables/{DownloadID}";

        public void SetShopName(string newShopName) {
            ShopName = newShopName;
        }

        public void SetItemID(string newItemID) {
            ItemID = newItemID;
        }

        public void SetDownloadID(string newDownloadID) {
            DownloadID = newDownloadID;
        }
    }
}