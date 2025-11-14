using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    [Serializable]
    public class AssetMetadata {
        public string id = Ulid.Generate();
        public string name = "";
        public string description = "";
        public long size;
        public string ext = "";
        public string folder = "";
        public List<string> tags = new();
        public bool isDeleted;
        public long modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}