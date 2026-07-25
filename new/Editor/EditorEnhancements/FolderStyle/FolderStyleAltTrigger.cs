using System;

namespace Ee4v.FolderStyle
{
    internal sealed class FolderStyleAltTrigger
    {
        private string _triggeredFolderGuid;

        public bool TryActivate(
            string folderGuid,
            bool altPressed,
            bool pointerInside)
        {
            if (!altPressed)
            {
                _triggeredFolderGuid = null;
                return false;
            }

            if (!pointerInside ||
                string.IsNullOrEmpty(folderGuid) ||
                string.Equals(
                    _triggeredFolderGuid,
                    folderGuid,
                    StringComparison.Ordinal))
            {
                return false;
            }

            _triggeredFolderGuid = folderGuid;
            return true;
        }
    }
}
