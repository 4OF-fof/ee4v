using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Hierarchy;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal interface IHierarchyObjectVisibility
    {
        int Hide(
            IReadOnlyList<GameObject> targets,
            string undoOperationName);
    }

    internal sealed class UnityHierarchyObjectVisibility
        : IHierarchyObjectVisibility
    {
        public int Hide(
            IReadOnlyList<GameObject> targets,
            string undoOperationName)
        {
            if (targets == null || targets.Count == 0)
            {
                return 0;
            }

            var instanceIds = targets
                .Where(gameObject =>
                    gameObject != null &&
                    gameObject.scene.IsValid())
                .Distinct()
                .Select(gameObject =>
                    gameObject.GetInstanceID())
                .ToArray();
            if (instanceIds.Length == 0)
            {
                return 0;
            }

            return HierarchyObjectVisibilityApi
                .HideFromHierarchy(
                    instanceIds,
                    undoOperationName);
        }
    }
}
