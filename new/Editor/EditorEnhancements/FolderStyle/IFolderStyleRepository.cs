using System.Collections.Generic;

namespace Ee4v.FolderStyle
{
    internal interface IFolderStyleRepository
    {
        FolderStyleValue Get(string folderGuid);

        void Put(FolderStyleValue style);

        IReadOnlyList<string> GetRecentIconGuids();

        void RecordRecentIcon(
            string iconGuid,
            int maximumCount);

        bool RemoveRecentIcon(string iconGuid);

        void Save();
    }
}
