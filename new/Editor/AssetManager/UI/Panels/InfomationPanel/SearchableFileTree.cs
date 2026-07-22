using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Api;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class SearchableFileTree : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-tree";
        private const string RowClassName = "ee4v-asset-manager-file-tree__row";
        private const string RowImportTargetClassName = "ee4v-asset-manager-file-tree__row--import-target";
        private const string RowGroupClassName = "ee4v-asset-manager-file-tree__row--group";
        private const string RowVariantGroupClassName = "ee4v-asset-manager-file-tree__row--variant-group";
        private const string RowVersionGroupClassName = "ee4v-asset-manager-file-tree__row--version-group";
        private const string RowPrimaryFileClassName = "ee4v-asset-manager-file-tree__row--primary-file";
        private const string RowTitleClassName = "ee4v-asset-manager-file-tree__title";
        private const string RowMetaClassName = "ee4v-asset-manager-file-tree__meta";
        private const string RowGroupMetaClassName = "ee4v-asset-manager-file-tree__meta--group";
        private readonly SearchableTreeView<FileTreeNode> _treeView;
        private CancellationTokenSource _reloadCancellation;
        private int _reloadVersion;
        private bool _isAttached;
        private string _itemId;
        private string _fileId;

        public SearchableFileTree()
        {
            AddToClassList(RootClassName);
            _treeView = new SearchableTreeView<FileTreeNode>(
                CreateTreeItem,
                BindTreeItem,
                null,
                I18N.Get("assetManager.infomationPanel.fileTree.empty"),
                I18N.Get("assetManager.infomationPanel.fileTree.searchPlaceholder"),
                SelectionType.Multiple,
                OnTreeContextClick,
                node => node == null || !node.IsGroup);
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
            if (_isAttached)
            {
                Reload();
            }
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
            if (_isAttached)
            {
                Reload();
            }
        }

        public void ClearTree()
        {
            CancelPendingReload();
            _itemId = string.Empty;
            _fileId = string.Empty;
            _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
            _treeView.SetItems(null);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            _isAttached = true;
            AssetManagerApi.Changed -= OnAssetManagerChanged;
            AssetManagerApi.Changed += OnAssetManagerChanged;
            AssetManagerApi.FileTreeChanged -= OnFileTreeChanged;
            AssetManagerApi.FileTreeChanged += OnFileTreeChanged;
            Reload(preserveTreeState: true);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            _isAttached = false;
            CancelPendingReload();
            AssetManagerApi.Changed -= OnAssetManagerChanged;
            AssetManagerApi.FileTreeChanged -= OnFileTreeChanged;
        }

        private void OnAssetManagerChanged()
        {
            FileTreeMemoryCache.Clear();
            Reload(preserveTreeState: true);
        }

        private void OnFileTreeChanged()
        {
            FileTreeMemoryCache.Clear();
            Reload(preserveTreeState: true);
        }

        private void Reload(bool preserveTreeState = false)
        {
            CancelPendingReload();
            if (string.IsNullOrWhiteSpace(_itemId))
            {
                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
                _treeView.SetItems(null);
                return;
            }

            SettingApi.Preload(SettingScope.User);
            var itemId = _itemId;
            var fileId = _fileId;
            var cacheDirectory = AssetFileTreeCache.ResolveCacheDirectory();
            var inaccessibleText = I18N.Get("assetManager.infomationPanel.fileTree.meta.inaccessible");
            var zipText = I18N.Get("assetManager.infomationPanel.fileTree.meta.zip");
            var memoryCacheKey = new FileTreeMemoryCacheKey(
                itemId,
                fileId,
                cacheDirectory,
                inaccessibleText,
                zipText);
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> cachedItems;
            if (FileTreeMemoryCache.TryGet(memoryCacheKey, out cachedItems))
            {
                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
                _treeView.SetItems(cachedItems, preserveTreeState);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _reloadCancellation = cancellation;
            var reloadVersion = ++_reloadVersion;

            _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.loading"));
            _treeView.SetItems(null);

            Task.Run(
                () => LoadTree(itemId, fileId, cacheDirectory, inaccessibleText, zipText, cancellation.Token),
                cancellation.Token).ContinueWith(task =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (reloadVersion != _reloadVersion || cancellation.IsCancellationRequested || task.IsCanceled)
                    {
                        cancellation.Dispose();
                        return;
                    }

                    if (ReferenceEquals(_reloadCancellation, cancellation))
                    {
                        _reloadCancellation = null;
                    }

                    cancellation.Dispose();
                    if (task.IsFaulted)
                    {
                        if (task.Exception != null)
                        {
                            Debug.LogException(task.Exception.GetBaseException());
                        }

                        _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.loadFailed"));
                        _treeView.SetItems(null);
                        return;
                    }

                    FileTreeMemoryCache.Set(memoryCacheKey, task.Result);
                    _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
                    _treeView.SetItems(task.Result, preserveTreeState);
                };
            });
        }

        private static IReadOnlyList<SearchableTreeItemData<FileTreeNode>> LoadTree(
            string itemId,
            string fileId,
            string cacheDirectory,
            string inaccessibleText,
            string zipText,
            CancellationToken cancellationToken)
        {
            var files = LoadFiles(itemId, fileId);
            cancellationToken.ThrowIfCancellationRequested();
            var variants = string.IsNullOrWhiteSpace(fileId)
                ? AssetManagerApi.GetVariantGroups(itemId)
                : Array.Empty<AssetVariantGroup>();
            var versions = string.IsNullOrWhiteSpace(fileId)
                ? AssetManagerApi.GetVersionGroups(itemId)
                : Array.Empty<AssetVersionGroup>();
            var importTargetsByFileId = new Dictionary<string, IReadOnlyList<AssetFileImportTarget>>(StringComparer.Ordinal);
            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                importTargetsByFileId[files[i].Id] = AssetManagerApi.GetFileImportTargets(files[i].Id);
            }

            var builder = new FileTreeBuilder(cacheDirectory, inaccessibleText, zipText, cancellationToken);
            return builder.Build(files, variants, versions, importTargetsByFileId);
        }

        private static IReadOnlyList<AssetFile> LoadFiles(string itemId, string fileId)
        {
            var files = AssetManagerApi.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return files;
            }

            var selectedFiles = new List<AssetFile>(1);
            for (var i = 0; i < files.Count; i++)
            {
                if (string.Equals(files[i].Id, fileId, StringComparison.Ordinal))
                {
                    selectedFiles.Add(files[i]);
                    break;
                }
            }

            return selectedFiles;
        }

        private void CancelPendingReload()
        {
            _reloadVersion++;
            if (_reloadCancellation == null)
            {
                return;
            }

            _reloadCancellation.Cancel();
            _reloadCancellation = null;
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
            element.EnableInClassList(RowGroupClassName, node.IsGroup);
            element.EnableInClassList(RowVariantGroupClassName, node.GroupKind == FileTreeGroupKind.Variant);
            element.EnableInClassList(RowVersionGroupClassName, node.GroupKind == FileTreeGroupKind.Version);
            element.EnableInClassList(RowPrimaryFileClassName, node.IsPrimaryFile);
            var title = element.ElementAt(0) as UiTextElement;
            var meta = element.ElementAt(1) as UiTextElement;

            if (title != null)
            {
                title.SetText(node.Name);
                title.tooltip = string.Empty;
            }

            if (meta != null)
            {
                var metaText = ResolveMetaText(node);
                meta.SetText(metaText);
                meta.tooltip = string.Empty;
                meta.EnableInClassList(RowGroupMetaClassName, node.IsGroup);
                meta.EnableInClassList("ee4v-asset-manager-file-tree__meta--empty", string.IsNullOrWhiteSpace(metaText));
            }
        }

        private static string ResolveMetaText(FileTreeNode node)
        {
            if (node.GroupKind == FileTreeGroupKind.Variant)
            {
                return I18N.Get("assetManager.infomationPanel.fileTree.meta.variantGroup");
            }

            if (node.GroupKind == FileTreeGroupKind.Version)
            {
                return I18N.Get("assetManager.infomationPanel.fileTree.meta.versionGroup");
            }

            return node.Meta;
        }

        private void OnTreeContextClick(VisualElement target, FileTreeNode item, IReadOnlyList<FileTreeNode> selectedItems, Vector2 panelPosition)
        {
            if (item == null || item.IsGroup)
            {
                return;
            }

            var selected = selectedItems ?? Array.Empty<FileTreeNode>();
            var importTargetSelection = selected.Where(node => node != null && !node.IsGroup && node.CanSetImportTarget).ToArray();

            var menuItems = new List<ContextMenuItemState>();
            if (item.CanSetAsVersionGroupPrimary)
            {
                menuItems.Add(new ContextMenuItemState(
                    "set-version-group-primary",
                    I18N.Get("assetManager.infomationPanel.fileTree.context.setPrimaryFile"),
                    () => AssetManagerApi.SetVersionGroupPrimaryFile(item.VersionGroupId, item.AssetFileId)));
            }

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
                            RelativePath = entry.RelativePath
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
                            RelativePath = target.RelativePath
                        })
                        .ToArray());
            }
        }
    }

    internal readonly struct FileTreeMemoryCacheKey : IEquatable<FileTreeMemoryCacheKey>
    {
        public FileTreeMemoryCacheKey(
            string itemId,
            string fileId,
            string cacheDirectory,
            string inaccessibleText,
            string zipText)
        {
            ItemId = itemId ?? string.Empty;
            FileId = fileId ?? string.Empty;
            CacheDirectory = cacheDirectory ?? string.Empty;
            InaccessibleText = inaccessibleText ?? string.Empty;
            ZipText = zipText ?? string.Empty;
        }

        private string ItemId { get; }

        private string FileId { get; }

        private string CacheDirectory { get; }

        private string InaccessibleText { get; }

        private string ZipText { get; }

        public bool Equals(FileTreeMemoryCacheKey other)
        {
            return string.Equals(ItemId, other.ItemId, StringComparison.Ordinal) &&
                   string.Equals(FileId, other.FileId, StringComparison.Ordinal) &&
                   string.Equals(CacheDirectory, other.CacheDirectory, StringComparison.Ordinal) &&
                   string.Equals(InaccessibleText, other.InaccessibleText, StringComparison.Ordinal) &&
                   string.Equals(ZipText, other.ZipText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FileTreeMemoryCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(ItemId);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(FileId);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(CacheDirectory);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(InaccessibleText);
                return (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(ZipText);
            }
        }
    }

    internal static class FileTreeMemoryCache
    {
        private const int MaxEntryCount = 64;
        private static readonly object Gate = new object();
        private static readonly Dictionary<FileTreeMemoryCacheKey, IReadOnlyList<SearchableTreeItemData<FileTreeNode>>> Entries =
            new Dictionary<FileTreeMemoryCacheKey, IReadOnlyList<SearchableTreeItemData<FileTreeNode>>>();
        private static readonly Queue<FileTreeMemoryCacheKey> InsertionOrder = new Queue<FileTreeMemoryCacheKey>();

        public static bool TryGet(
            FileTreeMemoryCacheKey key,
            out IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items)
        {
            lock (Gate)
            {
                return Entries.TryGetValue(key, out items);
            }
        }

        public static void Set(
            FileTreeMemoryCacheKey key,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items)
        {
            lock (Gate)
            {
                if (Entries.ContainsKey(key))
                {
                    Entries[key] = items ?? Array.Empty<SearchableTreeItemData<FileTreeNode>>();
                    return;
                }

                while (Entries.Count >= MaxEntryCount && InsertionOrder.Count > 0)
                {
                    Entries.Remove(InsertionOrder.Dequeue());
                }

                Entries[key] = items ?? Array.Empty<SearchableTreeItemData<FileTreeNode>>();
                InsertionOrder.Enqueue(key);
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Entries.Clear();
                InsertionOrder.Clear();
            }
        }
    }

    internal enum FileTreeGroupKind
    {
        None,
        Variant,
        Version
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
            bool isGroup = false,
            FileTreeGroupKind groupKind = FileTreeGroupKind.None,
            bool isAssetFileRoot = false,
            string assetFileId = null,
            string versionGroupId = null,
            bool isPrimaryFile = false,
            IReadOnlyList<FileTreeImportTargetEntry> importTargetEntries = null)
        {
            Name = name ?? string.Empty;
            Meta = meta ?? string.Empty;
            Path = path ?? string.Empty;
            IsImportTarget = isImportTarget;
            HasAnyImportTarget = hasAnyImportTarget;
            RelativePath = NormalizeRelativePath(relativePath);
            IsGroup = isGroup;
            GroupKind = groupKind;
            IsAssetFileRoot = isAssetFileRoot;
            AssetFileId = assetFileId ?? string.Empty;
            VersionGroupId = versionGroupId ?? string.Empty;
            IsPrimaryFile = isPrimaryFile;
            ImportTargetEntries = importTargetEntries ?? Array.Empty<FileTreeImportTargetEntry>();
        }

        public string Name { get; }

        public string Meta { get; }

        public string Path { get; }

        public bool IsImportTarget { get; }

        public bool HasAnyImportTarget { get; }

        public string RelativePath { get; }

        public bool IsGroup { get; }

        public FileTreeGroupKind GroupKind { get; }

        public bool IsAssetFileRoot { get; }

        public string AssetFileId { get; }

        public string VersionGroupId { get; }

        public bool IsPrimaryFile { get; }

        public IReadOnlyList<FileTreeImportTargetEntry> ImportTargetEntries { get; }

        public bool CanSetImportTarget
        {
            get { return !IsAssetFileRoot && ImportTargetEntries.Count > 0; }
        }

        public bool CanSetAsVersionGroupPrimary
        {
            get
            {
                return IsAssetFileRoot &&
                       !IsPrimaryFile &&
                       !string.IsNullOrWhiteSpace(AssetFileId) &&
                       !string.IsNullOrWhiteSpace(VersionGroupId);
            }
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');
        }
    }

    internal sealed class FileTreeImportTargetEntry
    {
        public FileTreeImportTargetEntry(string fileId, string relativePath)
        {
            FileId = fileId ?? string.Empty;
            RelativePath = (relativePath ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');
        }

        public string FileId { get; }

        public string RelativePath { get; }
    }

    internal sealed class FileTreeBuilder
    {
        private readonly ZipFileTreeReader _zipReader;
        private readonly string _inaccessibleText;
        private readonly string _zipText;
        private readonly CancellationToken _cancellationToken;
        private int _nextId;
        private IReadOnlyDictionary<string, IReadOnlyList<AssetFileImportTarget>> _importTargetsByFileId;

        public FileTreeBuilder(
            string cacheDirectory,
            string inaccessibleText,
            string zipText,
            CancellationToken cancellationToken)
        {
            _zipReader = new ZipFileTreeReader(cacheDirectory, cancellationToken);
            _inaccessibleText = inaccessibleText ?? string.Empty;
            _zipText = zipText ?? string.Empty;
            _cancellationToken = cancellationToken;
        }

        public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> Build(
            IReadOnlyList<AssetFile> files,
            IReadOnlyList<AssetVariantGroup> variantGroups,
            IReadOnlyList<AssetVersionGroup> versionGroups,
            IReadOnlyDictionary<string, IReadOnlyList<AssetFileImportTarget>> importTargetsByFileId)
        {
            _nextId = 1;
            _importTargetsByFileId = importTargetsByFileId ?? new Dictionary<string, IReadOnlyList<AssetFileImportTarget>>();
            var items = new List<SearchableTreeItemData<FileTreeNode>>();
            if (files == null)
            {
                return items;
            }

            variantGroups = variantGroups ?? Array.Empty<AssetVariantGroup>();
            versionGroups = versionGroups ?? Array.Empty<AssetVersionGroup>();
            var consumedFileIds = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < variantGroups.Count; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var groupItem = BuildVariantGroupNode(variantGroups[i], versionGroups, files, consumedFileIds);
                if (groupItem != null)
                {
                    items.Add(groupItem);
                }
            }

            for (var i = 0; i < versionGroups.Count; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(versionGroups[i].VariantGroupId))
                {
                    continue;
                }

                var groupItem = BuildVersionGroupNode(versionGroups[i], files, consumedFileIds);
                if (groupItem != null)
                {
                    items.Add(groupItem);
                }
            }

            for (var i = 0; i < files.Count; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var file = files[i];
                if (file != null && !consumedFileIds.Contains(file.Id))
                {
                    items.Add(BuildAssetFileNode(file));
                }
            }

            return items;
        }

        private SearchableTreeItemData<FileTreeNode> BuildVariantGroupNode(
            AssetVariantGroup variantGroup,
            IReadOnlyList<AssetVersionGroup> versionGroups,
            IReadOnlyList<AssetFile> files,
            HashSet<string> consumedFileIds)
        {
            if (variantGroup == null)
            {
                return null;
            }

            var children = new List<SearchableTreeItemData<FileTreeNode>>();
            for (var i = 0; i < versionGroups.Count; i++)
            {
                if (!string.Equals(versionGroups[i].VariantGroupId, variantGroup.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                var versionItem = BuildVersionGroupNode(versionGroups[i], files, consumedFileIds);
                if (versionItem != null)
                {
                    children.Add(versionItem);
                }
            }

            AddFiles(children, files, consumedFileIds, file => string.Equals(file.VariantGroupId, variantGroup.Id, StringComparison.Ordinal));
            return children.Count == 0
                ? null
                : CreateGroupItem(variantGroup.Name, FileTreeGroupKind.Variant, children);
        }

        private SearchableTreeItemData<FileTreeNode> BuildVersionGroupNode(
            AssetVersionGroup versionGroup,
            IReadOnlyList<AssetFile> files,
            HashSet<string> consumedFileIds)
        {
            if (versionGroup == null)
            {
                return null;
            }

            var children = new List<SearchableTreeItemData<FileTreeNode>>();
            AddFiles(
                children,
                files,
                consumedFileIds,
                file => string.Equals(file.VersionGroupId, versionGroup.Id, StringComparison.Ordinal),
                versionGroup.PrimaryFileId);
            return children.Count == 0
                ? null
                : CreateGroupItem(versionGroup.Name, FileTreeGroupKind.Version, children);
        }

        private void AddFiles(
            List<SearchableTreeItemData<FileTreeNode>> items,
            IReadOnlyList<AssetFile> files,
            HashSet<string> consumedFileIds,
            Func<AssetFile, bool> predicate,
            string primaryFileId = null)
        {
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file == null || consumedFileIds.Contains(file.Id) || !predicate(file))
                {
                    continue;
                }

                items.Add(BuildAssetFileNode(file, string.Equals(file.Id, primaryFileId, StringComparison.Ordinal)));
                consumedFileIds.Add(file.Id);
            }
        }

        private SearchableTreeItemData<FileTreeNode> CreateGroupItem(
            string name,
            FileTreeGroupKind groupKind,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children)
        {
            var node = new FileTreeNode(
                name,
                string.Empty,
                name,
                isGroup: true,
                groupKind: groupKind);
            return new SearchableTreeItemData<FileTreeNode>(
                _nextId++,
                node,
                node.Name,
                node.Name,
                children);
        }

        private SearchableTreeItemData<FileTreeNode> BuildAssetFileNode(AssetFile file, bool isPrimaryFile = false)
        {
            var resolution = AssetManagerApi.ResolveFilePath(file.Id);
            if (resolution == null || !resolution.Found || string.IsNullOrWhiteSpace(resolution.Path))
            {
                return CreateItem(
                    file.FileName,
                    string.Empty,
                    file.FileName,
                    file,
                    isAssetFileRoot: true,
                    isPrimaryFile: isPrimaryFile);
            }

            var path = resolution.Path;
            if (Directory.Exists(path))
            {
                return CreateDirectoryItem(path, Path.GetFileName(path), path, file, path, string.Empty, isAssetFileRoot: true, isPrimaryFile: isPrimaryFile);
            }

            if (File.Exists(path) && IsZipFile(file, path))
            {
                return CreateZipItem(file.FileName, path, file, isAssetFileRoot: true, isPrimaryFile: isPrimaryFile);
            }

            return CreateFileItem(path, string.IsNullOrWhiteSpace(file.FileName) ? Path.GetFileName(path) : file.FileName, path, file, isAssetFileRoot: true, isPrimaryFile: isPrimaryFile);
        }

        private SearchableTreeItemData<FileTreeNode> CreateDirectoryItem(
            string path,
            string name,
            string searchPath,
            AssetFile assetFile = null,
            string rootPath = null,
            string relativePath = null,
            bool isAssetFileRoot = false,
            bool isPrimaryFile = false)
        {
            var children = new List<SearchableTreeItemData<FileTreeNode>>();
            try
            {
                foreach (var childDirectory in Directory.EnumerateDirectories(path))
                {
                    _cancellationToken.ThrowIfCancellationRequested();
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
                    _cancellationToken.ThrowIfCancellationRequested();
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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                children.Add(CreateItem(
                    _inaccessibleText,
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
                children,
                isAssetFileRoot,
                isPrimaryFile);
        }

        private SearchableTreeItemData<FileTreeNode> CreateZipItem(
            string name,
            string path,
            AssetFile assetFile = null,
            string relativePath = null,
            bool isAssetFileRoot = false,
            bool isPrimaryFile = false)
        {
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children;
            try
            {
                children = _zipReader.Read(path, () => _nextId++, assetFile, GetTargetPathSet(assetFile), relativePath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                children = new[]
                {
                    CreateItem(
                        _inaccessibleText,
                        string.Empty,
                        path)
                };
            }

            return CreateItem(
                string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name,
                _zipText,
                path,
                assetFile,
                relativePath,
                false,
                children,
                isAssetFileRoot,
                isPrimaryFile);
        }

        private SearchableTreeItemData<FileTreeNode> CreateFileItem(
            string path,
            string name,
            string searchPath,
            AssetFile assetFile = null,
            string relativePath = null,
            bool isAssetFileRoot = false,
            bool isPrimaryFile = false)
        {
            return CreateItem(
                string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name,
                string.Empty,
                searchPath,
                assetFile,
                relativePath,
                false,
                isAssetFileRoot: isAssetFileRoot,
                isPrimaryFile: isPrimaryFile);
        }

        private SearchableTreeItemData<FileTreeNode> CreateItem(
            string name,
            string meta,
            string searchPath,
            AssetFile assetFile = null,
            string relativePath = null,
            bool isDirectory = false,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children = null,
            bool isAssetFileRoot = false,
            bool isPrimaryFile = false)
        {
            var targetPath = NormalizeRelativePath(relativePath);
            var importTargetEntries = CreateImportTargetEntries(assetFile, targetPath, isDirectory, children);
            var targetPaths = GetTargetPathSet(assetFile);
            var node = new FileTreeNode(
                name,
                meta,
                searchPath,
                !isAssetFileRoot && IsImportTarget(targetPaths, targetPath, importTargetEntries),
                !isAssetFileRoot && HasAnyImportTarget(targetPaths, targetPath, importTargetEntries),
                targetPath,
                isDirectory,
                isAssetFileRoot: isAssetFileRoot,
                assetFileId: assetFile == null ? null : assetFile.Id,
                versionGroupId: assetFile == null ? null : assetFile.VersionGroupId,
                isPrimaryFile: isPrimaryFile,
                importTargetEntries: importTargetEntries);
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
            if (children != null)
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

            if (assetFile == null || isDirectory)
            {
                return entries;
            }

            entries.Add(new FileTreeImportTargetEntry(assetFile.Id, relativePath));
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
        private readonly string _cacheDirectory;
        private readonly CancellationToken _cancellationToken;

        public ZipFileTreeReader(string cacheDirectory, CancellationToken cancellationToken)
        {
            _cacheDirectory = cacheDirectory;
            _cancellationToken = cancellationToken;
        }

        public IReadOnlyList<SearchableTreeItemData<FileTreeNode>> Read(
            string zipPath,
            Func<int> nextId,
            AssetFile assetFile,
            HashSet<string> importTargetPaths,
            string relativePathPrefix)
        {
            var root = new ZipVirtualDirectory(string.Empty, string.Empty);
            var entries = AssetFileTreeCache.ReadZipEntries(_cacheDirectory, zipPath, _cancellationToken);
            for (var i = 0; i < entries.Count; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                AddEntry(root, entries[i]);
            }

            var snapshot = new ZipTreeCacheEntry(root.Children);
            return snapshot.CreateTree(nextId, assetFile, importTargetPaths, relativePathPrefix);
        }

        private static void AddEntry(ZipVirtualDirectory root, AssetFileTreeArchiveEntry entry)
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

            public ZipTreeCacheEntry(IReadOnlyList<ZipVirtualNode> children)
            {
                _children = children ?? Array.Empty<ZipVirtualNode>();
            }

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
                    importTargetEntries: importTargetEntries);
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
                if (children != null)
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

                if (assetFile == null || isDirectory)
                {
                    return entries;
                }

                entries.Add(new FileTreeImportTargetEntry(assetFile.Id, relativePath));
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
