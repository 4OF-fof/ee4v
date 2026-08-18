using UnityEngine;

namespace Ee4v.ItemStyle
{
    public sealed class ItemStyleValue
    {
        public ItemStyleValue(
            string identity,
            bool hasColor,
            Color color,
            string iconGuid)
        {
            Identity = identity ?? string.Empty;
            HasColor = hasColor;
            Color = hasColor ? color : Color.clear;
            IconGuid = iconGuid ?? string.Empty;
        }

        public string Identity { get; }
        public bool HasColor { get; }
        public Color Color { get; }
        public string IconGuid { get; }
        public bool HasIcon => !string.IsNullOrEmpty(IconGuid);
        public bool IsEmpty => !HasColor && !HasIcon;

        public static ItemStyleValue Empty(string identity)
        {
            return new ItemStyleValue(
                identity,
                false,
                Color.clear,
                string.Empty);
        }

        public ItemStyleValue WithColor(Color color)
        {
            var hasColor = color.a > 0f;
            return new ItemStyleValue(
                Identity,
                hasColor,
                color,
                IconGuid);
        }

        public ItemStyleValue WithIcon(string iconGuid)
        {
            return new ItemStyleValue(
                Identity,
                HasColor,
                Color,
                iconGuid);
        }
    }
}
