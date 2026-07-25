using System;
using System.Collections.Generic;

namespace Ee4v.FolderStyle
{
    internal sealed class FolderStyleRecentIconSession
    {
        private readonly List<string> _iconGuids;

        public FolderStyleRecentIconSession(
            IReadOnlyList<string> iconGuids)
        {
            _iconGuids = iconGuids == null
                ? new List<string>()
                : new List<string>(iconGuids);
        }

        public IReadOnlyList<string> IconGuids
        {
            get { return _iconGuids; }
        }

        public void Remove(string iconGuid)
        {
            if (string.IsNullOrEmpty(iconGuid))
            {
                return;
            }

            _iconGuids.RemoveAll(
                guid => string.Equals(
                    guid,
                    iconGuid,
                    StringComparison.Ordinal));
        }
    }
}
