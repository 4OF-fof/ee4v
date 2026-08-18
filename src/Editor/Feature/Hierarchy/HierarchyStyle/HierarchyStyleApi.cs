using System;
using System.Collections.Generic;
using Ee4v.Core.Injector;
using Ee4v.HiddenObjects;
using Ee4v.ItemStyle;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    public static class HierarchyStyleApi
    {
        public static ItemStyleValue Get(GameObject target)
        {
            var identity = GetIdentity(target);
            return string.IsNullOrEmpty(identity)
                ? ItemStyleValue.Empty(string.Empty)
                : HierarchyStyleBootstrap.Service.Get(identity);
        }

        public static void SetColor(
            IReadOnlyList<GameObject> targets,
            Color color)
        {
            HierarchyStyleBootstrap.Service.SetColor(
                GetIdentities(targets),
                color);
            Repaint();
        }

        public static void SetIcon(
            IReadOnlyList<GameObject> targets,
            string iconGuid)
        {
            HierarchyStyleBootstrap.Service.SetIcon(
                GetIdentities(targets),
                iconGuid);
            Repaint();
        }

        public static void Clear(
            IReadOnlyList<GameObject> targets)
        {
            HierarchyStyleBootstrap.Service.Clear(
                GetIdentities(targets));
            Repaint();
        }

        public static int Hide(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName)
        {
            return HiddenObjectsFeature.Visibility.Hide(
                instanceIds,
                undoOperationName);
        }

        public static int Reveal(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName)
        {
            return HiddenObjectsFeature.Visibility.Reveal(
                instanceIds,
                undoOperationName);
        }

        public static void OpenHiddenObjects(int sceneHandle = 0)
        {
            HiddenObjectsWindow.OpenForScene(sceneHandle);
        }

        public static void RefreshHiddenObjects()
        {
            HiddenObjectsWindow.RefreshAll();
        }

        private static IReadOnlyList<string> GetIdentities(
            IReadOnlyList<GameObject> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return Array.Empty<string>();
            }

            var identities = new List<string>(targets.Count);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < targets.Count; i++)
            {
                var identity = GetIdentity(targets[i]);
                if (!string.IsNullOrEmpty(identity) &&
                    visited.Add(identity))
                {
                    identities.Add(identity);
                }
            }

            return identities;
        }

        private static string GetIdentity(GameObject target)
        {
            return target != null && target.scene.IsValid()
                ? HierarchyStyleBootstrap.Identity.Get(target)
                : string.Empty;
        }

        private static void Repaint()
        {
            InjectorApi.Repaint(InjectionChannel.HierarchyItem);
        }
    }
}
