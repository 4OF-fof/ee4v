using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ee4v.AssetManager.Infrastructure.Files
{
    internal static class AssetArchivePathUtility
    {
        public static string ResolveIgnoredRootFolder(string archivePath, IEnumerable<string> entryPaths)
        {
            var archiveName = Path.GetFileNameWithoutExtension(archivePath) ?? string.Empty;
            archiveName = archiveName.Trim();
            if (string.IsNullOrEmpty(archiveName))
            {
                return string.Empty;
            }

            var paths = (entryPaths ?? Array.Empty<string>())
                .Select(path => NormalizeEntryPath(path, preserveTrailingSlash: true))
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();
            if (paths.Length == 0)
            {
                return string.Empty;
            }

            var prefix = archiveName + "/";
            var hasChildEntry = false;
            var hasExplicitRootDirectory = false;
            for (var i = 0; i < paths.Length; i++)
            {
                var comparablePath = paths[i].TrimEnd('/');
                if (string.Equals(comparablePath, archiveName, StringComparison.OrdinalIgnoreCase))
                {
                    hasExplicitRootDirectory |= paths[i].EndsWith("/", StringComparison.Ordinal);
                    continue;
                }

                if (!paths[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                hasChildEntry = true;
            }

            return hasChildEntry || hasExplicitRootDirectory ? archiveName : string.Empty;
        }

        public static string ToDisplayPath(string entryPath, string ignoredRootFolder)
        {
            var path = NormalizeEntryPath(entryPath, preserveTrailingSlash: true);
            var root = NormalizeEntryPath(ignoredRootFolder);
            if (string.IsNullOrEmpty(root))
            {
                return path;
            }

            var comparablePath = path.TrimEnd('/');
            if (string.Equals(comparablePath, root, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var prefix = root + "/";
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(prefix.Length)
                : path;
        }

        public static string ToArchiveEntryPath(string displayPath, string ignoredRootFolder)
        {
            var path = NormalizeEntryPath(displayPath);
            var root = NormalizeEntryPath(ignoredRootFolder);
            return string.IsNullOrEmpty(root) ? path : root + "/" + path;
        }

        private static string NormalizeEntryPath(string path, bool preserveTrailingSlash = false)
        {
            var normalized = (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
            if (!preserveTrailingSlash)
            {
                normalized = normalized.TrimEnd('/');
            }

            return normalized;
        }
    }
}
