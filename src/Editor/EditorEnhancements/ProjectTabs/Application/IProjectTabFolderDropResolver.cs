using System.Collections.Generic;

namespace Ee4v.ProjectTabs
{
    internal interface IProjectTabFolderDropResolver
    {
        IReadOnlyList<ProjectTabLocation> Resolve(
            IReadOnlyList<string> paths);
    }
}
