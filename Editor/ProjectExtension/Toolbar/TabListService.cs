using System.Linq;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Toolbar {
    public static class TabListService {
        private static string _currentWorkspace;

        public static void SetCurrentWorkspace(string workspaceName) {
            _currentWorkspace = workspaceName;
        }

        public static string GetCurrentWorkspace() {
            return _currentWorkspace;
        }

        public static void RemoveWorkspaceLabels(string workspaceName) {
            if (string.IsNullOrEmpty(workspaceName)) return;

            var labelName = $"Ee4v.ws.{workspaceName}";
            var guids = AssetDatabase.FindAssets($"l:{labelName}");
            if (guids.Length == 0) return;

            var removedCount = 0;
            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null) continue;

                var labels = AssetDatabase.GetLabels(asset).ToList();
                if (!labels.Remove(labelName)) continue;
                AssetDatabase.SetLabels(asset, labels.ToArray());
                removedCount++;
            }

            if (removedCount <= 0) return;
            AssetDatabase.SaveAssets();
            Debug.Log(I18N.Get("Debug.ProjectExtension.RemovedWorkspaceLabels", labelName, removedCount));
        }
    }
}