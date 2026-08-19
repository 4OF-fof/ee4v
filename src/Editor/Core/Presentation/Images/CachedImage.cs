using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Images
{
    public sealed class CachedImageCache : IDisposable
    {
        private readonly Dictionary<string, byte[]> _sources =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<TextureKey, Texture2D> _textures =
            new Dictionary<TextureKey, Texture2D>();
        private bool _disposed;

        public bool HasSource(string key)
        {
            return !string.IsNullOrEmpty(key) &&
                _sources.ContainsKey(key);
        }

        public void SetSource(string key, byte[] encodedImage)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(
                    "Image cache key is required.",
                    nameof(key));
            }

            ThrowIfDisposed();
            _sources[key] = encodedImage ?? Array.Empty<byte>();
        }

        public void Clear()
        {
            foreach (var texture in _textures.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            _textures.Clear();
            _sources.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Clear();
            _disposed = true;
        }

        internal bool TryGetSource(string key, out byte[] encodedImage)
        {
            ThrowIfDisposed();
            return _sources.TryGetValue(
                key ?? string.Empty,
                out encodedImage);
        }

        internal Texture2D GetTexture(string key, byte[] encodedImage)
        {
            ThrowIfDisposed();
            if (encodedImage == null || encodedImage.Length == 0)
            {
                return null;
            }

            var textureKey = new TextureKey(key, encodedImage);
            if (_textures.TryGetValue(textureKey, out var cached))
            {
                return cached;
            }

            var texture = new Texture2D(2, 2)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!texture.LoadImage(encodedImage))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
            }

            _textures[textureKey] = texture;
            return texture;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(CachedImageCache));
            }
        }

        private readonly struct TextureKey : IEquatable<TextureKey>
        {
            public TextureKey(string key, byte[] data)
            {
                Key = key ?? string.Empty;
                Data = data;
            }

            private string Key { get; }
            private byte[] Data { get; }

            public bool Equals(TextureKey other)
            {
                return string.Equals(
                        Key,
                        other.Key,
                        StringComparison.Ordinal) &&
                    ReferenceEquals(Data, other.Data);
            }

            public override bool Equals(object obj)
            {
                return obj is TextureKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(Key) * 397) ^
                        RuntimeHelpers.GetHashCode(Data);
                }
            }
        }
    }

    public sealed class CachedImage : Image, IDisposable
    {
        private readonly CachedImageCache _cache;
        private string _sourceKey = string.Empty;
        private byte[] _sourceData;
        private bool _hasSource;

        public CachedImage(CachedImageCache cache)
        {
            _cache = cache ??
                throw new ArgumentNullException(nameof(cache));
            pickingMode = PickingMode.Ignore;
            scaleMode = ScaleMode.ScaleToFit;
        }

        public Texture DisplayedTexture => image;

        public void SetSource(string key)
        {
            var hasSource = _cache.TryGetSource(key, out var sourceData);
            if (_hasSource == hasSource &&
                string.Equals(
                    _sourceKey,
                    key,
                    StringComparison.Ordinal) &&
                ReferenceEquals(_sourceData, sourceData))
            {
                return;
            }

            _sourceKey = key ?? string.Empty;
            _sourceData = sourceData;
            _hasSource = hasSource;
            image = hasSource
                ? _cache.GetTexture(_sourceKey, sourceData)
                : null;
        }

        public void ClearSource()
        {
            _sourceKey = string.Empty;
            _sourceData = null;
            _hasSource = false;
            image = null;
        }

        public void Dispose()
        {
            ClearSource();
        }
    }
}
