using System.Collections.Generic;

namespace Ee4v.HierarchyStyle
{
    internal interface IHierarchyStyleRepository
    {
        HierarchyStyleValue Get(string objectId);

        void Put(HierarchyStyleValue style);

        IReadOnlyList<string> GetRecentIconGuids();

        void RecordRecentIcon(string iconGuid, int maximumCount);

        bool RemoveRecentIcon(string iconGuid);

        void Save();
    }
}
