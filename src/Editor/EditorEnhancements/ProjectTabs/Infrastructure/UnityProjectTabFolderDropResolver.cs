using System;
using System.Collections.Generic;
using UnityEditor;

namespace Ee4v.ProjectTabs
{
    internal sealed class UnityProjectTabFolderDropResolver
        : IProjectTabFolderDropResolver
    {
        public IReadOnlyList<ProjectTabLocation> Resolve(
            IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return Array.Empty<ProjectTabLocation>();
            }

            var locations = new List<ProjectTabLocation>();
            var resolvedGuids = new HashSet<string>(
                StringComparer.Ordinal);
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.IsNullOrWhiteSpace(path) ||
                    !AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid) ||
                    !resolvedGuids.Add(guid))
                {
                    continue;
                }

                locations.Add(new ProjectTabLocation(guid, path));
            }

            return locations;
        }
    }
}
