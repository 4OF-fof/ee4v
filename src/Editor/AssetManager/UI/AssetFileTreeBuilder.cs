using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetFileTreeBuilder
    {
        internal static IReadOnlyList<SearchableTreeItemData<FileTreeNode>>
            Build(
                IReadOnlyList<AssetFile> files,
                IReadOnlyDictionary<string, AssetFileAnalysis> analyses,
                CancellationToken cancellationToken)
        {
            var source = files ?? Array.Empty<AssetFile>();
            var usedIds = new HashSet<int>();
            var items = new List<SearchableTreeItemData<FileTreeNode>>(
                source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = source[index];
                AssetFileAnalysis analysis = null;
                analyses?.TryGetValue(file.Id, out analysis);
                items.Add(CreateFileItem(
                    file,
                    analysis,
                    usedIds,
                    cancellationToken));
            }
            return items;
        }

        internal static bool CanAnalyze(AssetFile file)
        {
            var extension = GetExtension(file);
            return string.Equals(
                       extension,
                       "zip",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       extension,
                       "unitypackage",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static SearchableTreeItemData<FileTreeNode> CreateFileItem(
            AssetFile file,
            AssetFileAnalysis analysis,
            ISet<int> usedIds,
            CancellationToken cancellationToken)
        {
            var node = new FileTreeNode(
                file.FileName,
                GetMeta(file),
                file,
                null);
            var children = analysis == null
                ? Array.Empty<SearchableTreeItemData<FileTreeNode>>()
                : BuildEntryItems(
                    file,
                    analysis.Entries,
                    usedIds,
                    cancellationToken);
            return CreateTreeItem(
                CreateKey(file.Id, null),
                node,
                children,
                usedIds,
                cancellationToken);
        }

        private static IReadOnlyList<SearchableTreeItemData<FileTreeNode>>
            BuildEntryItems(
                AssetFile file,
                IReadOnlyList<AssetFileContentEntry> entries,
                ISet<int> usedIds,
                CancellationToken cancellationToken)
        {
            var root = new PathNode(string.Empty, string.Empty);
            foreach (var entry in entries ?? Array.Empty<AssetFileContentEntry>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddEntry(root, entry);
            }
            return root.Children
                .OrderBy(node => node.IsFile)
                .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                .Select(node => CreateEntryItem(
                    file,
                    node,
                    usedIds,
                    cancellationToken))
                .ToArray();
        }

        private static void AddEntry(
            PathNode root,
            AssetFileContentEntry entry)
        {
            var normalized = NormalizePath(entry?.Path);
            if (normalized.Length == 0)
            {
                return;
            }

            var current = root;
            var path = string.Empty;
            foreach (var segment in normalized.Split('/'))
            {
                path = path.Length == 0
                    ? segment
                    : path + "/" + segment;
                current = current.GetOrAdd(segment, path);
            }
            current.Entry = entry;
        }

        private static SearchableTreeItemData<FileTreeNode> CreateEntryItem(
            AssetFile file,
            PathNode pathNode,
            ISet<int> usedIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = pathNode.Entry ?? new AssetFileContentEntry
            {
                Path = pathNode.Path,
                Kind = AssetFileContentEntryKind.Directory,
                AssetGuid = string.Empty
            };
            var node = new FileTreeNode(
                pathNode.Name,
                entry.Kind == AssetFileContentEntryKind.File
                    ? FormatSize(entry.SizeBytes)
                    : string.Empty,
                file,
                entry);
            var children = pathNode.Children
                .OrderBy(child => child.IsFile)
                .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                .Select(child => CreateEntryItem(
                    file,
                    child,
                    usedIds,
                    cancellationToken))
                .ToArray();
            return CreateTreeItem(
                CreateKey(file.Id, pathNode.Path),
                node,
                children,
                usedIds,
                cancellationToken);
        }

        private static SearchableTreeItemData<FileTreeNode> CreateTreeItem(
            string key,
            FileTreeNode node,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children,
            ISet<int> usedIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var searchText = string.Join(
                " ",
                new[]
                {
                    node.Title,
                    node.Meta,
                    node.Entry?.Path,
                    node.Entry?.AssetGuid
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return new SearchableTreeItemData<FileTreeNode>(
                AllocateTreeId(key, usedIds),
                node,
                searchText,
                node.Entry?.Path ?? node.File.FileName,
                children);
        }

        private static int AllocateTreeId(string key, ISet<int> usedIds)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (var index = 0; index < key.Length; index++)
                {
                    hash ^= key[index];
                    hash *= 16777619;
                }

                var id = (int)(hash & 0x7fffffff);
                id = id == 0 ? 1 : id;
                while (!usedIds.Add(id))
                {
                    id = id == int.MaxValue ? 1 : id + 1;
                }
                return id;
            }
        }

        private static string CreateKey(string fileId, string entryPath)
        {
            return (fileId ?? string.Empty) + "|" +
                   NormalizePath(entryPath);
        }

        private static string GetMeta(AssetFile file)
        {
            var extension = GetExtension(file);
            return extension.Length == 0
                ? file.SourceType.ToString()
                : extension.ToUpperInvariant();
        }

        private static string GetExtension(AssetFile file)
        {
            var extension = file?.Extension;
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(file?.FileName);
            }
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(file?.SourcePath);
            }
            return (extension ?? string.Empty).Trim().TrimStart('.');
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private static string FormatSize(long sizeBytes)
        {
            if (sizeBytes < 1024)
            {
                return sizeBytes + " B";
            }
            if (sizeBytes < 1024L * 1024L)
            {
                return (sizeBytes / 1024d).ToString("0.#") + " KB";
            }
            return (sizeBytes / (1024d * 1024d)).ToString("0.#") + " MB";
        }

        private sealed class PathNode
        {
            private readonly Dictionary<string, PathNode> _children =
                new Dictionary<string, PathNode>(
                    StringComparer.OrdinalIgnoreCase);

            internal PathNode(string name, string path)
            {
                Name = name;
                Path = path;
            }

            internal string Name { get; }
            internal string Path { get; }
            internal AssetFileContentEntry Entry { get; set; }
            internal IEnumerable<PathNode> Children => _children.Values;
            internal bool IsFile =>
                Entry?.Kind == AssetFileContentEntryKind.File &&
                _children.Count == 0;

            internal PathNode GetOrAdd(string name, string path)
            {
                if (!_children.TryGetValue(name, out var child))
                {
                    child = new PathNode(name, path);
                    _children.Add(name, child);
                }
                return child;
            }
        }
    }

    internal sealed class FileTreeNode
    {
        internal FileTreeNode(
            string title,
            string meta,
            AssetFile file,
            AssetFileContentEntry entry)
        {
            Title = title ?? string.Empty;
            Meta = meta ?? string.Empty;
            File = file;
            Entry = entry;
        }

        internal string Title { get; }
        internal string Meta { get; }
        internal AssetFile File { get; }
        internal AssetFileContentEntry Entry { get; }
    }
}
