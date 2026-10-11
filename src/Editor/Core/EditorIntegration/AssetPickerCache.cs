using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ee4v.Core.EditorIntegration
{
    [InitializeOnLoad]
    public static class AssetPickerCache
    {
        private static readonly string CachePath = Path.Combine(Path.GetDirectoryName(Application.dataPath),
            "Library", "ee4v", "asset-picker-v1.json");
        private static readonly Dictionary<Type, AssetPickerCandidate[]> Candidates = new Dictionary<Type, AssetPickerCandidate[]>();
        private static readonly Dictionary<string, CandidateFile> CandidateFiles = new Dictionary<string, CandidateFile>();
        private static readonly Dictionary<string, UsageFile> UsageFiles = new Dictionary<string, UsageFile>();
        private static readonly Dictionary<string, MeshFile> MeshFiles = new Dictionary<string, MeshFile>();
        private static readonly HashSet<string> CandidateTypes = new HashSet<string>();
        private static readonly Dictionary<string, HashSet<string>> UsageDependencies = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, HashSet<string>> MeshKeys = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, string> DependencyHashes = new Dictionary<string, string>();
        private static readonly Dictionary<Object, string> ObjectKeys = new Dictionary<Object, string>();
        private static readonly Dictionary<string, Object> Objects = new Dictionary<string, Object>();
        private static readonly HashSet<string> EditedAssets = new HashSet<string>();
        private static readonly Dictionary<string, HashSet<Object>> EditedObjects = new Dictionary<string, HashSet<Object>>();
        private static MaterialUvUsageIndex _uvUsage;
        private static bool _loaded, _dirty, _reportedIoFailure;
        private static double _saveAfter;

        [Serializable]
        internal sealed class CandidateData
        {
            public long LocalId;
            public string Name;
            public string MainTextureKey;
            public string[] TextureKeys;
            public AssetPickerClipKind ClipKind;
        }

        [Serializable]
        private sealed class CandidateFile
        {
            public string Guid, Type, Path, Hash;
            public CandidateData[] Entries;
        }

        [Serializable]
        private sealed class UsageFile
        {
            public string Guid, Path, Hash;
            public string[] Dependencies;
            public MaterialUvUsageIndex.UsageData[] Entries;
        }

        [Serializable]
        private sealed class MeshFile
        {
            public string Key, Hash;
            public LayoutData[] Slots;
        }

        [Serializable]
        private sealed class LayoutData
        {
            public int Slot;
            public string Signature;
        }

        [Serializable]
        private sealed class DiskIndex
        {
            public int Version = 1;
            public string UnityVersion;
            public CandidateFile[] Candidates;
            public UsageFile[] Usage;
            public MeshFile[] Meshes;
        }

        static AssetPickerCache()
        {
            EditorApplication.projectChanged += Invalidate;
            EditorApplication.playModeStateChanged += _ => Invalidate();
            Undo.undoRedoPerformed += () =>
            {
                foreach (var guid in EditedAssets.ToArray()) EvictAsset(guid);
                if (EditedAssets.Count > 0) Invalidate();
            };
            Undo.postprocessModifications += modifications =>
            {
                var changed = new HashSet<string>();
                foreach (var change in modifications)
                {
                    var target = change.currentValue?.target;
                    if (target == null || !EditorUtility.IsPersistent(target)) continue;
                    var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
                    if (string.IsNullOrEmpty(guid)) continue;
                    EditedAssets.Add(guid);
                    if (!EditedObjects.TryGetValue(guid, out var targets))
                        EditedObjects[guid] = targets = new HashSet<Object>();
                    targets.Add(target);
                    changed.Add(guid);
                }
                foreach (var guid in changed) EvictAsset(guid);
                if (changed.Count > 0) Invalidate();
                return modifications;
            };
            EditorApplication.update += () =>
            {
                if (_dirty && EditorApplication.timeSinceStartup >= _saveAfter) Save();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Save;
            EditorApplication.quitting += Save;
        }

        public static int Version { get; private set; }

        public static void Invalidate()
        {
            Version++;
            Candidates.Clear();
            DependencyHashes.Clear();
            ObjectKeys.Clear();
            Objects.Clear();
            _uvUsage = null;
        }

        public static string GetAssetKey(Object asset)
        {
            if (asset == null) return string.Empty;
            if (ObjectKeys.TryGetValue(asset, out var key)) return key;
            key = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)
                && !string.IsNullOrEmpty(guid) ? CreateAssetKey(guid, localId) : "instance:" + asset.GetInstanceID();
            ObjectKeys[asset] = key;
            Objects[key] = asset;
            return key;
        }

        internal static Object ResolveAsset(string guid, long localId, string path)
        {
            var key = CreateAssetKey(guid, localId);
            if (Objects.TryGetValue(key, out var cached) && cached != null) return cached;
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            if (GetAssetKey(main) == key) return main;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) GetAssetKey(asset);
            if (Objects.TryGetValue(key, out cached) && cached != null) return cached;
            EvictAsset(guid);
            Invalidate();
            return null;
        }

        internal static string CreateAssetKey(string guid, long localId) => guid + ":" + localId.ToString(CultureInfo.InvariantCulture);

        public static bool TryGetCandidates(Type type, IReadOnlyDictionary<string, AssetPickerOrigin> origins,
            out IReadOnlyList<AssetPickerCandidate> candidates)
        {
            candidates = null;
            if (!Candidates.TryGetValue(type, out var cached)) return false;
            candidates = cached.Select(entry => entry.WithOrigin(origins.TryGetValue(entry.Guid, out var origin) ? origin : null)).ToArray();
            return true;
        }

        public static void StoreCandidates(Type type, IEnumerable<AssetPickerCandidate> candidates, int version)
        {
            if (version != Version) return;
            Candidates[type] = candidates.ToArray();
            Save();
        }

        public static bool TryGetUvUsage(out MaterialUvUsageIndex usage)
        {
            usage = _uvUsage;
            return usage != null;
        }

        public static void StoreUvUsage(MaterialUvUsageIndex usage, int version)
        {
            if (version != Version) return;
            _uvUsage = usage;
            Save();
        }

        internal static bool TryGetCandidateFile(Type type, string guid, string path, out CandidateData[] entries)
        {
            EnsureLoaded();
            entries = null;
            if (!CandidateFiles.TryGetValue(type.FullName + "|" + guid, out var file) ||
                file.Path != path || file.Hash != DependencyHash(path)) return false;
            entries = file.Entries;
            return entries != null && entries.All(entry => entry != null &&
                (!typeof(Material).IsAssignableFrom(type) || entry.TextureKeys != null) &&
                (!typeof(AnimationClip).IsAssignableFrom(type) || entry.ClipKind != AssetPickerClipKind.None));
        }

        internal static void StoreCandidateFile(Type type, string guid, string path, CandidateData[] entries, int version)
        {
            if (version != Version || EditorApplication.isPlaying || HasUnsavedChanges(guid)) return;
            EnsureLoaded();
            CandidateFiles[type.FullName + "|" + guid] = new CandidateFile
            {
                Guid = guid, Type = type.FullName, Path = path, Hash = DependencyHash(path), Entries = entries
            };
            CandidateTypes.Add(type.FullName);
            MarkDirty();
        }

        internal static bool TryGetUsageFile(string guid, string path, out MaterialUvUsageIndex.UsageData[] entries)
        {
            EnsureLoaded();
            entries = null;
            if (!UsageFiles.TryGetValue(guid, out var file) || file.Path != path || file.Hash != DependencyHash(path)) return false;
            entries = file.Entries;
            return entries != null;
        }

        internal static void StoreUsageFile(string guid, string path, MaterialUvUsageIndex.UsageData[] entries, int version)
        {
            if (version != Version || EditorApplication.isPlaying) return;
            var dependencies = AssetDatabase.GetDependencies(path).Select(AssetDatabase.AssetPathToGUID).ToArray();
            if (HasUnsavedChanges(guid) || dependencies.Any(HasUnsavedChanges)) return;
            EnsureLoaded();
            RemoveUsageFile(guid);
            UsageFiles[guid] = new UsageFile
            {
                Guid = guid, Path = path, Hash = DependencyHash(path), Entries = entries,
                Dependencies = dependencies
            };
            IndexUsageFile(UsageFiles[guid]);
            MarkDirty();
        }

        internal static bool TryGetMeshLayout(Mesh mesh, int slot, out string signature)
        {
            EnsureLoaded();
            signature = null;
            var path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path) || !MeshFiles.TryGetValue(GetAssetKey(mesh), out var file) ||
                file.Hash != DependencyHash(path)) return false;
            var layout = file.Slots?.FirstOrDefault(entry => entry != null && entry.Slot == slot);
            if (layout == null) return false;
            signature = layout.Signature;
            return true;
        }

        internal static void StoreMeshLayout(Mesh mesh, int slot, string signature, int version)
        {
            if (version != Version || EditorApplication.isPlaying || EditorUtility.IsDirty(mesh)) return;
            var path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path)) return;
            EnsureLoaded();
            var key = GetAssetKey(mesh);
            var hash = DependencyHash(path);
            if (!MeshFiles.TryGetValue(key, out var file) || file.Hash != hash)
                MeshFiles[key] = file = new MeshFile { Key = key, Hash = hash, Slots = Array.Empty<LayoutData>() };
            AddIndexEntry(MeshKeys, key.Substring(0, key.IndexOf(':')), key);
            file.Slots = file.Slots.Where(entry => entry.Slot != slot)
                .Concat(new[] { new LayoutData { Slot = slot, Signature = signature } }).ToArray();
            MarkDirty();
        }

        private static string DependencyHash(string path)
        {
            if (!DependencyHashes.TryGetValue(path, out var hash))
                DependencyHashes[path] = hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return hash;
        }

        private static bool HasUnsavedChanges(string guid) => EditedObjects.TryGetValue(guid, out var targets) &&
            targets.Any(target => target != null && EditorUtility.IsDirty(target));

        private static void EvictAsset(string guid)
        {
            EnsureLoaded();
            foreach (var type in CandidateTypes) CandidateFiles.Remove(type + "|" + guid);
            if (UsageDependencies.TryGetValue(guid, out var roots))
                foreach (var key in roots.ToArray()) RemoveUsageFile(key);
            RemoveUsageFile(guid);
            if (MeshKeys.TryGetValue(guid, out var meshes))
            {
                foreach (var key in meshes) MeshFiles.Remove(key);
                MeshKeys.Remove(guid);
            }
            MarkDirty();
        }

        private static void AddIndexEntry(Dictionary<string, HashSet<string>> index, string dependency, string key)
        {
            if (string.IsNullOrEmpty(dependency)) return;
            if (!index.TryGetValue(dependency, out var values)) index[dependency] = values = new HashSet<string>();
            values.Add(key);
        }

        private static void IndexUsageFile(UsageFile file)
        {
            AddIndexEntry(UsageDependencies, file.Guid, file.Guid);
            foreach (var dependency in file.Dependencies ?? Array.Empty<string>())
                AddIndexEntry(UsageDependencies, dependency, file.Guid);
        }

        private static void RemoveUsageFile(string guid)
        {
            if (!UsageFiles.TryGetValue(guid, out var file)) return;
            UsageFiles.Remove(guid);
            foreach (var dependency in (file.Dependencies ?? Array.Empty<string>()).Concat(new[] { guid }))
                if (!string.IsNullOrEmpty(dependency) && UsageDependencies.TryGetValue(dependency, out var roots))
                {
                    roots.Remove(guid);
                    if (roots.Count == 0) UsageDependencies.Remove(dependency);
                }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(CachePath)) return;
                var index = JsonUtility.FromJson<DiskIndex>(File.ReadAllText(CachePath));
                if (index == null || index.Version != 1 || index.UnityVersion != Application.unityVersion) return;
                foreach (var file in index.Candidates ?? Array.Empty<CandidateFile>())
                    if (file != null && file.Guid != null && file.Type != null)
                    {
                        CandidateFiles[file.Type + "|" + file.Guid] = file;
                        CandidateTypes.Add(file.Type);
                    }
                foreach (var file in index.Usage ?? Array.Empty<UsageFile>())
                    if (file?.Guid != null)
                    {
                        UsageFiles[file.Guid] = file;
                        IndexUsageFile(file);
                    }
                foreach (var file in index.Meshes ?? Array.Empty<MeshFile>())
                    if (file?.Key != null && file.Key.IndexOf(':') > 0)
                    {
                        MeshFiles[file.Key] = file;
                        AddIndexEntry(MeshKeys, file.Key.Substring(0, file.Key.IndexOf(':')), file.Key);
                    }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                CandidateFiles.Clear();
                UsageFiles.Clear();
                MeshFiles.Clear();
                CandidateTypes.Clear();
                UsageDependencies.Clear();
                MeshKeys.Clear();
                ReportIoFailure(exception);
            }
        }

        private static void MarkDirty()
        {
            _saveAfter = EditorApplication.timeSinceStartup + 1d;
            _dirty = true;
        }

        private static void Save()
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                var json = JsonUtility.ToJson(new DiskIndex
                {
                    UnityVersion = Application.unityVersion,
                    Candidates = CandidateFiles.Values.ToArray(), Usage = UsageFiles.Values.ToArray(), Meshes = MeshFiles.Values.ToArray()
                });
                var temporary = CachePath + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(CachePath)) File.Replace(temporary, CachePath, null);
                else File.Move(temporary, CachePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ReportIoFailure(exception);
            }
        }

        private static void ReportIoFailure(Exception exception)
        {
            if (_reportedIoFailure) return;
            _reportedIoFailure = true;
            Debug.LogWarning("ee4v asset browser cache: " + exception.Message);
        }
    }
}
