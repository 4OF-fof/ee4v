using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Ee4v.AssetManager.UI.AvatarPerformanceCapturePlugin))]

namespace Ee4v.AssetManager.UI
{
    [InitializeOnLoad]
    internal static class AvatarPlayModePerformanceCache
    {
        private const string ResultsKey = "Ee4v.AssetManager.PlayModePerformance.Results";
        private const string PendingKey = "Ee4v.AssetManager.PlayModePerformance.Pending";

        internal sealed class Record
        {
            public string Key;
            public string Scene;
            public string Hierarchy;
            public string Name;
            public bool AaoAttached;
            public DateTime? CapturedAt;
            public AvatarOverviewAnalysis.PerformanceReport Desktop;
            public AvatarOverviewAnalysis.PerformanceReport Mobile;
            public string Error;
        }

        private sealed class Store
        {
            public List<Record> Records = new List<Record>();
        }

        private sealed class CaptureState
        {
            internal Record Source;
        }

        private static readonly Dictionary<string, Record> Results = Load(ResultsKey)
            .ToDictionary(record => record.Key, StringComparer.Ordinal);
        private static List<Record> _pending = Load(PendingKey);
        private static readonly List<(BuildContext Context, Record Source)> Built =
            new List<(BuildContext, Record)>();
        private static bool _enteredPlayMode = EditorApplication.isPlaying;

        internal static event Action Changed;

        static AvatarPlayModePerformanceCache()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static Record Get(GameObject avatar)
        {
            if (avatar == null) { return null; }
            if (Results.TryGetValue(GetKey(avatar), out var record)) { return record; }
            if (!EditorApplication.isPlaying) { return null; }
            var matches = Results.Values.Where(result => result.Scene == GetSceneKey(avatar) &&
                result.Hierarchy == GetHierarchy(avatar)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        internal static GameObject FindPlayModeAvatar(string prefabPath)
        {
            var scenePath = DerivedAssetCreator.GetWorkingScenePath(prefabPath);
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

        internal static void RememberSource(BuildContext context)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode || _pending.Count == 0)
            {
                return;
            }
            var avatar = context.AvatarRootObject;
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
            context.GetState<CaptureState>().Source = source;
        }

        internal static void RememberBuild(BuildContext context)
        {
            var source = context.GetState<CaptureState>().Source;
            if (source == null) { return; }
            Built.Add((context, source));
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
                        AaoAttached = AvatarOverviewAnalysis.HasAaoComponents(avatar)
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
                var avatar = build.Context.AvatarRootObject;
                if (avatar == null || !build.Context.Successful) { continue; }
                if (AvatarOverviewAnalysis.TryReadPerformance(avatar, false,
                        out var desktopRating, out var desktopMetrics, out var error) &&
                    AvatarOverviewAnalysis.TryReadPerformance(avatar, true,
                        out var mobileRating, out var mobileMetrics, out error))
                {
                    record.Desktop = new AvatarOverviewAnalysis.PerformanceReport
                        { Rating = desktopRating, Metrics = desktopMetrics };
                    record.Mobile = new AvatarOverviewAnalysis.PerformanceReport
                        { Rating = mobileRating, Metrics = mobileMetrics };
                    record.CapturedAt = DateTime.Now;
                    record.Error = null;
                }
                else { record.Error = error; }
                Results[record.Key] = record;
            }
            Built.Clear();
            Save(ResultsKey, Results.Values);
            Changed?.Invoke();
        }

        private static string GetKey(GameObject avatar)
        {
            var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(avatar);
            return GetSceneKey(avatar) + "|" + GetHierarchy(avatar) + "|" +
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

    public sealed class AvatarPerformanceCapturePlugin : Plugin<AvatarPerformanceCapturePlugin>
    {
        public override string QualifiedName => "dev.4of.ee4v.avatar-performance-capture";
        public override string DisplayName => "ee4v Avatar Performance Capture";

        protected override void Configure()
        {
            InPhase(BuildPhase.FirstChance).Run("Remember play mode avatar", context =>
                AvatarPlayModePerformanceCache.RememberSource(context));
            InPhase(BuildPhase.Optimizing).AfterPlugin("com.anatawa12.avatar-optimizer")
                .Run("Remember play mode build", context =>
                    AvatarPlayModePerformanceCache.RememberBuild(context));
        }
    }
}
