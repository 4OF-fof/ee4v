using UnityEngine;

namespace Ee4v.FolderStyle
{
    internal sealed class FolderStyleValue
    {
        public FolderStyleValue(
            string folderGuid,
            bool hasColor,
            Color color,
            string iconGuid)
        {
            FolderGuid = folderGuid ?? string.Empty;
            HasColor = hasColor;
            Color = color;
            IconGuid = iconGuid ?? string.Empty;
        }

        public string FolderGuid { get; }

        public bool HasColor { get; }

        public Color Color { get; }

        public string IconGuid { get; }

        public bool HasIcon
        {
            get { return !string.IsNullOrEmpty(IconGuid); }
        }

        public bool IsEmpty
        {
            get { return !HasColor && !HasIcon; }
        }

        public static FolderStyleValue Empty(string folderGuid)
        {
            return new FolderStyleValue(
                folderGuid,
                false,
                Color.clear,
                string.Empty);
        }

        public FolderStyleValue WithColor(Color color)
        {
            var hasColor = color.a > 0f;
            return new FolderStyleValue(
                FolderGuid,
                hasColor,
                hasColor ? color : Color.clear,
                IconGuid);
        }

        public FolderStyleValue WithIcon(string iconGuid)
        {
            return new FolderStyleValue(
                FolderGuid,
                HasColor,
                Color,
                iconGuid);
        }
    }
}
