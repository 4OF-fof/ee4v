using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Domain
{
    internal static class ImportedAssetGuidPolicy
    {
        private const int UnityGuidLength = 32;

        internal static IReadOnlyList<string> Normalize(
            IEnumerable<string> assetGuids)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (assetGuids == null)
            {
                return result;
            }

            foreach (var assetGuid in assetGuids)
            {
                var normalized = NormalizeOne(assetGuid);
                if (normalized != null && seen.Add(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static string NormalizeOne(string assetGuid)
        {
            var value = (assetGuid ?? string.Empty).Trim();
            if (value.Length != UnityGuidLength)
            {
                return null;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                var isHex =
                    character >= '0' && character <= '9' ||
                    character >= 'a' && character <= 'f' ||
                    character >= 'A' && character <= 'F';
                if (!isHex)
                {
                    return null;
                }
            }

            return value.ToLowerInvariant();
        }
    }
}
