namespace _4OF.ee4v.ProjectExtension.ItemStyle {
    public static class FolderStyleService {
        public static int IndexOfGuid(string guid) {
            if (string.IsNullOrEmpty(guid)) return -1;
            for (var i = 0; i < FolderStyleList.instance.Contents.Count; i++)
                if (FolderStyleList.instance.Contents[i].guid == guid)
                    return i;
            return -1;
        }
    }
}