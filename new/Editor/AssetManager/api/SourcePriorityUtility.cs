using System;
using System.Collections.Generic;
using Ee4v.Core.Settings;

namespace Ee4v.AssetManager.Api
{
    internal static class SourcePriorityUtility
    {
        public static IReadOnlyList<AssetSourceType> GetPriority()
        {
            return Parse(SettingApi.Get(AssetManagerDefinitions.SourcePriority));
        }

        public static IReadOnlyList<AssetSourceType> Parse(string value)
        {
            var results = new List<AssetSourceType>();
            var seen = new HashSet<AssetSourceType>();
            var parts = (value ?? string.Empty).Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                AssetSourceType sourceType;
                if (TryParseSourceType(parts[i], out sourceType) && seen.Add(sourceType))
                {
                    results.Add(sourceType);
                }
            }

            return results;
        }

        public static string ToDbValue(AssetSourceType sourceType)
        {
            if (sourceType == AssetSourceType.Ee4v) return "ee4v";
            if (sourceType == AssetSourceType.Eagle) return "eagle";
            return "blm";
        }

        private static bool TryParseSourceType(string value, out AssetSourceType sourceType)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.Equals(normalized, "ee4v", StringComparison.OrdinalIgnoreCase))
            {
                sourceType = AssetSourceType.Ee4v;
                return true;
            }

            if (string.Equals(normalized, "eagle", StringComparison.OrdinalIgnoreCase))
            {
                sourceType = AssetSourceType.Eagle;
                return true;
            }

            if (string.Equals(normalized, "blm", StringComparison.OrdinalIgnoreCase))
            {
                sourceType = AssetSourceType.Blm;
                return true;
            }

            sourceType = AssetSourceType.Blm;
            return false;
        }
    }
}
