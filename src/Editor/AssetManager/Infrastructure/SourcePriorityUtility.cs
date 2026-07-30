using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Infrastructure
{
    internal static class SourcePriorityUtility
    {
        public static IReadOnlyList<AssetSourceType> GetPriority()
        {
            return Parse(AssetManagerInfrastructureSettings.Current.SourcePriority);
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
