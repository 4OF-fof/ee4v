using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    [Serializable]
    public class LibraryMetadata {
        public List<FolderInfo> folderInfo = new();
        public long modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public string libraryVersion = "1.0.0";
    }
    [Serializable]
    public class FolderInfo {
        public string id = Ulid.Generate();
        public string name = "";
        public string description = "";
        public List<string> children = new();
        public long modificationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public List<string> tags = new();
    }
}