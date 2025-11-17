using System.Collections.Concurrent;
using System.Collections.Generic;

namespace _4OF.ee4v.AssetManager.Adapter {
    public static class BoothLibraryServerState {
        private static readonly ConcurrentQueue<List<ShopDto>> Pending = new();
        private static List<ShopDto> _lastContents = new List<ShopDto>();
        public static IReadOnlyList<ShopDto> LastContents => _lastContents;

        public static void SetContents(List<ShopDto> shops) {
            // enqueue and let UI flush in EditorApplication.update
            Pending.Enqueue(shops);
        }

        public static bool TryTakePending(out List<ShopDto> shops) {
            if (Pending.TryDequeue(out shops)) {
                _lastContents = shops ?? new List<ShopDto>();
                return true;
            }
            shops = null;
            return false;
        }

        public static void Clear() {
            _lastContents = new List<ShopDto>();
        }
    }
}
