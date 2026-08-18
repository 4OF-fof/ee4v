using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.HiddenObjects
{
    internal static class HiddenObjectTreeBuilder
    {
        public static IReadOnlyList<HiddenObjectSceneGroup> Build(
            IReadOnlyList<HiddenObjectSnapshotItem> snapshot,
            int sceneHandle,
            string query)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                return Array.Empty<HiddenObjectSceneGroup>();
            }

            var normalizedQuery = (query ?? string.Empty).Trim();
            return snapshot
                .Where(item =>
                    sceneHandle == 0 || item.SceneHandle == sceneHandle)
                .GroupBy(item => new
                {
                    item.SceneHandle,
                    item.SceneName
                })
                .OrderBy(group => group.Min(item => item.Order))
                .Select(group => BuildSceneGroup(
                    group.Key.SceneHandle,
                    group.Key.SceneName,
                    group.OrderBy(item => item.Order).ToArray(),
                    normalizedQuery))
                .Where(group => group.Roots.Count > 0)
                .ToArray();
        }

        private static HiddenObjectSceneGroup BuildSceneGroup(
            int sceneHandle,
            string sceneName,
            IReadOnlyList<HiddenObjectSnapshotItem> items,
            string query)
        {
            var nodeById = items.ToDictionary(
                item => item.InstanceId,
                item => new MutableNode(item));
            var roots = new List<MutableNode>();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var node = nodeById[item.InstanceId];
                if (item.ParentInstanceId != 0 &&
                    nodeById.TryGetValue(
                        item.ParentInstanceId,
                        out var parent))
                {
                    parent.Children.Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }

            var visibleRoots = roots
                .Select(root => Filter(root, query, false))
                .Where(root => root != null)
                .ToArray();
            return new HiddenObjectSceneGroup(
                sceneHandle,
                sceneName,
                visibleRoots);
        }

        private static HiddenObjectTreeNode Filter(
            MutableNode node,
            string query,
            bool ancestorMatches)
        {
            var matches = ancestorMatches ||
                string.IsNullOrWhiteSpace(query) ||
                node.Item.Name.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0;
            var children = node.Children
                .Select(child => Filter(child, query, matches))
                .Where(child => child != null)
                .ToArray();
            var hasHiddenSubtree = node.Item.IsHidden ||
                node.Children.Any(ContainsHidden);
            var visibleForSearch = string.IsNullOrWhiteSpace(query) ||
                matches ||
                children.Length > 0;

            if (!hasHiddenSubtree || !visibleForSearch)
            {
                return null;
            }

            return new HiddenObjectTreeNode(
                node.Item.InstanceId,
                node.Item.Name,
                node.Item.IsHidden,
                children);
        }

        private static bool ContainsHidden(MutableNode node)
        {
            return node.Item.IsHidden ||
                node.Children.Any(ContainsHidden);
        }

        private sealed class MutableNode
        {
            public MutableNode(HiddenObjectSnapshotItem item)
            {
                Item = item;
                Children = new List<MutableNode>();
            }

            public HiddenObjectSnapshotItem Item { get; }

            public List<MutableNode> Children { get; }
        }
    }
}
