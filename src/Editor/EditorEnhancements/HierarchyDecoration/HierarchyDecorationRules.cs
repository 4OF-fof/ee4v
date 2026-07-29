using System;
using System.Globalization;

namespace Ee4v.HierarchyDecoration
{
    internal static class HierarchyDecorationRules
    {
        internal const string SeparatorName = "---";
        private const string DuplicateSuffixPrefix = " (";

        public static bool IsSeparator(
            string objectName,
            int componentCount,
            int childCount)
        {
            return string.Equals(
                    objectName,
                    SeparatorName,
                    StringComparison.Ordinal) &&
                componentCount == 1 &&
                childCount == 0;
        }

        public static bool TryNormalizeSeparatorName(
            string objectName,
            int componentCount,
            int childCount,
            out string normalizedName)
        {
            normalizedName = objectName;
            if (componentCount != 1 ||
                childCount != 0 ||
                string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            var prefix =
                SeparatorName + DuplicateSuffixPrefix;
            if (!objectName.StartsWith(
                    prefix,
                    StringComparison.Ordinal) ||
                objectName[objectName.Length - 1] != ')')
            {
                return false;
            }

            var numberText = objectName.Substring(
                prefix.Length,
                objectName.Length - prefix.Length - 1);
            if (!int.TryParse(
                    numberText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var duplicateIndex) ||
                duplicateIndex < 1)
            {
                return false;
            }

            normalizedName = SeparatorName;
            return true;
        }
    }
}
