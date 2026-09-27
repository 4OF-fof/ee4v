using System;
using System.Collections.Generic;
using Ee4v.Core.EditorIntegration;
using Ee4v.ItemStyle;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleIconApplier
    {
        private readonly ItemStyleIconCache _iconCache;
        private readonly HashSet<int> _hierarchyStyled =
            new HashSet<int>();

        public HierarchyStyleIconApplier(
            ItemStyleIconCache iconCache)
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
            iconGuid = iconGuid ?? string.Empty;
            var icon = string.IsNullOrEmpty(iconGuid)
                ? ResolveDefaultIcon(gameObject)
                : _iconCache.Get(iconGuid) as Texture2D;
            var applied =
                HierarchyItemApi.TrySetIcon(
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
            ItemStyleValue style)
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

                HierarchyItemApi.TrySetIcon(
                    instanceIds[i],
                    ResolveDefaultIcon(gameObject));
            }

            _hierarchyStyled.Clear();
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
