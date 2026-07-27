using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleIconCache
    {
        private readonly Dictionary<string, Texture2D> _cache =
            new Dictionary<string, Texture2D>(
                StringComparer.Ordinal);

        public Texture2D Get(string iconGuid)
        {
            if (string.IsNullOrEmpty(iconGuid))
            {
                return null;
            }

            if (_cache.TryGetValue(iconGuid, out var cached))
            {
                return cached;
            }

            var path =
                AssetDatabase.GUIDToAssetPath(iconGuid);
            var texture = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(
                    path);
            _cache[iconGuid] = texture;
            return texture;
        }
    }
}
