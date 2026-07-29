using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.AssetManager.Domain
{
    internal enum CollectionPlacementError
    {
        None,
        EmptySelection,
        CollectionNotFound,
        ParentNotFound,
        SmartCollectionParent,
        Cycle
    }

    internal sealed class CollectionPlacementNode
    {
        internal CollectionPlacementNode(
            string id,
            string parentId,
            bool isSmartCollection,
            int sortOrder)
        {
            Id = id ?? string.Empty;
            ParentId = NormalizeParentId(parentId);
            IsSmartCollection = isSmartCollection;
            SortOrder = sortOrder;
        }

        internal string Id { get; }
        internal string ParentId { get; }
        internal bool IsSmartCollection { get; }
        internal int SortOrder { get; }

        internal static string NormalizeParentId(string parentId)
        {
            return string.IsNullOrWhiteSpace(parentId)
                ? string.Empty
                : parentId;
        }
    }

    internal sealed class CollectionPlacementResult
    {
        internal CollectionPlacementResult(
            CollectionPlacementError error,
            string targetParentId,
            IReadOnlyList<string> movingIds,
            IReadOnlyList<string> targetSiblingIds,
            bool changesPlacement)
        {
            Error = error;
            TargetParentId =
                CollectionPlacementNode.NormalizeParentId(targetParentId);
            MovingIds = movingIds ?? Array.Empty<string>();
            TargetSiblingIds =
                targetSiblingIds ?? Array.Empty<string>();
            ChangesPlacement = changesPlacement;
        }

        internal CollectionPlacementError Error { get; }
        internal string TargetParentId { get; }
        internal IReadOnlyList<string> MovingIds { get; }
        internal IReadOnlyList<string> TargetSiblingIds { get; }
        internal bool ChangesPlacement { get; }
        internal bool IsValid => Error == CollectionPlacementError.None;
    }

    internal static class CollectionPlacementPolicy
    {
        internal static CollectionPlacementResult Evaluate(
            IReadOnlyList<CollectionPlacementNode> nodes,
            IReadOnlyList<string> requestedIds,
            string targetParentId,
            int siblingIndex)
        {
            var byId = (nodes ?? Array.Empty<CollectionPlacementNode>())
                .Where(node =>
                    node != null &&
                    !string.IsNullOrWhiteSpace(node.Id))
                .GroupBy(node => node.Id, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
            var requested = DistinctIds(requestedIds);
            if (requested.Count == 0)
            {
                return Error(CollectionPlacementError.EmptySelection);
            }

            if (requested.Any(id => !byId.ContainsKey(id)))
            {
                return Error(CollectionPlacementError.CollectionNotFound);
            }

            var movingIds = GetTopLevelIds(
                byId,
                requestedIds,
                new HashSet<string>(requested, StringComparer.Ordinal));
            if (movingIds.Count == 0)
            {
                return Error(CollectionPlacementError.EmptySelection);
            }

            var normalizedParentId =
                CollectionPlacementNode.NormalizeParentId(targetParentId);
            CollectionPlacementNode targetParent = null;
            if (normalizedParentId.Length > 0 &&
                !byId.TryGetValue(normalizedParentId, out targetParent))
            {
                return Error(CollectionPlacementError.ParentNotFound);
            }

            if (normalizedParentId.Length > 0 &&
                targetParent.IsSmartCollection)
            {
                return Error(CollectionPlacementError.SmartCollectionParent);
            }

            for (var i = 0; i < movingIds.Count; i++)
            {
                if (IsAncestorOrSelf(
                        byId,
                        movingIds[i],
                        normalizedParentId))
                {
                    return Error(CollectionPlacementError.Cycle);
                }
            }

            var currentSiblings = GetSiblings(
                byId.Values,
                normalizedParentId);
            var movingIdSet = new HashSet<string>(
                movingIds,
                StringComparer.Ordinal);
            var nextSiblings = currentSiblings
                .Where(id => !movingIdSet.Contains(id))
                .ToList();
            var targetIndex = siblingIndex < 0
                ? nextSiblings.Count
                : Math.Max(0, Math.Min(siblingIndex, nextSiblings.Count));
            nextSiblings.InsertRange(targetIndex, movingIds);

            var changesParent = movingIds.Any(id =>
                !string.Equals(
                    byId[id].ParentId,
                    normalizedParentId,
                    StringComparison.Ordinal));
            var changesOrder = !currentSiblings.SequenceEqual(
                nextSiblings,
                StringComparer.Ordinal);
            return new CollectionPlacementResult(
                CollectionPlacementError.None,
                normalizedParentId,
                movingIds,
                nextSiblings,
                changesParent || changesOrder);
        }

        internal static IReadOnlyList<string> GetTopLevelIds(
            IReadOnlyList<CollectionPlacementNode> nodes,
            IReadOnlyList<string> requestedIds)
        {
            var byId = (nodes ?? Array.Empty<CollectionPlacementNode>())
                .Where(node =>
                    node != null &&
                    !string.IsNullOrWhiteSpace(node.Id))
                .GroupBy(node => node.Id, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
            var requested = new HashSet<string>(
                DistinctIds(requestedIds),
                StringComparer.Ordinal);
            return GetTopLevelIds(byId, requestedIds, requested);
        }

        private static IReadOnlyList<string> GetTopLevelIds(
            IReadOnlyDictionary<string, CollectionPlacementNode> byId,
            IReadOnlyList<string> requestedIds,
            ISet<string> requested)
        {
            if (requestedIds == null)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            var added = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < requestedIds.Count; i++)
            {
                var id = requestedIds[i];
                if (string.IsNullOrWhiteSpace(id) ||
                    !byId.ContainsKey(id) ||
                    !added.Add(id) ||
                    HasSelectedAncestor(byId, id, requested))
                {
                    continue;
                }

                result.Add(id);
            }

            return result;
        }

        private static bool HasSelectedAncestor(
            IReadOnlyDictionary<string, CollectionPlacementNode> byId,
            string id,
            ISet<string> selectedIds)
        {
            CollectionPlacementNode node;
            if (!byId.TryGetValue(id, out node))
            {
                return false;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var parentId = node.ParentId;
            while (parentId.Length > 0 && visited.Add(parentId))
            {
                if (selectedIds.Contains(parentId))
                {
                    return true;
                }

                if (!byId.TryGetValue(parentId, out node))
                {
                    return false;
                }

                parentId = node.ParentId;
            }

            return false;
        }

        private static bool IsAncestorOrSelf(
            IReadOnlyDictionary<string, CollectionPlacementNode> byId,
            string candidateAncestorId,
            string targetId)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var currentId = targetId;
            while (currentId.Length > 0 && visited.Add(currentId))
            {
                if (string.Equals(
                        candidateAncestorId,
                        currentId,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                CollectionPlacementNode node;
                if (!byId.TryGetValue(currentId, out node))
                {
                    return false;
                }

                currentId = node.ParentId;
            }

            return false;
        }

        private static IReadOnlyList<string> GetSiblings(
            IEnumerable<CollectionPlacementNode> nodes,
            string parentId)
        {
            return nodes
                .Where(node => string.Equals(
                    node.ParentId,
                    parentId,
                    StringComparison.Ordinal))
                .OrderBy(node => node.SortOrder)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .Select(node => node.Id)
                .ToArray();
        }

        private static IReadOnlyList<string> DistinctIds(
            IReadOnlyList<string> ids)
        {
            return (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static CollectionPlacementResult Error(
            CollectionPlacementError error)
        {
            return new CollectionPlacementResult(
                error,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false);
        }
    }
}
