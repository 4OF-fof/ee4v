using System;
using System.Collections.Concurrent;
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
    public class ShopListDtp {
        public List<ShopDto> shopList;
    }

    public static class BoothLibraryServerState {
        private static readonly ConcurrentQueue<List<ShopDto>> Pending = new();
        private static bool _isProcessing;
        private static readonly object StatusLock = new();

        public static string Status {
            get {
                lock (StatusLock) {
                    if (_isProcessing) return "working";
                    return Pending.IsEmpty ? "waiting" : "working";
                }
            }
        }

        public static void SetContents(List<ShopDto> shops) {
            Pending.Enqueue(shops);
        }

        public static void SetProcessing(bool processing) {
            lock (StatusLock) {
                _isProcessing = processing;
            }
        }
    }
}