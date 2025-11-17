using System;
using System.Collections.Generic;

namespace _4OF.ee4v.AssetManager.Adapter {
    [Serializable]
    public class ShopDto {
        public string shopURL;
        public string shopName;
        public List<ItemDto> items;
    }

    [Serializable]
    public class ItemDto {
        public string itemURL;
        public string name;
        public string description;
        public string imageURL;
        public List<FileDto> files;
    }

    [Serializable]
    public class FileDto {
        public string url;
        public string filename;
    }

    [Serializable]
    public class ShopsWrapper {
        public List<ShopDto> Shops;
    }
}
