using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.AssetManager.UI
{
    internal enum FileTreeImportTargetCoverage
    {
        None,
        Partial,
        All
    }

    internal static class FileTreeImportTargetPolicy
    {
        internal static IReadOnlyList<FileTreeImportTargetEntry>
            CreateEntries(
                string assetFileId,
                string relativePath,
                bool isDirectory,
                IEnumerable<IReadOnlyList<
                    FileTreeImportTargetEntry>> childEntries)
        {
            if (childEntries != null)
            {
                return childEntries
                    .Where(entries => entries != null)
                    .SelectMany(entries => entries)
                    .ToArray();
            }

            if (string.IsNullOrWhiteSpace(assetFileId) ||
                isDirectory)
            {
                return Array.Empty<FileTreeImportTargetEntry>();
            }

            return new[]
            {
                new FileTreeImportTargetEntry(
                    assetFileId,
                    NormalizeRelativePath(relativePath))
            };
        }

        internal static FileTreeImportTargetCoverage GetCoverage(
            ISet<string> targetPaths,
            string relativePath,
            IReadOnlyList<FileTreeImportTargetEntry> entries)
        {
            if (targetPaths == null || targetPaths.Count == 0)
            {
                return FileTreeImportTargetCoverage.None;
            }

            if (targetPaths.Contains(
                    NormalizeRelativePath(relativePath)))
            {
                return FileTreeImportTargetCoverage.All;
            }

            if (entries == null || entries.Count == 0)
            {
                return FileTreeImportTargetCoverage.None;
            }

            var matched = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                if (targetPaths.Contains(
                        NormalizeRelativePath(
                            entries[i].RelativePath)))
                {
                    matched++;
                }
            }

            if (matched == 0)
            {
                return FileTreeImportTargetCoverage.None;
            }

            return matched == entries.Count
                ? FileTreeImportTargetCoverage.All
                : FileTreeImportTargetCoverage.Partial;
        }

        internal static string CombineRelativePath(
            string prefix,
            string path)
        {
            prefix = NormalizeRelativePath(prefix);
            path = NormalizeRelativePath(path);
            if (prefix.Length == 0)
            {
                return path;
            }

            if (path.Length == 0)
            {
                return prefix;
            }

            return prefix + "/" + path;
        }

        internal static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .TrimStart('/')
                .TrimEnd('/');
        }
    }
}
