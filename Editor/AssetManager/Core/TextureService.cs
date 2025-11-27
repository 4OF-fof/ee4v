using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.AssetManager.Core {
    public class TextureService {
        private const int MaxCacheSize = 200;

        private readonly Dictionary<string, Texture2D> _cache = new();
        private readonly List<string> _lruKeys = new();
        private readonly IAssetRepository _repository;

        public TextureService(IAssetRepository repository) {
            _repository = repository;
        }

        public async Task<Texture2D> GetAssetThumbnailAsync(Ulid assetId) {
            var key = $"asset_{assetId}";
            if (TryGetFromCache(key, out var texture)) return texture;

            var data = await _repository.GetThumbnailDataAsync(assetId);
            return CreateAndCacheTexture(key, data);
        }

        public async Task<Texture2D> GetFolderThumbnailAsync(Ulid folderId) {
            var key = $"folder_{folderId}";
            if (TryGetFromCache(key, out var texture)) return texture;

            var data = await _repository.GetFolderThumbnailDataAsync(folderId);
            return CreateAndCacheTexture(key, data);
        }

        public static Texture2D GetDefaultFallback(bool isFolder, bool isEmpty = false) {
            if (!isFolder) {
                const string key = "GameObject Icon";
                return EditorGUIUtility.IconContent(key).image as Texture2D;
            }

            var folderKey = isEmpty ? "FolderEmpty Icon" : "Folder Icon";
            return EditorGUIUtility.IconContent(folderKey).image as Texture2D;
        }

        public void ClearCache() {
            foreach (var tex in _cache.Values.Where(tex => tex != null)) Object.DestroyImmediate(tex);
            _cache.Clear();
            _lruKeys.Clear();
        }

        public void RemoveAssetFromCache(Ulid assetId) {
            var key = $"asset_{assetId}";
            if (!_cache.TryGetValue(key, out var tex)) return;
            if (tex != null) Object.DestroyImmediate(tex);
            _cache.Remove(key);
            _lruKeys.Remove(key);
        }

        public void RemoveFolderFromCache(Ulid folderId) {
            var key = $"folder_{folderId}";
            if (!_cache.TryGetValue(key, out var tex)) return;
            if (tex != null) Object.DestroyImmediate(tex);
            _cache.Remove(key);
            _lruKeys.Remove(key);
        }

        private bool TryGetFromCache(string key, out Texture2D texture) {
            if (!_cache.TryGetValue(key, out texture)) return false;
            _lruKeys.Remove(key);
            _lruKeys.Add(key);
            return true;
        }

        private Texture2D CreateAndCacheTexture(string key, byte[] data) {
            if (data == null || data.Length == 0) return null;

            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(data)) {
                AddToCache(key, tex);
                return tex;
            }

            Object.DestroyImmediate(tex);
            return null;
        }

        private void AddToCache(string key, Texture2D texture) {
            if (_cache.ContainsKey(key)) {
                _lruKeys.Remove(key);
                _lruKeys.Add(key);
                _cache[key] = texture;
                return;
            }

            if (_cache.Count >= MaxCacheSize) {
                var oldestKey = _lruKeys[0];
                _lruKeys.RemoveAt(0);
                if (_cache.TryGetValue(oldestKey, out var oldTex)) {
                    if (oldTex != null) Object.DestroyImmediate(oldTex);
                    _cache.Remove(oldestKey);
                }
            }

            _cache[key] = texture;
            _lruKeys.Add(key);
        }
    }
}