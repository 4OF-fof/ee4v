using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.ProjectExtension.Data;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension {
    public static class WorkspaceLabelContextMenu {
        private const int MenuPriority = 2000;

        [MenuItem("Assets/Remove Workspace Label", false, MenuPriority)]
        private static void RemoveWorkspaceLabel() {
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0) return;

            var currentWorkspace = TabListController.GetCurrentWorkspace();
            if (string.IsNullOrEmpty(currentWorkspace)) return;

            var labelName = $"Ee4v.ws.{currentWorkspace}";
            RemoveLabel(selectedObjects, labelName);
        }

        [MenuItem("Assets/Remove from Workspace", true)]
        private static bool ValidateRemoveWorkspaceLabel() {
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0) return false;

            var currentWorkspace = TabListController.GetCurrentWorkspace();
            if (string.IsNullOrEmpty(currentWorkspace)) return false;

            var labelName = $"Ee4v.ws.{currentWorkspace}";

            return selectedObjects.Any(obj =>
            {
                if (obj == null) return false;
                var labels = AssetDatabase.GetLabels(obj);
                return labels.Contains(labelName);
            });
        }

        private static void RemoveLabel(Object[] objects, string labelName) {
            var removedCount = 0;

            foreach (var obj in objects) {
                if (obj == null) continue;

                var labels = AssetDatabase.GetLabels(obj).ToList();
                if (!labels.Contains(labelName)) continue;

                labels.Remove(labelName);
                AssetDatabase.SetLabels(obj, labels.ToArray());
                removedCount++;
            }

            if (removedCount <= 0) return;
            AssetDatabase.SaveAssets();
            var message = objects.Length > 1
                ? I18N.Get("Debug.ProjectExtension.RemovedWorkspaceLabelsMultiple", labelName, removedCount)
                : I18N.Get("Debug.ProjectExtension.RemovedWorkspaceLabelSingle", labelName);
            Debug.Log(message);
        }
    }
}