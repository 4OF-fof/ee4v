using System;
using UnityEditor;
using UnityEngine;

namespace Ee4v.ExpressionMenu
{
    public static class ExpressionMenuInstaller
    {
        public static bool IsGeneratedPrefab(GameObject target)
        {
            if (target == null) return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target) ?? string.Empty;
            return path.IndexOf("/Animation/ExpressionMenu/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/MenuItem.prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".ExpressionMenu/ExpressionMenu.prefab", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf(".ExpressionMenu/Gimmicks/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/Gimmick.prefab", StringComparison.OrdinalIgnoreCase);
        }

        internal static void SaveGeneratedPrefab(GameObject target)
        {
            if (!IsGeneratedPrefab(target)) return;
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(target);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (!ExpressionMenuModel.CanWrite(source))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.FlushUndoRecordObjects();
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
        }
    }
}
