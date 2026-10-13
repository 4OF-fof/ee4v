using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.Core
{
    /// <summary>Coalesces derived asset writes without delaying changes to loaded objects.</summary>
    [InitializeOnLoad]
    public static class DeferredAssetSave
    {
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.Ordinal);
        private static double _saveAt;

        static DeferredAssetSave()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
            EditorApplication.quitting += Flush;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Flush();
            };
            EditorSceneManager.sceneSaving += (scene, path) => Flush();
        }

        public static void Queue(UnityEngine.Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return;
            Pending.Add(path);
            _saveAt = EditorApplication.timeSinceStartup + 0.3d;
        }

        public static void Flush()
        {
            foreach (var path in new List<string>(Pending)) Save(path);
        }

        private static void Update()
        {
            if (Pending.Count == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < _saveAt) return;
            // Saving one asset file per update allows input between writes.
            string next = null;
            foreach (var path in Pending) { next = path; break; }
            try { Save(next); }
            catch (Exception exception)
            {
                _saveAt = double.PositiveInfinity;
                Debug.LogException(exception);
            }
        }

        private static void Save(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!EditorUtility.IsDirty(asset)) continue;
                AssetDatabase.SaveAssetIfDirty(asset);
                break; // Subassets are serialized with the same file.
            }
            Pending.Remove(path);
        }
    }
}
