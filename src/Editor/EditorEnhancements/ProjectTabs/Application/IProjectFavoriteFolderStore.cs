using System;
using System.Collections.Generic;

namespace Ee4v.ProjectTabs
{
    internal interface IProjectFavoriteFolderStore
    {
        event Action Changed;

        bool TryGetAll(
            out IReadOnlyList<ProjectTabLocation> locations);

        bool TryAdd(ProjectTabLocation location);

        bool TryRemove(ProjectTabLocation location);
    }
}
