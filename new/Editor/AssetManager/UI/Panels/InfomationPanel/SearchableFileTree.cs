using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Ee4v.AssetManager.Api;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class SearchableFileTree : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-tree";
        private const string RowClassName = "ee4v-asset-manager-file-tree__row";
        private const string RowTitleClassName = "ee4v-asset-manager-file-tree__title";
        private const string RowMetaClassName = "ee4v-asset-manager-file-tree__meta";
        private readonly SearchableTreeView<FileTreeNode> _treeView;
        private readonly FileTreeBuilder _builder;
        private string _itemId;

        public SearchableFileTree()
        {
            AddToClassList(RootClassName);
            _builder = new FileTreeBuilder();
            _treeView = new SearchableTreeView<FileTreeNode>(
                CreateTreeItem,
                BindTreeItem,
                null,
                I18N.Get("assetManager.infomationPanel.fileTree.empty"),
                I18N.Get("assetManager.infomationPanel.fileTree.searchPlaceholder"));
            _treeView.SetViewDataKey("ee4v-asset-manager-infomation-panel-file-tree");
            Add(_treeView);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void SetItemId(string itemId)
        {
            var nextItemId = itemId ?? string.Empty;
            if (string.Equals(_itemId, nextItemId, StringComparison.Ordinal))
            {
                return;
            }

            _itemId = nextItemId;
            Reload();
        }

        public void Clear()
        {
            _itemId = string.Empty;
            _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
            _treeView.SetItems(null);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerApi.Changed -= Reload;
            AssetManagerApi.Changed += Reload;
            Reload();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerApi.Changed -= Reload;
        }

        private void Reload()
        {
            if (string.IsNullOrWhiteSpace(_itemId))
            {
                Clear();
                return;
            }

            try
            {
                var files = AssetManagerApi.GetFiles(_itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
                _treeView.SetItems(_builder.Build(files));
            }
            catch (Exception)
            {
                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.loadFailed"));
                _treeView.SetItems(null);
            }
        }

        private static VisualElement CreateTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList(RowClassName);
            row.Add(UiTextFactory.Create(string.Empty, RowTitleClassName));
            row.Add(UiTextFactory.Create(string.Empty, RowMetaClassName));
            return row;
        }

        private static void BindTreeItem(VisualElement element, FileTreeNode node)
        {
            var title = element.ElementAt(0) as UiTextElement;
            var meta = element.ElementAt(1) as UiTextElement;

            if (title != null)
            {
                title.SetText(node.Name);
            }

            if (meta != null)
            {
                meta.SetText(node.Meta);
                meta.EnableInClassList("ee4v-asset-manager-file-tree__meta--empty", string.IsNullOrWhiteSpace(node.Meta));
            }
        }
    }

    internal sealed class FileTreeNode
    {
        public FileTreeNode(string name, string meta, string path)
        {
            Name = name ?? string.Empty;
            Meta = meta ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public string Name { get; }

        public string Meta { get; }

        public string Path { get; }
    }

    internal sealed class FileTreeBuilder
    {
        private readonly ZipFileTreeReader _zipReader = new ZipFileTreeReader();
        private int _nextId;

        public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> Build(IReadOnlyList<AssetFile> files)
        {
            _nextId = 1;
            var items = new List<SearchableTreeItemData<FileTreeNode>>();
            if (files == null)
            {
                return items;
            }

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file == null)
                {
                    continue;
                }

                items.Add(BuildAssetFileNode(file));
            }

            return items;
        }

        private SearchableTreeItemData<FileTreeNode> BuildAssetFileNode(AssetFile file)
        {
            var resolution = AssetManagerApi.ResolveFilePath(file.Id);
            if (resolution == null || !resolution.Found || string.IsNullOrWhiteSpace(resolution.Path))
            {
                return CreateItem(
                    file.FileName,
                    I18N.Get("assetManager.infomationPanel.fileTree.meta.missing"),
                    file.FileName);
            }

            var path = resolution.Path;
            if (Directory.Exists(path))
            {
                return CreateDirectoryItem(path, Path.GetFileName(path), path);
            }

            if (File.Exists(path) && IsZipFile(file, path))
            {
                return CreateZipItem(file.FileName, path);
            }

            return CreateFileItem(path, string.IsNullOrWhiteSpace(file.FileName) ? Path.GetFileName(path) : file.FileName, path);
        }

        private SearchableTreeItemData<FileTreeNode> CreateDirectoryItem(string path, string name, string searchPath)
        {
            var children = new List<SearchableTreeItemData<FileTreeNode>>();
            try
            {
                foreach (var childDirectory in Directory.EnumerateDirectories(path))
                {
                    children.Add(CreateDirectoryItem(childDirectory, Path.GetFileName(childDirectory), childDirectory));
                }

                foreach (var childFile in Directory.EnumerateFiles(path))
                {
                    if (IsZipPath(childFile))
                    {
                        children.Add(CreateZipItem(Path.GetFileName(childFile), childFile));
                    }
                    else
                    {
                        children.Add(CreateFileItem(childFile, Path.GetFileName(childFile), childFile));
                    }
                }
            }
            catch (Exception)
            {
                children.Add(CreateItem(
                    I18N.Get("assetManager.infomationPanel.fileTree.meta.inaccessible"),
                    string.Empty,
                    searchPath));
            }

            return CreateItem(
                string.IsNullOrWhiteSpace(name) ? path : name,
                I18N.Get("assetManager.infomationPanel.fileTree.meta.folder"),
                searchPath,
                children);
        }

        private SearchableTreeItemData<FileTreeNode> CreateZipItem(string name, string path)
        {
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children;
            try
            {
                children = _zipReader.Read(path, () => _nextId++);
            }
            catch (Exception)
            {
                children = new[]
                {
                    CreateItem(
                        I18N.Get("assetManager.infomationPanel.fileTree.meta.inaccessible"),
                        string.Empty,
                        path)
                };
            }

            return CreateItem(
                string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name,
                I18N.Get("assetManager.infomationPanel.fileTree.meta.zip"),
                path,
                children);
        }

        private SearchableTreeItemData<FileTreeNode> CreateFileItem(string path, string name, string searchPath)
        {
            return CreateItem(
                string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name,
                FormatFileMeta(path),
                searchPath);
        }

        private SearchableTreeItemData<FileTreeNode> CreateItem(
            string name,
            string meta,
            string searchPath,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children = null)
        {
            var node = new FileTreeNode(name, meta, searchPath);
            return new SearchableTreeItemData<FileTreeNode>(
                _nextId++,
                node,
                string.Join(" ", new[] { node.Name, node.Meta, node.Path }),
                children);
        }

        private static bool IsZipFile(AssetFile file, string path)
        {
            return string.Equals(file.Extension, "zip", StringComparison.OrdinalIgnoreCase) || IsZipPath(path);
        }

        private static bool IsZipPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatFileMeta(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Exists)
                {
                    return FormatBytes(info.Length);
                }
            }
            catch (Exception)
            {
            }

            return I18N.Get("assetManager.infomationPanel.fileTree.meta.file");
        }

        internal static string FormatBytes(long bytes)
        {
            if (bytes < 1024L)
            {
                return bytes + " B";
            }

            if (bytes < 1024L * 1024L)
            {
                return (bytes / 1024f).ToString("0.#") + " KB";
            }

            if (bytes < 1024L * 1024L * 1024L)
            {
                return (bytes / 1024f / 1024f).ToString("0.#") + " MB";
            }

            return (bytes / 1024f / 1024f / 1024f).ToString("0.#") + " GB";
        }
    }

    internal sealed class ZipFileTreeReader
    {
        private readonly Dictionary<string, ZipTreeCacheEntry> _cache = new Dictionary<string, ZipTreeCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> Read(string zipPath, Func<int> nextId)
        {
            var fileInfo = new FileInfo(zipPath);
            var cacheKey = zipPath;
            ZipTreeCacheEntry cached;
            if (_cache.TryGetValue(cacheKey, out cached) &&
                cached.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc &&
                cached.Length == fileInfo.Length)
            {
                return cached.CreateTree(nextId);
            }

            var root = new ZipVirtualDirectory(string.Empty, string.Empty);
            using (var stream = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                for (var i = 0; i < archive.Entries.Count; i++)
                {
                    AddEntry(root, archive.Entries[i]);
                }
            }

            var snapshot = new ZipTreeCacheEntry(fileInfo.LastWriteTimeUtc, fileInfo.Length, root.Children);
            _cache[cacheKey] = snapshot;
            return snapshot.CreateTree(nextId);
        }

        private static void AddEntry(ZipVirtualDirectory root, ZipArchiveEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FullName))
            {
                return;
            }

            var normalizedPath = entry.FullName.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return;
            }

            var parts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var isLast = i == parts.Length - 1;
                var childPath = string.IsNullOrEmpty(current.Path) ? parts[i] : current.Path + "/" + parts[i];
                if (!isLast || entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    current = current.GetOrCreateDirectory(parts[i], childPath);
                    continue;
                }

                current.AddFile(parts[i], childPath, entry.Length);
            }
        }

        private sealed class ZipTreeCacheEntry
        {
            private readonly IReadOnlyList<ZipVirtualNode> _children;

            public ZipTreeCacheEntry(DateTime lastWriteTimeUtc, long length, IReadOnlyList<ZipVirtualNode> children)
            {
                LastWriteTimeUtc = lastWriteTimeUtc;
                Length = length;
                _children = children ?? Array.Empty<ZipVirtualNode>();
            }

            public DateTime LastWriteTimeUtc { get; }

            public long Length { get; }

            public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> CreateTree(Func<int> nextId)
            {
                var items = new List<SearchableTreeItemData<FileTreeNode>>(_children.Count);
                for (var i = 0; i < _children.Count; i++)
                {
                    items.Add(_children[i].CreateTreeItem(nextId));
                }

                return items;
            }
        }

        private abstract class ZipVirtualNode
        {
            protected ZipVirtualNode(string name, string path)
            {
                Name = name ?? string.Empty;
                Path = path ?? string.Empty;
            }

            protected string Name { get; }

            public string Path { get; }

            public abstract SearchableTreeItemData<FileTreeNode> CreateTreeItem(Func<int> nextId);

            protected SearchableTreeItemData<FileTreeNode> CreateItem(
                Func<int> nextId,
                string meta,
                IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children = null)
            {
                var node = new FileTreeNode(Name, meta, Path);
                return new SearchableTreeItemData<FileTreeNode>(
                    nextId(),
                    node,
                    string.Join(" ", new[] { node.Name, node.Meta, node.Path }),
                    children);
            }
        }

        private sealed class ZipVirtualDirectory : ZipVirtualNode
        {
            private readonly Dictionary<string, ZipVirtualDirectory> _directories = new Dictionary<string, ZipVirtualDirectory>(StringComparer.OrdinalIgnoreCase);
            private readonly List<ZipVirtualNode> _children = new List<ZipVirtualNode>();

            public ZipVirtualDirectory(string name, string path)
                : base(name, path)
            {
            }

            public IReadOnlyList<ZipVirtualNode> Children
            {
                get { return _children; }
            }

            public ZipVirtualDirectory GetOrCreateDirectory(string name, string path)
            {
                ZipVirtualDirectory directory;
                if (_directories.TryGetValue(path, out directory))
                {
                    return directory;
                }

                directory = new ZipVirtualDirectory(name, path);
                _directories[path] = directory;
                _children.Add(directory);
                return directory;
            }

            public void AddFile(string name, string path, long length)
            {
                _children.Add(new ZipVirtualFile(name, path, length));
            }

            public override SearchableTreeItemData<FileTreeNode> CreateTreeItem(Func<int> nextId)
            {
                var childItems = new List<SearchableTreeItemData<FileTreeNode>>(_children.Count);
                for (var i = 0; i < _children.Count; i++)
                {
                    childItems.Add(_children[i].CreateTreeItem(nextId));
                }

                return CreateItem(nextId, I18N.Get("assetManager.infomationPanel.fileTree.meta.folder"), childItems);
            }
        }

        private sealed class ZipVirtualFile : ZipVirtualNode
        {
            private readonly long _length;

            public ZipVirtualFile(string name, string path, long length)
                : base(name, path)
            {
                _length = length;
            }

            public override SearchableTreeItemData<FileTreeNode> CreateTreeItem(Func<int> nextId)
            {
                return CreateItem(nextId, FileTreeBuilder.FormatBytes(_length));
            }
        }
    }
}
