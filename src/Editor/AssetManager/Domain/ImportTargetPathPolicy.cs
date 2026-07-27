using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Domain
{
    internal enum ImportTargetPathError
    {
        Empty,
        NotRelative
    }

    internal sealed class ImportTargetPathRuleException : Exception
    {
        internal ImportTargetPathRuleException(ImportTargetPathError error)
        {
            Error = error;
        }

        internal ImportTargetPathError Error { get; }
    }

    internal static class ImportTargetPathPolicy
    {
        internal static IReadOnlyList<string> Normalize(IReadOnlyList<string> relativePaths)
        {
            if (relativePaths == null || relativePaths.Count == 0)
            {
                return Array.Empty<string>();
            }

            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < relativePaths.Count; i++)
            {
                var normalizedPath = NormalizeOne(relativePaths[i]);
                if (seen.Add(normalizedPath))
                {
                    results.Add(normalizedPath);
                }
            }

            return results;
        }

        private static string NormalizeOne(string relativePath)
        {
            var normalized = (relativePath ?? string.Empty)
                .Replace('\\', '/')
                .Trim();

            while (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            normalized = normalized.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ImportTargetPathRuleException(ImportTargetPathError.Empty);
            }

            if (normalized.IndexOf('\0') >= 0)
            {
                throw new ImportTargetPathRuleException(ImportTargetPathError.NotRelative);
            }

            var segments = normalized.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "." || segments[i] == "..")
                {
                    throw new ImportTargetPathRuleException(ImportTargetPathError.NotRelative);
                }
            }

            return normalized;
        }
    }
}
