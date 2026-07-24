using UnityEngine;

namespace Ee4v.FolderContentOverlay
{
    internal interface IFolderContentIconProvider
    {
        Texture Get(string folderPath);
    }
}
