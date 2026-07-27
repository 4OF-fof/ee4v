using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyObjectIdentity
    {
        private readonly Dictionary<int, string> _cache =
            new Dictionary<int, string>();

        public string Get(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            var instanceId = gameObject.GetInstanceID();
            if (_cache.TryGetValue(
                    instanceId,
                    out var objectId))
            {
                return objectId;
            }

            objectId = GlobalObjectId
                .GetGlobalObjectIdSlow(gameObject)
                .ToString();
            _cache[instanceId] = objectId;
            return objectId;
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
