using System;
using System.Collections.Generic;
using Ee4v.AvatarModify.Application;
using Ee4v.AvatarModify.Domain;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarModify.Infrastructure.Unity
{
    internal sealed class UnityAvatarAssetGateway : IAvatarAssetGateway
    {
        internal static Func<GameObject, bool> HasAvatarDescriptor = _ => false;

        public IReadOnlyList<PrefabCandidate> FindPrefabs(
            IReadOnlyList<string> assetGuids)
        {
            var result = new List<PrefabCandidate>();
            if (assetGuids == null)
            {
                return result;
            }

            for (var i = 0; i < assetGuids.Count; i++)
            {
                var guid = assetGuids[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null ||
                    PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
                {
                    continue;
                }

                result.Add(new PrefabCandidate(
                    guid,
                    path,
                    HasAvatarDescriptor(prefab)));
            }

            return result;
        }
    }
}
