using System;
using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleIconApplier
    {
        private readonly HierarchyStyleIconCache _iconCache;
        private readonly HashSet<int> _hierarchyStyled =
            new HashSet<int>();
        private readonly HashSet<int> _legacySceneIconCleared =
            new HashSet<int>();

        public HierarchyStyleIconApplier(
            HierarchyStyleIconCache iconCache)
        {
            _iconCache = iconCache ??
                throw new ArgumentNullException(
                    nameof(iconCache));
        }

        public Texture2D Apply(
            GameObject gameObject,
            string iconGuid)
        {
            if (gameObject == null)
            {
                return null;
            }

            var instanceId = gameObject.GetInstanceID();
            ClearLegacySceneIcon(
                gameObject,
                instanceId);

            iconGuid = iconGuid ?? string.Empty;
            var icon = string.IsNullOrEmpty(iconGuid)
                ? ResolveDefaultIcon(gameObject)
                : _iconCache.Get(iconGuid);
            var applied =
                SceneHierarchyItemIcon.TrySetItemIcon(
                    instanceId,
                    icon);

            if (string.IsNullOrEmpty(iconGuid))
            {
                _hierarchyStyled.Remove(instanceId);
            }
            else
            {
                _hierarchyStyled.Add(instanceId);
            }

            return applied ? null : icon;
        }

        public Texture2D ApplyConfigured(
            GameObject gameObject,
            HierarchyStyleValue style)
        {
            if (style != null && style.HasIcon)
            {
                return Apply(
                    gameObject,
                    style.IconGuid);
            }

            if (gameObject != null &&
                _hierarchyStyled.Contains(
                    gameObject.GetInstanceID()))
            {
                Apply(gameObject, string.Empty);
            }

            return null;
        }

        public void RemoveAll()
        {
            var instanceIds =
                new List<int>(_hierarchyStyled);
            for (var i = 0;
                 i < instanceIds.Count;
                 i++)
            {
                var gameObject =
                    EditorUtility.InstanceIDToObject(
                        instanceIds[i]) as GameObject;
                if (gameObject == null)
                {
                    continue;
                }

                ClearLegacySceneIcon(
                    gameObject,
                    instanceIds[i]);
                SceneHierarchyItemIcon.TrySetItemIcon(
                    instanceIds[i],
                    ResolveDefaultIcon(gameObject));
            }

            _hierarchyStyled.Clear();
        }

        private void ClearLegacySceneIcon(
            GameObject gameObject,
            int instanceId)
        {
            if (!_legacySceneIconCleared.Add(instanceId))
            {
                return;
            }

            EditorGUIUtility.SetIconForObject(
                gameObject,
                null);
        }

        private static Texture2D ResolveDefaultIcon(
            GameObject gameObject)
        {
            if (PrefabUtility.GetPrefabAssetType(
                    gameObject) !=
                PrefabAssetType.NotAPrefab &&
                !PrefabUtility.IsAnyPrefabInstanceRoot(
                    gameObject))
            {
                return EditorGUIUtility.IconContent(
                    "GameObject Icon").image as Texture2D;
            }

            return EditorGUIUtility.ObjectContent(
                gameObject,
                typeof(GameObject)).image as Texture2D;
        }
    }
}
