using _4OF.ee4v.Core.Setting;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Components.CustomStyle {
    public static class CustomStyleUtility {
        public static bool IsCustomStyleItem(GameObject gameObject) {
            if (gameObject == null) return false;

            if (!string.IsNullOrEmpty(Settings.I.headingPrefix) &&
                gameObject.name.StartsWith(Settings.I.headingPrefix))
                return true;

            if (!string.IsNullOrEmpty(Settings.I.separatorPrefix) &&
                gameObject.name.StartsWith(Settings.I.separatorPrefix))
                return true;

            return false;
        }
    }
}