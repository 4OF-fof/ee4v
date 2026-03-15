using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Core {
    public class AssetMetadata {
        private readonly List<string> _tags = new();

        public AssetMetadata() {
        }

        public AssetMetadata(AssetMetadata metadata) {
            ID = metadata.ID;
            Name = string.IsNullOrWhiteSpace(metadata.Name)
                ? I18N.Get("UI.AssetManager.Default.Untitled")
                : metadata.Name;
            Description = metadata.Description;
            Size = metadata.Size;
            Ext = metadata.Ext;
            BoothData = new BoothMetadata(metadata.BoothData);
            UnityData = new UnityMetadata(metadata.UnityData);
            Folder = metadata.Folder;
            _tags = new List<string>(metadata.Tags);
            IsDeleted = metadata.IsDeleted;
            ModificationTime = metadata.ModificationTime;
        }

        [JsonConstructor]
        public AssetMetadata(Ulid id, string name, string description, long size, string ext, BoothMetadata boothData,
            UnityMetadata unityData,
            Ulid folder, List<string> tags, bool isDeleted, long modificationTime) {
            ID = id;
            Name = string.IsNullOrWhiteSpace(name) ? I18N.Get("UI.AssetManager.Default.Untitled") : name;
            Description = description;
            Size = size;
            Ext = ext;
            BoothData = boothData;
            UnityData = unityData;
            Folder = folder;
            _tags = tags ?? new List<string>();
            IsDeleted = isDeleted;
            ModificationTime = modificationTime;
        }

        public Ulid ID { get; } = Ulid.Generate();
        public string Name { get; private set; } = I18N.Get("UI.AssetManager.Default.Untitled");
        public string Description { get; private set; } = "";
        public long Size { get; private set; }
        public string Ext { get; private set; } = "";
        public BoothMetadata BoothData { get; private set; } = new();
        public UnityMetadata UnityData { get; private set; } = new();
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
            Ext = newExt?.ToLowerInvariant();
            Touch();
        }

        public void SetBoothData(BoothMetadata newBoothData) {
            BoothData = newBoothData;
            Touch();
        }

        public void SetUnityData(UnityMetadata newUnityData) {
            UnityData = newUnityData;
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
            ItemId = data?.ItemId ?? "";
            DownloadId = data?.DownloadId ?? "";
            FileName = data?.FileName ?? "";
        }

        [JsonConstructor]
        public BoothMetadata(string shopDomain, string itemID, string downloadID, string fileName) {
            ShopDomain = shopDomain;
            ItemId = itemID;
            DownloadId = downloadID;
            FileName = fileName;
        }

        public string ShopDomain { get; private set; } = "";
        public string ItemId { get; private set; } = "";
        public string DownloadId { get; private set; } = "";
        public string FileName { get; private set; } = "";

        [JsonIgnore] public string ShopUrl => string.IsNullOrEmpty(ShopDomain) ? "" : $"https://{ShopDomain}.booth.pm";

        [JsonIgnore]
        public string ItemUrl => string.IsNullOrEmpty(ShopDomain) || string.IsNullOrEmpty(ItemId)
            ? ""
            : $"https://{ShopDomain}.booth.pm/items/{ItemId}";

        [JsonIgnore]
        public string DownloadUrl =>
            string.IsNullOrEmpty(DownloadId) ? "" : $"https://booth.pm/downloadables/{DownloadId}";

        public void SetShopDomain(string newShopName) {
            ShopDomain = newShopName;
        }

        public void SetItemID(string newItemID) {
            ItemId = newItemID;
        }

        public void SetDownloadID(string newDownloadID) {
            DownloadId = newDownloadID;
        }

        public void SetFileName(string newFileName) {
            FileName = newFileName;
        }
    }

    public class UnityMetadata {
        public UnityMetadata() {
        }

        public UnityMetadata(UnityMetadata metadata) {
            AssetGuidList = new List<Guid>(metadata.AssetGuidList);
            DependenceItemList = new List<Ulid>(metadata.DependenceItemList);
        }

        [JsonConstructor]
        public UnityMetadata(List<Guid> assetGuidList, List<Ulid> dependenceItemList) {
            AssetGuidList = assetGuidList ?? new List<Guid>();
            DependenceItemList = dependenceItemList ?? new List<Ulid>();
        }

        public List<Guid> AssetGuidList { get; } = new();
        public List<Ulid> DependenceItemList { get; } = new();

        public void AddAssetGuid(Guid guid) {
            if (!AssetGuidList.Contains(guid)) AssetGuidList.Add(guid);
        }

        public void RemoveAssetGuid(Guid guid) {
            AssetGuidList.Remove(guid);
        }

        public void AddDependenceItem(Ulid itemId) {
            if (!DependenceItemList.Contains(itemId)) DependenceItemList.Add(itemId);
        }

        public void RemoveDependenceItem(Ulid itemId) {
            DependenceItemList.Remove(itemId);
        }
    }
}