using System.Collections.Generic;

namespace Ee4v.Core.Hierarchy
{
    public interface IHierarchyObjectVisibilityService
    {
        int HideFromHierarchy(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName);

        int RevealInHierarchy(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName);
    }
}
