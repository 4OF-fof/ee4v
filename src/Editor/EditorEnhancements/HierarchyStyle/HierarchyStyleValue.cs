using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleValue
    {
        public HierarchyStyleValue(
            string objectId,
            bool hasBackgroundColor,
            Color backgroundColor,
            string iconGuid)
        {
            ObjectId = objectId ?? string.Empty;
            HasBackgroundColor = hasBackgroundColor;
            BackgroundColor = backgroundColor;
            IconGuid = iconGuid ?? string.Empty;
        }

        public string ObjectId { get; }

        public bool HasBackgroundColor { get; }

        public Color BackgroundColor { get; }

        public string IconGuid { get; }

        public bool HasIcon
        {
            get { return !string.IsNullOrEmpty(IconGuid); }
        }

        public bool IsEmpty
        {
            get { return !HasBackgroundColor && !HasIcon; }
        }

        public static HierarchyStyleValue Empty(string objectId)
        {
            return new HierarchyStyleValue(
                objectId,
                false,
                Color.clear,
                string.Empty);
        }

        public HierarchyStyleValue WithBackgroundColor(Color color)
        {
            var hasColor = color.a > 0f;
            return new HierarchyStyleValue(
                ObjectId,
                hasColor,
                hasColor ? color : Color.clear,
                IconGuid);
        }

        public HierarchyStyleValue WithIcon(string iconGuid)
        {
            return new HierarchyStyleValue(
                ObjectId,
                HasBackgroundColor,
                BackgroundColor,
                iconGuid);
        }
    }
}
