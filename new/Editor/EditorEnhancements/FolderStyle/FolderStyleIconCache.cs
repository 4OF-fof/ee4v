using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderStyle
{
    internal sealed class FolderStyleIconCache
    {
        private readonly Dictionary<string, Texture> _cache =
            new Dictionary<string, Texture>(
                StringComparer.Ordinal);

        public Texture Get(string iconGuid)
        {
            if (string.IsNullOrEmpty(iconGuid))
            {
                return null;
            }

            if (_cache.TryGetValue(iconGuid, out var cached))
            {
                return cached;
            }

            var path = AssetDatabase.GUIDToAssetPath(iconGuid);
            var texture = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(path);
            _cache[iconGuid] = texture;
            return texture;
        }
    }
}
