using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.AvatarInfo
{
    [InitializeOnLoad]
    public static class AvatarPlayModePerformanceCache
    {
        private const string ResultsKey = "Ee4v.AvatarInfo.PlayModePerformance.Results";
        private const string PendingKey = "Ee4v.AvatarInfo.PlayModePerformance.Pending";

        public sealed class Record
        {
            public string Key;
            public string Scene;
            public string Hierarchy;
            public string Name;
            public bool AaoAttached;
            public DateTime? CapturedAt;
            public AvatarInfoParameterMemory ParameterMemory;
            public AvatarInfoAnalysis.PerformanceReport Desktop;
            public AvatarInfoAnalysis.PerformanceReport Mobile;
            public string Error;
        }

        private sealed class Store
        {
            public List<Record> Records = new List<Record>();
        }

        private static readonly Dictionary<string, Record> Results = Load(ResultsKey)
            .ToDictionary(record => record.Key, StringComparer.Ordinal);
        private static List<Record> _pending = Load(PendingKey);
        private static readonly List<(Func<GameObject> Avatar, Func<bool> Successful, Record Source)> Built =
            new List<(Func<GameObject>, Func<bool>, Record)>();
        private static bool _enteredPlayMode = EditorApplication.isPlaying;

        public static event Action Changed;

        static AvatarPlayModePerformanceCache()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static Record Get(GameObject avatar)
        {
            if (avatar == null) { return null; }
            if (Results.TryGetValue(GetKey(avatar), out var record)) { return record; }
            if (!EditorApplication.isPlaying) { return null; }
            var matches = Results.Values.Where(result => result.Scene == GetSceneKey(avatar) &&
                result.Hierarchy == GetHierarchy(avatar)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        public static GameObject FindPlayModeAvatar(string scenePath, string prefabPath)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) { return null; }
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var sources = _pending.Where(record => record.Scene == scenePath &&
                record.Key.EndsWith("|" + guid, StringComparison.Ordinal)).ToArray();
            if (sources.Length != 1) { return null; }
            var source = sources[0];
            var roots = scene.GetRootGameObjects();
            var exact = roots.FirstOrDefault(root => GetHierarchy(root) == source.Hierarchy);
            if (exact != null) { return exact; }
            var matches = roots.Where(root => NormalizeName(root.name) == source.Name).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        internal static Record FindBuildSource(GameObject avatar)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode || _pending.Count == 0)
            {
                return null;
            }
            var scene = GetSceneKey(avatar);
            var hierarchy = GetHierarchy(avatar);
            var candidates = _pending.Where(record => record.Scene == scene).ToArray();
            var exact = candidates.Where(record => record.Hierarchy == hierarchy).ToArray();
            var source = exact.Length == 1 ? exact[0] : null;
            if (source == null)
            {
                var names = candidates.Where(record => record.Name == NormalizeName(avatar.name)).ToArray();
                if (names.Length == 1) { source = names[0]; }
            }
            return source;
        }

        internal static void RememberBuild(Record source, Func<GameObject> avatar, Func<bool> successful)
        {
            if (source == null) { return; }
            Built.Add((avatar, successful, source));
            if (_enteredPlayMode)
            {
                EditorApplication.delayCall -= CaptureBuiltAvatars;
                EditorApplication.delayCall += CaptureBuiltAvatars;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _enteredPlayMode = false;
                Built.Clear();
                _pending = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(avatar => !EditorUtility.IsPersistent(avatar) && avatar.scene.IsValid() &&
                        avatar.scene.isLoaded && !EditorSceneManager.IsPreviewScene(avatar.scene) &&
                        avatar.GetComponents<Component>().Any(component => component != null &&
                            component.GetType().FullName == "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor"))
                    .Select(avatar => new Record
                    {
                        Key = GetKey(avatar), Scene = GetSceneKey(avatar), Hierarchy = GetHierarchy(avatar),
                        Name = NormalizeName(avatar.name),
                        AaoAttached = AvatarInfoAnalysis.HasAaoComponents(avatar)
                    }).ToList();
                foreach (var record in _pending) { Results[record.Key] = record; }
                Save(PendingKey, _pending);
                Save(ResultsKey, Results.Values);
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _enteredPlayMode = true;
                CaptureBuiltAvatars();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _enteredPlayMode = false;
                Built.Clear();
                EditorApplication.delayCall -= CaptureBuiltAvatars;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                _pending.Clear();
                SessionState.EraseString(PendingKey);
                Changed?.Invoke();
            }
        }

        private static void CaptureBuiltAvatars()
        {
            if (!_enteredPlayMode) { return; }
            foreach (var build in Built)
            {
                var record = build.Source;
                var avatar = build.Avatar();
                if (avatar == null || !build.Successful()) { continue; }
                if (AvatarInfoAnalysis.TryReadPerformance(avatar, false,
                        out var desktopRating, out var desktopMetrics, out var error) &&
                    AvatarInfoAnalysis.TryReadPerformance(avatar, true,
                        out var mobileRating, out var mobileMetrics, out error))
                {
                    record.Desktop = new AvatarInfoAnalysis.PerformanceReport
                        { Rating = desktopRating, Metrics = desktopMetrics };
                    record.Mobile = new AvatarInfoAnalysis.PerformanceReport
                        { Rating = mobileRating, Metrics = mobileMetrics };
                    record.CapturedAt = DateTime.Now;
                    record.ParameterMemory = AvatarInfoSdk.Provider?.ReadParameterMemory(avatar);
                    record.Error = null;
                }
                else { record.Error = error; }
                Results[record.Key] = record;
            }
            Built.Clear();
            Save(ResultsKey, Results.Values);
            Changed?.Invoke();
        }

        internal static string GetKey(GameObject avatar)
        {
            var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(avatar);
            var indices = new List<int>();
            for (var current = avatar.transform; current != null; current = current.parent)
                indices.Add(current.GetSiblingIndex());
            indices.Reverse();
            return GetSceneKey(avatar) + "|" + string.Join("/", indices) + "|" +
                AssetDatabase.AssetPathToGUID(sourcePath);
        }

        private static string GetSceneKey(GameObject avatar)
        {
            return string.IsNullOrEmpty(avatar.scene.path) ? avatar.scene.name : avatar.scene.path;
        }

        private static string GetHierarchy(GameObject avatar)
        {
            var segments = new List<string>();
            for (var transform = avatar.transform; transform != null; transform = transform.parent)
            {
                segments.Add(Uri.EscapeDataString(NormalizeName(transform.name)) +
                    "[" + transform.GetSiblingIndex() + "]");
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string NormalizeName(string name)
        {
            return name.EndsWith("(Clone)", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "(Clone)".Length) : name;
        }

        private static List<Record> Load(string key)
        {
            var json = SessionState.GetString(key, string.Empty);
            return string.IsNullOrEmpty(json) ? new List<Record>()
                : JsonConvert.DeserializeObject<Store>(json)?.Records ?? new List<Record>();
        }

        private static void Save(string key, IEnumerable<Record> records)
        {
            SessionState.SetString(key, JsonConvert.SerializeObject(new Store { Records = records.ToList() }));
        }
    }

}
