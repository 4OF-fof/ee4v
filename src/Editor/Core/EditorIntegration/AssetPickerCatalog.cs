using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ee4v.Core.EditorIntegration
{
    public sealed class AssetPickerOrigin
    {
        private readonly Func<CancellationToken, Task<byte[]>> _thumbnail;
        private byte[] _thumbnailData;
        private bool _thumbnailLoaded;

        public AssetPickerOrigin(string id, string name,
            Func<CancellationToken, Task<byte[]>> thumbnail = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            _thumbnail = thumbnail;
        }

        public string Id { get; }
        public string Name { get; }

        public async Task<byte[]> GetThumbnailAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_thumbnailLoaded || _thumbnail == null) return _thumbnailData;
            var data = await _thumbnail(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _thumbnailData = data;
            _thumbnailLoaded = true;
            return data;
        }
    }

    public enum AssetPickerMatch
    {
        SameUvUsage, SameMainTexture, ReferencedTexture, SameFolder, SameOrigin, Other
    }

    public enum AssetPickerClipKind
    {
        None, Facial, Other
    }

    public sealed class AssetPickerCandidate
    {
        private Object _asset;
        private bool _resolved;

        internal AssetPickerCandidate(Object asset, string guid, long localId,
            string path, AssetPickerOrigin origin)
            : this(guid, localId, path, origin, asset.name,
                asset is Material material ? AssetPickerCache.GetAssetKey(material.mainTexture) : string.Empty,
                AssetPickerCatalog.GetTextureKeys(asset as Material),
                asset is AnimationClip clip ? AssetPickerCatalog.ClassifyClip(clip) : AssetPickerClipKind.None)
        {
            _asset = asset;
            _resolved = true;
            AssetPickerCache.GetAssetKey(asset);
        }

        internal AssetPickerCandidate(string guid, long localId, string path, AssetPickerOrigin origin,
            string name, string mainTextureKey, string[] textureKeys, AssetPickerClipKind clipKind)
        {
            Guid = guid;
            LocalId = localId;
            Key = AssetPickerCache.CreateAssetKey(guid, localId);
            Path = path;
            Folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
            Origin = origin;
            Name = name ?? string.Empty;
            MainTextureKey = mainTextureKey ?? string.Empty;
            TextureKeys = textureKeys ?? Array.Empty<string>();
            ClipKind = clipKind;
        }

        public Object Asset
        {
            get
            {
                if (!_resolved)
                {
                    _asset = AssetPickerCache.ResolveAsset(Guid, LocalId, Path);
                    _resolved = true;
                }
                return _asset;
            }
        }
        public string Name { get; }
        public string Key { get; }
        internal string MainTextureKey { get; }
        internal string[] TextureKeys { get; }
        public AssetPickerClipKind ClipKind { get; }
        public string Guid { get; }
        public long LocalId { get; }
        public string Path { get; }
        public string Folder { get; }
        public AssetPickerOrigin Origin { get; }
        public AssetPickerMatch Match { get; internal set; } = AssetPickerMatch.Other;

        public bool IsAsset(Object asset) => asset != null && Key == AssetPickerCache.GetAssetKey(asset);

        internal AssetPickerCandidate WithOrigin(AssetPickerOrigin origin) =>
            new AssetPickerCandidate(Guid, LocalId, Path, origin, Name, MainTextureKey, TextureKeys, ClipKind)
            { _asset = this._asset, _resolved = this._resolved };
    }

    /// <summary>Project assets and optional GUID ownership, independent of feature assemblies.</summary>
    public static class AssetPickerCatalog
    {
        private static Func<IReadOnlyDictionary<string, AssetPickerOrigin>> _origins;
        private static IReadOnlyDictionary<string, AssetPickerOrigin> _originSnapshot;

        public static IDisposable RegisterOrigins(
            Func<IReadOnlyDictionary<string, AssetPickerOrigin>> provider)
        {
            _origins = provider ?? throw new ArgumentNullException(nameof(provider));
            InvalidateOrigins();
            return new OriginLease(provider);
        }

        public static IReadOnlyDictionary<string, AssetPickerOrigin> GetOrigins()
        {
            return _originSnapshot ?? (_originSnapshot =
                _origins?.Invoke() ?? new Dictionary<string, AssetPickerOrigin>());
        }

        public static void InvalidateOrigins() => _originSnapshot = null;

        // Each yield bounds asset loading to one file; callers can enumerate on Editor ticks.
        public static IEnumerable<IReadOnlyList<AssetPickerCandidate>> Find(Type type,
            IReadOnlyDictionary<string, AssetPickerOrigin> origins)
        {
            if (type == null || !typeof(Object).IsAssignableFrom(type))
                throw new ArgumentException("A Unity Object type is required.", nameof(type));

            var version = AssetPickerCache.Version;
            foreach (var guid in AssetDatabase.FindAssets("t:" + type.Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                origins.TryGetValue(guid, out var origin);
                if (AssetPickerCache.TryGetCandidateFile(type, guid, path, out var cached))
                {
                    yield return cached.Select(entry => new AssetPickerCandidate(guid, entry.LocalId, path, origin,
                        entry.Name, entry.MainTextureKey, entry.TextureKeys, entry.ClipKind)).ToArray();
                    continue;
                }
                var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                var candidates = new List<AssetPickerCandidate>();
                foreach (var asset in objects)
                {
                    if (asset == null || !type.IsInstanceOfType(asset))
                        continue;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,
                            out string objectGuid, out long localId))
                        candidates.Add(new AssetPickerCandidate(asset, objectGuid, localId, path, origin));
                }
                if (!candidates.Any(candidate => EditorUtility.IsDirty(candidate.Asset) ||
                        candidate.MainTextureKey.StartsWith("instance:", StringComparison.Ordinal) ||
                        candidate.TextureKeys.Any(key => key.StartsWith("instance:", StringComparison.Ordinal))))
                    AssetPickerCache.StoreCandidateFile(type, guid, path, candidates.Select(candidate => new AssetPickerCache.CandidateData
                    {
                        LocalId = candidate.LocalId, Name = candidate.Name, MainTextureKey = candidate.MainTextureKey,
                        TextureKeys = candidate.TextureKeys, ClipKind = candidate.ClipKind
                    }).ToArray(), version);
                yield return candidates;
            }
        }

        public static void UpdateMatches(IEnumerable<AssetPickerCandidate> candidates, Object current,
            IReadOnlyDictionary<string, AssetPickerOrigin> origins, MaterialUvUsageIndex uvUsage,
            IEnumerable<AssetPickerCandidate> textureMaterials = null)
        {
            var path = AssetDatabase.GetAssetPath(current);
            var folder = string.IsNullOrEmpty(path) ? string.Empty : Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            origins.TryGetValue(guid, out var origin);
            var reference = current as Material;
            var textureKey = reference == null ? string.Empty : AssetPickerCache.GetAssetKey(reference.mainTexture);
            var referenceKey = AssetPickerCache.GetAssetKey(reference);
            var textureMode = textureMaterials != null;
            var referencedTextures = new HashSet<string>(textureMode ? GetTextureKeys(reference) : Array.Empty<string>(), StringComparer.Ordinal);
            var textureFolders = new HashSet<string>(StringComparer.Ordinal);
            var matchingTextures = new HashSet<string>(StringComparer.Ordinal);
            if (textureMode && reference != null)
            {
                foreach (var key in referencedTextures)
                {
                    var separator = key.IndexOf(':');
                    if (separator < 0 || key.StartsWith("instance:", StringComparison.Ordinal)) continue;
                    var texturePath = AssetDatabase.GUIDToAssetPath(key.Substring(0, separator));
                    if (!string.IsNullOrEmpty(texturePath))
                        textureFolders.Add(Path.GetDirectoryName(texturePath)?.Replace('\\', '/') ?? string.Empty);
                }
                if (uvUsage != null)
                    foreach (var material in textureMaterials)
                        if (uvUsage.HasSameUsage(referenceKey, material.Key)) matchingTextures.UnionWith(material.TextureKeys);
            }
            foreach (var candidate in candidates)
            {
                candidate.Match = current == null ? AssetPickerMatch.Other :
                    reference != null && (textureMode ? matchingTextures.Contains(candidate.Key) :
                        uvUsage != null && uvUsage.HasSameUsage(referenceKey, candidate.Key)) ? AssetPickerMatch.SameUvUsage :
                    textureKey.Length > 0 && textureKey == (textureMode ? candidate.Key : candidate.MainTextureKey) ? AssetPickerMatch.SameMainTexture :
                    textureMode && referencedTextures.Contains(candidate.Key) ? AssetPickerMatch.ReferencedTexture :
                    folder.Length > 0 && (folder == candidate.Folder || textureMode && textureFolders.Contains(candidate.Folder)) ? AssetPickerMatch.SameFolder :
                    origin != null && candidate.Origin?.Id == origin.Id ? AssetPickerMatch.SameOrigin : AssetPickerMatch.Other;
            }
        }

        internal static string[] GetTextureKeys(Material material)
        {
            if (material == null || material.shader == null) return Array.Empty<string>();
            return material.GetTexturePropertyNames().Select(name => AssetPickerCache.GetAssetKey(material.GetTexture(name)))
                .Append(AssetPickerCache.GetAssetKey(material.mainTexture))
                .Where(key => key.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        }

        public static AssetPickerClipKind ClassifyClip(AnimationClip clip)
        {
            if (clip == null) return AssetPickerClipKind.None;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            return bindings.Length > 0 && bindings.All(binding =>
                binding.type == typeof(SkinnedMeshRenderer) &&
                binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal) &&
                binding.propertyName.Length > "blendShape.".Length &&
                (binding.path == "Body" || binding.path.EndsWith("/Body", StringComparison.Ordinal))) &&
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Length == 0 &&
                AnimationUtility.GetAnimationEvents(clip).Length == 0
                ? AssetPickerClipKind.Facial : AssetPickerClipKind.Other;
        }

        private sealed class OriginLease : IDisposable
        {
            private readonly Func<IReadOnlyDictionary<string, AssetPickerOrigin>> _provider;
            public OriginLease(Func<IReadOnlyDictionary<string, AssetPickerOrigin>> provider) => _provider = provider;
            public void Dispose()
            {
                if (_origins == _provider)
                {
                    _origins = null;
                    InvalidateOrigins();
                }
            }
        }
    }
}
