using _4OF.ee4v.Core.Setting;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components.CustomStyle {
    public enum HierarchyItemType {
        Normal,
        Heading,
        Separator
    }

    public static class CustomStyleUtility {
        public static HierarchyItemType GetCustomStyleType(this GameObject gameObject) {
            if (gameObject == null) return HierarchyItemType.Normal;

            var name = gameObject.name;
            var settings = Settings.I;

            if (!string.IsNullOrEmpty(settings.headingPrefix) &&
                name.StartsWith(settings.headingPrefix))
                return HierarchyItemType.Heading;

            if (!string.IsNullOrEmpty(settings.separatorPrefix) &&
                name.StartsWith(settings.separatorPrefix))
                return HierarchyItemType.Separator;

            return HierarchyItemType.Normal;
        }

        public static bool IsCustomStyleItem(this GameObject gameObject) {
            return gameObject.GetCustomStyleType() != HierarchyItemType.Normal;
        }
    }
}