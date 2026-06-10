using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Ee4v.AssetManager.Api;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class SearchableFileTree : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-tree";
        private const string RowClassName = "ee4v-asset-manager-file-tree__row";
        private const string RowImportTargetClassName = "ee4v-asset-manager-file-tree__row--import-target";
        private const string RowTitleClassName = "ee4v-asset-manager-file-tree__title";
        private const string RowMetaClassName = "ee4v-asset-manager-file-tree__meta";
        private readonly SearchableTreeView<FileTreeNode> _treeView;
        private readonly FileTreeBuilder _builder;
        private string _itemId;
        private string _fileId;

        public SearchableFileTree()
        {
            AddToClassList(RootClassName);
            _builder = new FileTreeBuilder();
            _treeView = new SearchableTreeView<FileTreeNode>(
                CreateTreeItem,
                BindTreeItem,
                null,
                I18N.Get("assetManager.infomationPanel.fileTree.empty"),
                I18N.Get("assetManager.infomationPanel.fileTree.searchPlaceholder"),
                SelectionType.Multiple,
                OnTreeContextClick);
            _treeView.SetViewDataKey("ee4v-asset-manager-infomation-panel-file-tree");
            Add(_treeView);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void SetItemId(string itemId)
        {
            var nextItemId = itemId ?? string.Empty;
            if (string.Equals(_itemId, nextItemId, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(_fileId))
            {
                return;
            }

            _itemId = nextItemId;
            _fileId = string.Empty;
            Reload();
        }

        public void SetFileId(string itemId, string fileId)
        {
            var nextItemId = itemId ?? string.Empty;
            var nextFileId = fileId ?? string.Empty;
            if (string.Equals(_itemId, nextItemId, StringComparison.Ordinal) &&
                string.Equals(_fileId, nextFileId, StringComparison.Ordinal))
            {
                return;
            }

            _itemId = nextItemId;
            _fileId = nextFileId;
            Reload();
        }

        public void ClearTree()
        {
            _itemId = string.Empty;
            _fileId = string.Empty;
            _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
            _treeView.SetItems(null);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerApi.Changed -= OnAssetManagerChanged;
            AssetManagerApi.Changed += OnAssetManagerChanged;
            AssetManagerApi.FileTreeChanged -= OnFileTreeChanged;
            AssetManagerApi.FileTreeChanged += OnFileTreeChanged;
            Reload(preserveTreeState: true);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerApi.Changed -= OnAssetManagerChanged;
            AssetManagerApi.FileTreeChanged -= OnFileTreeChanged;
        }

        private void OnAssetManagerChanged()
        {
            Reload(preserveTreeState: true);
        }

        private void OnFileTreeChanged()
        {
            Reload(preserveTreeState: true);
        }

        private void Reload(bool preserveTreeState = false)
        {
            if (string.IsNullOrWhiteSpace(_itemId))
            {
                ClearTree();
                return;
            }

            try
            {
                var files = LoadFiles();
                var importTargetsByFileId = new Dictionary<string, IReadOnlyList<AssetFileImportTarget>>(StringComparer.Ordinal);
                for (var i = 0; i < files.Count; i++)
                {
                    importTargetsByFileId[files[i].Id] = AssetManagerApi.GetFileImportTargets(files[i].Id);
                }

                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
                _treeView.SetItems(_builder.Build(files, importTargetsByFileId), preserveTreeState);
            }
            catch (Exception)
            {
                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.loadFailed"));
                _treeView.SetItems(null);
            }
        }

        private IReadOnlyList<AssetFile> LoadFiles()
        {
            var files = AssetManagerApi.GetFiles(_itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            if (string.IsNullOrWhiteSpace(_fileId))
            {
                return files;
            }

            var selectedFiles = new List<AssetFile>(1);
            for (var i = 0; i < files.Count; i++)
            {
                if (string.Equals(files[i].Id, _fileId, StringComparison.Ordinal))
                {
                    selectedFiles.Add(files[i]);
                    break;
                }
            }

            return selectedFiles;
        }

        private static VisualElement CreateTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList(RowClassName);
            var title = UiTextFactory.Create(string.Empty, RowTitleClassName);
            title.SetWhiteSpace(WhiteSpace.NoWrap);
            title.pickingMode = PickingMode.Ignore;
            var meta = UiTextFactory.Create(string.Empty, RowMetaClassName);
            meta.SetWhiteSpace(WhiteSpace.NoWrap);
            meta.pickingMode = PickingMode.Ignore;
            row.Add(title);
            row.Add(meta);
            return row;
        }

        private static void BindTreeItem(VisualElement element, FileTreeNode node)
        {
            element.EnableInClassList(RowImportTargetClassName, node.IsImportTarget);
            var title = element.ElementAt(0) as UiTextElement;
            var meta = element.ElementAt(1) as UiTextElement;

            if (title != null)
            {
                title.SetText(node.Name);
                title.tooltip = string.Empty;
            }

            if (meta != null)
            {
                meta.SetText(node.Meta);
                meta.tooltip = string.Empty;
                meta.EnableInClassList("ee4v-asset-manager-file-tree__meta--empty", string.IsNullOrWhiteSpace(node.Meta));
            }
        }

        private void OnTreeContextClick(VisualElement target, FileTreeNode item, IReadOnlyList<FileTreeNode> selectedItems, Vector2 panelPosition)
        {
            var selected = selectedItems ?? Array.Empty<FileTreeNode>();
            var importTargetSelection = selected.Where(node => node != null && node.CanSetImportTarget).ToArray();

            var menuItems = new List<ContextMenuItemState>();
            if (importTargetSelection.Length > 0)
            {
                if (importTargetSelection.All(node => node.IsImportTarget))
                {
                    menuItems.Add(new ContextMenuItemState(
                        "unmark-import-target",
                        I18N.Get("assetManager.infomationPanel.fileTree.context.unmarkImportTarget"),
                        () => SetImportTargetSelection(importTargetSelection, false)));
                }
                else
                {
                    menuItems.Add(new ContextMenuItemState(
                        "mark-import-target",
                        I18N.Get("assetManager.infomationPanel.fileTree.context.markImportTarget"),
                        () => SetImportTargetSelection(importTargetSelection, true)));
                }
            }

            if (menuItems.Count == 0)
            {
                return;
            }

            var menu = new ContextMenuState(menuItems);
            ContextMenuWindow.Show(target, panelPosition, menu);
        }

        private void SetImportTargetSelection(IReadOnlyList<FileTreeNode> selectedNodes, bool isImportTarget)
        {
            var grouped = selectedNodes
                .Where(node => node != null && node.CanSetImportTarget)
                .SelectMany(node => node.ImportTargetEntries)
                .GroupBy(entry => entry.FileId, StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < grouped.Length; i++)
            {
                var fileId = grouped[i].Key;
                var targets = AssetManagerApi.GetFileImportTargets(fileId)
                    .ToDictionary(target => target.RelativePath ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                foreach (var entry in grouped[i])
                {
                    if (isImportTarget)
                    {
                        targets[entry.RelativePath] = new AssetFileImportTarget
                        {
                            FileId = fileId,
                            RelativePath = entry.RelativePath,
                            IsDirectory = entry.IsDirectory
                        };
                    }
                    else
                    {
                        targets.Remove(entry.RelativePath);
                    }
                }

                AssetManagerApi.SetFileImportTargets(
                    fileId,
                    targets.Values
                        .OrderBy(target => target.RelativePath, StringComparer.OrdinalIgnoreCase)
                        .Select(target => new AssetFileImportTargetRequest
                        {
                            RelativePath = target.RelativePath,
                            IsDirectory = target.IsDirectory
                        })
                        .ToArray());
            }
        }
    }

    internal sealed class FileTreeNode
    {
        public FileTreeNode(
            string name,
            string meta,
            string path,
            bool isImportTarget = false,
            bool hasAnyImportTarget = false,
            string relativePath = null,
            bool isDirectory = false,
            IReadOnlyList<FileTreeImportTargetEntry> importTargetEntries = null)
        {
            Name = name ?? string.Empty;
            Meta = meta ?? string.Empty;
            Path = path ?? string.Empty;
            IsImportTarget = isImportTarget;
            HasAnyImportTarget = hasAnyImportTarget;
            RelativePath = NormalizeRelativePath(relativePath);
            IsDirectory = isDirectory;
            ImportTargetEntries = importTargetEntries ?? Array.Empty<FileTreeImportTargetEntry>();
        }

        public string Name { get; }

        public string Meta { get; }

        public string Path { get; }

        public bool IsImportTarget { get; }

        public bool HasAnyImportTarget { get; }

        public string RelativePath { get; }

        public bool IsDirectory { get; }

        public IReadOnlyList<FileTreeImportTargetEntry> ImportTargetEntries { get; }

        public bool CanSetImportTarget
        {
            get { return ImportTargetEntries.Count > 0; }
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');
        }
    }

    internal sealed class FileTreeImportTargetEntry
    {
        public FileTreeImportTargetEntry(string fileId, string relativePath, bool isDirectory)
        {
            FileId = fileId ?? string.Empty;
            RelativePath = (relativePath ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');
            IsDirectory = isDirectory;
        }

        public string FileId { get; }

        public string RelativePath { get; }

        public bool IsDirectory { get; }
    }

    internal sealed class FileTreeBuilder
    {
        private readonly ZipFileTreeReader _zipReader = new ZipFileTreeReader();
        private int _nextId;
        private IReadOnlyDictionary<string, IReadOnlyList<AssetFileImportTarget>> _importTargetsByFileId;

        public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> Build(
            IReadOnlyList<AssetFile> files,
            IReadOnlyDictionary<string, IReadOnlyList<AssetFileImportTarget>> importTargetsByFileId)
        {
            _nextId = 1;
            _importTargetsByFileId = importTargetsByFileId ?? new Dictionary<string, IReadOnlyList<AssetFileImportTarget>>();
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
                    string.Empty,
                    file.FileName);
            }

            var path = resolution.Path;
            if (Directory.Exists(path))
            {
                return CreateDirectoryItem(path, Path.GetFileName(path), path, file, path, string.Empty);
            }

            if (File.Exists(path) && IsZipFile(file, path))
            {
                return CreateZipItem(file.FileName, path, file);
            }

            return CreateFileItem(path, string.IsNullOrWhiteSpace(file.FileName) ? Path.GetFileName(path) : file.FileName, path, file);
        }

        private SearchableTreeItemData<FileTreeNode> CreateDirectoryItem(
            string path,
            string name,
            string searchPath,
            AssetFile assetFile = null,
            string rootPath = null,
            string relativePath = null)
        {
            var children = new List<SearchableTreeItemData<FileTreeNode>>();
            try
            {
                foreach (var childDirectory in Directory.EnumerateDirectories(path))
                {
                    children.Add(CreateDirectoryItem(
                        childDirectory,
                        Path.GetFileName(childDirectory),
                        childDirectory,
                        assetFile,
                        rootPath,
                        GetRelativePath(rootPath, childDirectory)));
                }

                foreach (var childFile in Directory.EnumerateFiles(path))
                {
                    var childRelativePath = GetRelativePath(rootPath, childFile);
                    if (IsZipPath(childFile))
                    {
                        children.Add(CreateZipItem(Path.GetFileName(childFile), childFile, assetFile, childRelativePath));
                    }
                    else
                    {
                        children.Add(CreateFileItem(childFile, Path.GetFileName(childFile), childFile, assetFile, childRelativePath));
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
                string.Empty,
                searchPath,
                assetFile,
                relativePath,
                true,
                children);
        }

        private SearchableTreeItemData<FileTreeNode> CreateZipItem(string name, string path, AssetFile assetFile = null, string relativePath = null)
        {
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children;
            try
            {
                children = _zipReader.Read(path, () => _nextId++, assetFile, GetTargetPathSet(assetFile), relativePath);
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
                assetFile,
                relativePath,
                false,
                children);
        }

        private SearchableTreeItemData<FileTreeNode> CreateFileItem(string path, string name, string searchPath, AssetFile assetFile = null, string relativePath = null)
        {
            return CreateItem(
                string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name,
                string.Empty,
                searchPath,
                assetFile,
                relativePath,
                false);
        }

        private SearchableTreeItemData<FileTreeNode> CreateItem(
            string name,
            string meta,
            string searchPath,
            AssetFile assetFile = null,
            string relativePath = null,
            bool isDirectory = false,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children = null)
        {
            var targetPath = NormalizeRelativePath(relativePath);
            var importTargetEntries = CreateImportTargetEntries(assetFile, targetPath, isDirectory, children);
            var targetPaths = GetTargetPathSet(assetFile);
            var node = new FileTreeNode(
                name,
                meta,
                searchPath,
                IsImportTarget(targetPaths, targetPath, importTargetEntries),
                HasAnyImportTarget(targetPaths, targetPath, importTargetEntries),
                targetPath,
                isDirectory,
                importTargetEntries);
            return new SearchableTreeItemData<FileTreeNode>(
                _nextId++,
                node,
                string.Join(" ", new[] { node.Name, node.Meta, node.Path }),
                node.Name,
                children);
        }

        private IReadOnlyList<FileTreeImportTargetEntry> CreateImportTargetEntries(
            AssetFile assetFile,
            string relativePath,
            bool isDirectory,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children)
        {
            var entries = new List<FileTreeImportTargetEntry>();
            if (children != null && children.Count > 0)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var childEntries = children[i].Data.ImportTargetEntries;
                    for (var j = 0; j < childEntries.Count; j++)
                    {
                        entries.Add(childEntries[j]);
                    }
                }

                return entries;
            }

            if (assetFile == null)
            {
                return entries;
            }

            entries.Add(new FileTreeImportTargetEntry(assetFile.Id, relativePath, isDirectory));
            return entries;
        }

        private static bool IsImportTarget(
            HashSet<string> targetPaths,
            string relativePath,
            IReadOnlyList<FileTreeImportTargetEntry> importTargetEntries)
        {
            if (targetPaths.Contains(NormalizeRelativePath(relativePath)))
            {
                return true;
            }

            if (importTargetEntries == null || importTargetEntries.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < importTargetEntries.Count; i++)
            {
                if (!targetPaths.Contains(NormalizeRelativePath(importTargetEntries[i].RelativePath)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasAnyImportTarget(
            HashSet<string> targetPaths,
            string relativePath,
            IReadOnlyList<FileTreeImportTargetEntry> importTargetEntries)
        {
            if (targetPaths.Contains(NormalizeRelativePath(relativePath)))
            {
                return true;
            }

            if (importTargetEntries == null || importTargetEntries.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < importTargetEntries.Count; i++)
            {
                if (targetPaths.Contains(NormalizeRelativePath(importTargetEntries[i].RelativePath)))
                {
                    return true;
                }
            }

            return false;
        }

        private HashSet<string> GetTargetPathSet(AssetFile assetFile)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (assetFile == null)
            {
                return set;
            }

            IReadOnlyList<AssetFileImportTarget> targets;
            if (!_importTargetsByFileId.TryGetValue(assetFile.Id, out targets) || targets == null)
            {
                return set;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                set.Add(NormalizeRelativePath(targets[i].RelativePath));
            }

            return set;
        }

        private static bool IsZipFile(AssetFile file, string path)
        {
            return string.Equals(file.Extension, "zip", StringComparison.OrdinalIgnoreCase) || IsZipPath(path);
        }

        private static bool IsZipPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRelativePath(string rootPath, string path)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return NormalizeRelativePath(Path.GetRelativePath(rootPath, path));
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');
        }
    }

    internal sealed class ZipFileTreeReader
    {
        private readonly Dictionary<string, ZipTreeCacheEntry> _cache = new Dictionary<string, ZipTreeCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> Read(
            string zipPath,
            Func<int> nextId,
            AssetFile assetFile,
            HashSet<string> importTargetPaths,
            string relativePathPrefix)
        {
            var fileInfo = new FileInfo(zipPath);
            var cacheKey = zipPath;
            ZipTreeCacheEntry cached;
            if (_cache.TryGetValue(cacheKey, out cached) &&
                cached.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc &&
                cached.Length == fileInfo.Length)
            {
                return cached.CreateTree(nextId, assetFile, importTargetPaths, relativePathPrefix);
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
            return snapshot.CreateTree(nextId, assetFile, importTargetPaths, relativePathPrefix);
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

            public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> CreateTree(
                Func<int> nextId,
                AssetFile assetFile,
                HashSet<string> importTargetPaths,
                string relativePathPrefix)
            {
                var items = new List<SearchableTreeItemData<FileTreeNode>>(_children.Count);
                for (var i = 0; i < _children.Count; i++)
                {
                    items.Add(_children[i].CreateTreeItem(nextId, assetFile, importTargetPaths, relativePathPrefix));
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

            public abstract SearchableTreeItemData<FileTreeNode> CreateTreeItem(
                Func<int> nextId,
                AssetFile assetFile,
                HashSet<string> importTargetPaths,
                string relativePathPrefix);

            protected SearchableTreeItemData<FileTreeNode> CreateItem(
                Func<int> nextId,
                string meta,
                AssetFile assetFile,
                bool isDirectory,
                HashSet<string> importTargetPaths,
                string relativePathPrefix,
                IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children = null)
            {
                var targetPath = CombineRelativePath(relativePathPrefix, Path);
                var importTargetEntries = CreateImportTargetEntries(assetFile, targetPath, isDirectory, children);
                var node = new FileTreeNode(
                    Name,
                    meta,
                    targetPath,
                    IsImportTarget(importTargetPaths, targetPath, importTargetEntries),
                    HasAnyImportTarget(importTargetPaths, targetPath, importTargetEntries),
                    targetPath,
                    isDirectory,
                    importTargetEntries);
                return new SearchableTreeItemData<FileTreeNode>(
                    nextId(),
                    node,
                    string.Join(" ", new[] { node.Name, node.Meta, node.Path }),
                    node.Name,
                    children);
            }

            private static IReadOnlyList<FileTreeImportTargetEntry> CreateImportTargetEntries(
                AssetFile assetFile,
                string relativePath,
                bool isDirectory,
                IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children)
            {
                var entries = new List<FileTreeImportTargetEntry>();
                if (children != null && children.Count > 0)
                {
                    for (var i = 0; i < children.Count; i++)
                    {
                        var childEntries = children[i].Data.ImportTargetEntries;
                        for (var j = 0; j < childEntries.Count; j++)
                        {
                            entries.Add(childEntries[j]);
                        }
                    }

                    return entries;
                }

                if (assetFile == null)
                {
                    return entries;
                }

                entries.Add(new FileTreeImportTargetEntry(assetFile.Id, relativePath, isDirectory));
                return entries;
            }

            private static bool IsImportTarget(
                HashSet<string> importTargetPaths,
                string relativePath,
                IReadOnlyList<FileTreeImportTargetEntry> importTargetEntries)
            {
                if (importTargetPaths == null || importTargetPaths.Count == 0)
                {
                    return false;
                }

                if (importTargetPaths.Contains(relativePath))
                {
                    return true;
                }

                if (importTargetEntries == null || importTargetEntries.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < importTargetEntries.Count; i++)
                {
                    if (!importTargetPaths.Contains(importTargetEntries[i].RelativePath))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool HasAnyImportTarget(
                HashSet<string> importTargetPaths,
                string relativePath,
                IReadOnlyList<FileTreeImportTargetEntry> importTargetEntries)
            {
                if (importTargetPaths == null || importTargetPaths.Count == 0)
                {
                    return false;
                }

                if (importTargetPaths.Contains(relativePath))
                {
                    return true;
                }

                if (importTargetEntries == null || importTargetEntries.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < importTargetEntries.Count; i++)
                {
                    if (importTargetPaths.Contains(importTargetEntries[i].RelativePath))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string CombineRelativePath(string prefix, string path)
            {
                prefix = (prefix ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');
                path = (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');
                if (string.IsNullOrEmpty(prefix))
                {
                    return path;
                }

                return string.IsNullOrEmpty(path) ? prefix : prefix + "/" + path;
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

            public override SearchableTreeItemData<FileTreeNode> CreateTreeItem(
                Func<int> nextId,
                AssetFile assetFile,
                HashSet<string> importTargetPaths,
                string relativePathPrefix)
            {
                var childItems = new List<SearchableTreeItemData<FileTreeNode>>(_children.Count);
                for (var i = 0; i < _children.Count; i++)
                {
                    childItems.Add(_children[i].CreateTreeItem(nextId, assetFile, importTargetPaths, relativePathPrefix));
                }

                return CreateItem(nextId, string.Empty, assetFile, true, importTargetPaths, relativePathPrefix, childItems);
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

            public override SearchableTreeItemData<FileTreeNode> CreateTreeItem(
                Func<int> nextId,
                AssetFile assetFile,
                HashSet<string> importTargetPaths,
                string relativePathPrefix)
            {
                return CreateItem(nextId, string.Empty, assetFile, false, importTargetPaths, relativePathPrefix);
            }
        }
    }
}
