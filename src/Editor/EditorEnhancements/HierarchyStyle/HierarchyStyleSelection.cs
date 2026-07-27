using System.Collections.Generic;

namespace Ee4v.HierarchyStyle
{
    internal static class HierarchyStyleSelection
    {
        public static IReadOnlyList<int> ResolveTargetIds(
            int hoveredInstanceId,
            IReadOnlyList<int> selectedInstanceIds)
        {
            if (hoveredInstanceId == 0)
            {
                return new int[0];
            }

            if (selectedInstanceIds == null ||
                selectedInstanceIds.Count <= 1)
            {
                return new[] { hoveredInstanceId };
            }

            var containsHovered = false;
            var unique = new List<int>();
            var visited = new HashSet<int>();
            for (var i = 0;
                 i < selectedInstanceIds.Count;
                 i++)
            {
                var instanceId =
                    selectedInstanceIds[i];
                if (instanceId == 0 ||
                    !visited.Add(instanceId))
                {
                    continue;
                }

                unique.Add(instanceId);
                containsHovered |=
                    instanceId == hoveredInstanceId;
            }

            return containsHovered && unique.Count > 1
                ? unique
                : new[] { hoveredInstanceId };
        }
    }
}
