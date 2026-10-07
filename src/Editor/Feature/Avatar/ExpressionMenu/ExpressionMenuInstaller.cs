using System;
using UnityEditor;
using UnityEngine;

namespace Ee4v.ExpressionMenu
{
    public static class ExpressionMenuInstaller
    {
        internal static bool IsGroupingRoot(GameObject target)
        {
            if (target == null || !PrefabUtility.IsAnyPrefabInstanceRoot(target)) return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target) ?? string.Empty;
            return path.IndexOf("/Animation/ExpressionMenu/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/ExpressionMenu.prefab", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGeneratedPrefab(GameObject target)
        {
            if (target == null) return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target) ?? string.Empty;
            return path.IndexOf("/Animation/ExpressionMenu/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (path.EndsWith("/MenuItem.prefab", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith("/ExpressionMenu.prefab", StringComparison.OrdinalIgnoreCase)) ||
                path.EndsWith(".ExpressionMenu/ExpressionMenu.prefab", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf(".ExpressionMenu/Gimmicks/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/Gimmick.prefab", StringComparison.OrdinalIgnoreCase);
        }

        internal static void SaveGeneratedPrefab(GameObject target)
        {
            if (!IsGeneratedPrefab(target)) return;
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(target);
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!ExpressionMenuModel.CanWrite(source))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.FlushUndoRecordObjects();
            // ApplyPrefabInstance targets the outermost Avatar Prefab, even when passed a nested root.
            // Save only overrides within this generated root, explicitly targeting its own asset.
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node != root.transform &&
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(node.gameObject, path) == null &&
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(node.parent.gameObject, path) != null)
                    PrefabUtility.ApplyAddedGameObject(node.gameObject, path, InteractionMode.UserAction);

            foreach (var removed in PrefabUtility.GetRemovedGameObjects(root))
            {
                var asset = AssetDatabase.GetAssetPath(removed.assetGameObject) == path ? removed.assetGameObject :
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(removed.assetGameObject, path);
                if (asset != null && removed.parentOfRemovedGameObjectInInstance.transform.IsChildOf(root.transform))
                    PrefabUtility.ApplyRemovedGameObject(removed.parentOfRemovedGameObjectInInstance, asset,
                        InteractionMode.UserAction);
            }

            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null &&
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, path) == null &&
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component.gameObject, path) != null)
                    PrefabUtility.ApplyAddedComponent(component, path, InteractionMode.UserAction);

            foreach (var removed in PrefabUtility.GetRemovedComponents(root))
            {
                var asset = AssetDatabase.GetAssetPath(removed.assetComponent) == path ? removed.assetComponent :
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(removed.assetComponent, path);
                if (asset != null && removed.containingInstanceGameObject.transform.IsChildOf(root.transform))
                    PrefabUtility.ApplyRemovedComponent(removed.containingInstanceGameObject, asset,
                        InteractionMode.UserAction);
            }

            foreach (var change in PrefabUtility.GetObjectOverrides(root, false))
            {
                var transform = change.instanceObject is GameObject go ? go.transform :
                    (change.instanceObject as Component)?.transform;
                if (transform != null && transform.IsChildOf(root.transform) &&
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(change.instanceObject, path) != null)
                    PrefabUtility.ApplyObjectOverride(change.instanceObject, path, InteractionMode.UserAction);
            }
        }
    }
}
