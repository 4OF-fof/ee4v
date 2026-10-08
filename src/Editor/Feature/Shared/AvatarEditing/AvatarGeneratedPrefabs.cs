using System;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarEditing
{
    public static class AvatarGeneratedPrefabs
    {
        public static bool IsExpressionMenu(GameObject target)
        {
            if (target == null) { return false; }
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target) ?? string.Empty;
            return path.IndexOf("/Animation/ExpressionMenu/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (path.EndsWith("/MenuItem.prefab", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith("/ExpressionMenu.prefab", StringComparison.OrdinalIgnoreCase)) ||
                path.EndsWith(".ExpressionMenu/ExpressionMenu.prefab", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf(".ExpressionMenu/Gimmicks/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/Gimmick.prefab", StringComparison.OrdinalIgnoreCase);
        }
    }
}
