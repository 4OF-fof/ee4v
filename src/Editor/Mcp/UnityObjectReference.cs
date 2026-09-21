using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class UnityObjectReference
    {
        internal static string Create(UnityEngine.Object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(value);
            var serialized = globalId.ToString();
            if (!string.IsNullOrEmpty(serialized) &&
                !serialized.EndsWith("-0-0", StringComparison.Ordinal))
            {
                return serialized;
            }

            return AssetDatabase.GetAssetPath(value) ?? string.Empty;
        }

        internal static GameObject ResolveGameObject(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new McpToolException(
                    "avatar_ref_required",
                    "avatarRef is required. Use ee4v_find_avatars to obtain one.");
            }

            var value = reference.Trim();
            if (GlobalObjectId.TryParse(value, out var globalId))
            {
                var resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (resolved is GameObject gameObject)
                {
                    return gameObject;
                }

                if (resolved is Component component)
                {
                    return component.gameObject;
                }
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(value);
            if (asset != null)
            {
                return asset;
            }

            var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(candidate =>
                    !EditorUtility.IsPersistent(candidate) &&
                    candidate.scene.IsValid() &&
                    string.Equals(
                        HierarchyPath(candidate.transform),
                        value,
                        StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matches.Length == 1)
            {
                return matches[0];
            }

            throw new McpToolException(
                matches.Length > 1 ? "avatar_ref_ambiguous" : "avatar_not_found",
                matches.Length > 1
                    ? "avatarRef matches more than one loaded object. Use a GlobalObjectId."
                    : "The avatar could not be resolved. Run ee4v_find_avatars again.");
        }

        internal static string HierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
