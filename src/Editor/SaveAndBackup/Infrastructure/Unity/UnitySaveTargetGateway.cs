using UnityEditor;
using UnityEngine;

namespace Ee4v.SaveAndBackup.Infrastructure.Unity
{
    internal static class UnitySaveTargetGateway
    {
        internal static bool TryGetPrefab(
            GameObject target,
            out string guid,
            out bool hasUnappliedOverrides)
        {
            guid = string.Empty;
            hasUnappliedOverrides = false;
            if (target == null)
            {
                return false;
            }

            var root =
                PrefabUtility.GetNearestPrefabInstanceRoot(
                    target);
            var source = root == null
                ? target
                : PrefabUtility
                    .GetCorrespondingObjectFromSource(root);
            var path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(path))
            {
                path = PrefabUtility
                    .GetPrefabAssetPathOfNearestInstanceRoot(
                        target);
            }

            guid = AssetDatabase.AssetPathToGUID(path);
            hasUnappliedOverrides = root != null &&
                PrefabUtility.HasPrefabInstanceAnyOverrides(
                    root,
                    false);
            return !string.IsNullOrWhiteSpace(guid);
        }
    }
}
