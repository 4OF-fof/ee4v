using System;
using System.Collections.Generic;

namespace Ee4v.FolderStyle
{
    internal static class FolderStyleSelection
    {
        public static IReadOnlyList<string> ResolveTargets(
            string hoveredFolderGuid,
            IReadOnlyList<string> selectedFolderGuids)
        {
            if (string.IsNullOrEmpty(hoveredFolderGuid))
            {
                return Array.Empty<string>();
            }

            if (selectedFolderGuids == null ||
                selectedFolderGuids.Count <= 1)
            {
                return new[] { hoveredFolderGuid };
            }

            var containsHovered = false;
            var unique = new List<string>();
            var visited =
                new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < selectedFolderGuids.Count; i++)
            {
                var guid = selectedFolderGuids[i];
                if (string.IsNullOrEmpty(guid) ||
                    !visited.Add(guid))
                {
                    continue;
                }

                unique.Add(guid);
                containsHovered |= string.Equals(
                    guid,
                    hoveredFolderGuid,
                    StringComparison.Ordinal);
            }

            return containsHovered && unique.Count > 1
                ? unique
                : new[] { hoveredFolderGuid };
        }
    }
}
