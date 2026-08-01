using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class SearchableFileTree : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-file-tree";
        internal const string RowClassName = "ee4v-asset-manager-file-tree__row";
        private const string RowImportTargetClassName = "ee4v-asset-manager-file-tree__row--import-target";
        private const string RowGroupClassName = "ee4v-asset-manager-file-tree__row--group";
        private const string RowVariantGroupClassName = "ee4v-asset-manager-file-tree__row--variant-group";
        private const string RowVersionGroupClassName = "ee4v-asset-manager-file-tree__row--version-group";
        private const string RowPrimaryFileClassName = "ee4v-asset-manager-file-tree__row--primary-file";
        internal const string RowTitleClassName = "ee4v-asset-manager-file-tree__title";
        internal const string RowMetaClassName = "ee4v-asset-manager-file-tree__meta";
        internal const string RowEmptyMetaClassName = "ee4v-asset-manager-file-tree__meta--empty";
        private const string RowGroupMetaClassName = "ee4v-asset-manager-file-tree__meta--group";
        private const int MaximumCachedImagePreviews = 24;
        private readonly IAssetManager _assetManager;
        private readonly IAssetManagerUiPreferences _preferences;
        private readonly IAssetArchiveReader _archiveReader;
        private readonly IAssetFileSystemReader _fileSystemReader;
        private readonly IAssetManagerUiScheduler _scheduler;
        private readonly SearchableTreeView<FileTreeNode> _treeView;
        private readonly Dictionary<string, Texture2D> _imagePreviewCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _reloadCancellation;
        private CancellationTokenSource _imagePreviewCancellation;
        private ImageTooltipWindow _imageTooltipWindow;
        private VisualElement _hoveredImageRow;
        private FileTreeNode _hoveredImageNode;
        private Vector2 _hoveredPanelPosition;
        private int _imagePreviewVersion;
        private int _reloadVersion;
        private bool _isAttached;
        private string _itemId;
        private string _fileId;
        private FileTreeGroupKind _groupKind;
        private string _groupId;

        public SearchableFileTree(
            IAssetManager assetManager = null,
            IAssetManagerUiPreferences preferences = null,
            IAssetArchiveReader archiveReader = null,
            IAssetFileSystemReader fileSystemReader = null,
            IAssetManagerUiScheduler scheduler = null)
        {
            _assetManager = assetManager ?? AssetManagerUiDependencies.AssetManager;
            _preferences = preferences ?? AssetManagerUiDependencies.Preferences;
            _archiveReader = archiveReader ?? AssetManagerUiDependencies.ArchiveReader;
            _fileSystemReader = fileSystemReader ??
                                AssetManagerUiDependencies.FileSystemReader;
            _scheduler = scheduler ?? AssetManagerUiDependencies.Scheduler;
            AddToClassList(RootClassName);
            var searchTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.search.tooltip");
            var clearTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.clear.tooltip");
            _treeView = new SearchableTreeView<FileTreeNode>(
                CreateTreeItem,
                BindTreeItem,
                null,
                I18N.Get("assetManager.infomationPanel.fileTree.empty"),
                I18N.Get("assetManager.infomationPanel.fileTree.searchPlaceholder"),
                SelectionType.Multiple,
                OnTreeContextClick,
                node => node == null || !node.IsGroup || node.GroupKind == FileTreeGroupKind.Version,
                null,
                searchTooltip,
                clearTooltip,
                searchIconState:
                IconState.FromFluentIcon(
                    UiFluentIcon.Search,
                    UiSizeTokens.Size14,
                    searchTooltip),
                clearIconState:
                IconState.FromFluentIcon(
                    UiFluentIcon.Dismiss,
                    UiSizeTokens.Size10,
                    clearTooltip));
            _treeView.SetViewDataKey("ee4v-asset-manager-infomation-panel-file-tree");
            Add(_treeView);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void SetItemId(string itemId)
        {
            var nextItemId = itemId ?? string.Empty;
            if (string.Equals(_itemId, nextItemId, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(_fileId) &&
                _groupKind == FileTreeGroupKind.None &&
                string.IsNullOrWhiteSpace(_groupId))
            {
                return;
            }

            _itemId = nextItemId;
            _fileId = string.Empty;
            _groupKind = FileTreeGroupKind.None;
            _groupId = string.Empty;
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
                string.Equals(_fileId, nextFileId, StringComparison.Ordinal) &&
                _groupKind == FileTreeGroupKind.None &&
                string.IsNullOrWhiteSpace(_groupId))
            {
                return;
            }

            _itemId = nextItemId;
            _fileId = nextFileId;
            _groupKind = FileTreeGroupKind.None;
            _groupId = string.Empty;
            if (_isAttached)
            {
                Reload();
            }
        }

        public void SetGroupId(
            string itemId,
            FileTreeGroupKind groupKind,
            string groupId)
        {
            var nextItemId = itemId ?? string.Empty;
            var nextGroupId = groupId ?? string.Empty;
            if (string.Equals(_itemId, nextItemId, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(_fileId) &&
                _groupKind == groupKind &&
                string.Equals(_groupId, nextGroupId, StringComparison.Ordinal))
            {
                return;
            }

            _itemId = nextItemId;
            _fileId = string.Empty;
            _groupKind = groupKind;
            _groupId = nextGroupId;
            if (_isAttached)
            {
                Reload();
            }
        }

        public void ClearTree()
        {
            HideImageTooltip();
            CancelPendingReload();
            _itemId = string.Empty;
            _fileId = string.Empty;
            _groupKind = FileTreeGroupKind.None;
            _groupId = string.Empty;
            _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
            _treeView.SetItems(null);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            _isAttached = true;
            _assetManager.Changed -= OnAssetManagerChanged;
            _assetManager.Changed += OnAssetManagerChanged;
            _preferences.Changed -= OnSettingChanged;
            _preferences.Changed += OnSettingChanged;
            Reload(preserveTreeState: true);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            _isAttached = false;
            HideImageTooltip();
            ClearImagePreviewCache();
            CancelPendingReload();
            _assetManager.Changed -= OnAssetManagerChanged;
            _preferences.Changed -= OnSettingChanged;
        }

        private void OnAssetManagerChanged(AssetManagerChange change)
        {
            if (change == null)
            {
                return;
            }

            _scheduler.RunOnMainThread(() =>
                ApplyAssetManagerChange(change));
        }

        private void ApplyAssetManagerChange(
            AssetManagerChange change)
        {
            if (!_isAttached || change == null)
            {
                return;
            }

            switch (change.Kind)
            {
                case AssetManagerChangeKind.Catalog:
                    HideImageTooltip();
                    ClearImagePreviewCache();
                    FileTreeMemoryCache.Clear();
                    Reload(preserveTreeState: true);
                    break;
                case AssetManagerChangeKind.FileImportTargets:
                    FileTreeMemoryCache.SetImportTargets(change.SubjectId, change.ImportTargets);
                    _treeView.RefreshItems();
                    break;
                case AssetManagerChangeKind.VersionGroupPrimaryFile:
                    FileTreeMemoryCache.SetVersionGroupPrimaryFile(
                        change.SubjectId,
                        change.RelatedId);
                    _treeView.RefreshItems();
                    break;
            }
        }

        private void OnSettingChanged(AssetManagerUiPreference preference)
        {
            if (preference != AssetManagerUiPreference.ShowFileTreeImageTooltip)
            {
                return;
            }

            HideImageTooltip();
            _treeView.RefreshItems();
        }

        private void Reload(bool preserveTreeState = false)
        {
            HideImageTooltip();
            CancelPendingReload();
            if (string.IsNullOrWhiteSpace(_itemId) &&
                string.IsNullOrWhiteSpace(_fileId))
            {
                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
                _treeView.SetItems(null);
                return;
            }

            _preferences.Preload();
            var itemId = _itemId;
            var fileId = _fileId;
            var groupKind = _groupKind;
            var groupId = _groupId;
            var cacheDirectory = _archiveReader.CacheDirectory;
            var inaccessibleText = I18N.Get("assetManager.infomationPanel.fileTree.meta.inaccessible");
            var zipText = I18N.Get("assetManager.infomationPanel.fileTree.meta.zip");
            var memoryCacheKey = new FileTreeMemoryCacheKey(
                itemId,
                fileId,
                groupKind,
                groupId,
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

            _scheduler.RunInBackground(
                token => LoadTree(
                    itemId,
                    fileId,
                    groupKind,
                    groupId,
                    cacheDirectory,
                    inaccessibleText,
                    zipText,
                    token),
                cancellation.Token,
                result =>
            {
                if (reloadVersion != _reloadVersion || cancellation.IsCancellationRequested || result.Canceled)
                {
                    cancellation.Dispose();
                    return;
                }

                if (ReferenceEquals(_reloadCancellation, cancellation))
                {
                    _reloadCancellation = null;
                }

                cancellation.Dispose();
                if (result.Error != null)
                {
                    Debug.LogException(result.Error);
                    _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.loadFailed"));
                    _treeView.SetItems(null);
                    return;
                }

                FileTreeMemoryCache.Set(memoryCacheKey, result.Value);
                _treeView.SetEmptyText(I18N.Get("assetManager.infomationPanel.fileTree.empty"));
                _treeView.SetItems(result.Value, preserveTreeState);
            });
        }

        private IReadOnlyList<SearchableTreeItemData<FileTreeNode>> LoadTree(
            string itemId,
            string fileId,
            FileTreeGroupKind groupKind,
            string groupId,
            string cacheDirectory,
            string inaccessibleText,
            string zipText,
            CancellationToken cancellationToken)
        {
            var files = LoadFiles(itemId, fileId);
            cancellationToken.ThrowIfCancellationRequested();
            var variants =
                !string.IsNullOrWhiteSpace(itemId) &&
                string.IsNullOrWhiteSpace(fileId)
                ? _assetManager.GetVariantGroups(itemId)
                : Array.Empty<AssetVariantGroup>();
            var versions =
                !string.IsNullOrWhiteSpace(itemId) &&
                string.IsNullOrWhiteSpace(fileId)
                ? _assetManager.GetVersionGroups(itemId)
                : Array.Empty<AssetVersionGroup>();
            var importTargetsByFileId = new Dictionary<string, IReadOnlyList<AssetFileImportTarget>>(StringComparer.Ordinal);
            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                importTargetsByFileId[files[i].Id] = _assetManager.GetFileImportTargets(files[i].Id);
            }

            var builder = new FileTreeBuilder(
                _assetManager,
                _archiveReader,
                _fileSystemReader,
                inaccessibleText,
                zipText,
                cancellationToken);
            var items = builder.Build(
                files,
                variants,
                versions,
                importTargetsByFileId);
            if (groupKind == FileTreeGroupKind.None ||
                string.IsNullOrWhiteSpace(groupId))
            {
                return items;
            }

            var groupItem = FindGroupItem(
                items,
                groupKind,
                groupId);
            return groupItem == null
                ? Array.Empty<SearchableTreeItemData<FileTreeNode>>()
                : new[] { groupItem };
        }

        private static SearchableTreeItemData<FileTreeNode> FindGroupItem(
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items,
            FileTreeGroupKind groupKind,
            string groupId)
        {
            if (items == null)
            {
                return null;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.Data != null &&
                    item.Data.GroupKind == groupKind &&
                    string.Equals(
                        item.Data.GroupId,
                        groupId,
                        StringComparison.Ordinal))
                {
                    return item;
                }

                var childMatch = FindGroupItem(
                    item.Children,
                    groupKind,
                    groupId);
                if (childMatch != null)
                {
                    return childMatch;
                }
            }

            return null;
        }

        private IReadOnlyList<AssetFile> LoadFiles(string itemId, string fileId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return string.IsNullOrWhiteSpace(fileId)
                    ? Array.Empty<AssetFile>()
                    : _assetManager.GetUnassignedFiles(
                        new AssetFileQuery
                        {
                            FileId = fileId,
                            Lifecycle =
                                AssetFileLifecycle.Active
                        });
            }

            var files = _assetManager.GetFiles(itemId, new AssetFileQuery { Lifecycle = AssetFileLifecycle.Active });
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

        private VisualElement CreateTreeItem()
        {
            var row = CreateRow();
            row.RegisterCallback<PointerEnterEvent>(OnImageRowPointerEnter);
            row.RegisterCallback<PointerMoveEvent>(OnImageRowPointerMove);
            row.RegisterCallback<PointerLeaveEvent>(OnImageRowPointerLeave);
            return row;
        }

        internal static VisualElement CreateRow()
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

        private void BindTreeItem(VisualElement element, FileTreeNode node)
        {
            if (ReferenceEquals(element, _hoveredImageRow) && !ReferenceEquals(node, _hoveredImageNode))
            {
                HideImageTooltip();
            }

            element.EnableInClassList(RowImportTargetClassName, node.IsImportTarget);
            element.EnableInClassList(RowGroupClassName, node.IsGroup);
            element.EnableInClassList(RowVariantGroupClassName, node.GroupKind == FileTreeGroupKind.Variant);
            element.EnableInClassList(RowVersionGroupClassName, node.GroupKind == FileTreeGroupKind.Version);
            element.EnableInClassList(RowPrimaryFileClassName, node.IsPrimaryFile);
            var useImageTooltip = node.ImageSource != null && _preferences.ShowFileTreeImageTooltip;
            element.tooltip = useImageTooltip ? string.Empty : node.Name;
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
                meta.EnableInClassList(RowEmptyMetaClassName, string.IsNullOrWhiteSpace(metaText));
            }
        }

        private void OnImageRowPointerEnter(PointerEnterEvent evt)
        {
            var row = evt.currentTarget as VisualElement;
            var node = ResolveBoundNode(row);
            if (row == null ||
                node == null ||
                node.ImageSource == null ||
                !_preferences.ShowFileTreeImageTooltip)
            {
                return;
            }

            HideImageTooltip();
            _hoveredImageRow = row;
            _hoveredImageNode = node;
            _hoveredPanelPosition = row.LocalToWorld(evt.localPosition);

            Texture2D cachedTexture;
            if (_imagePreviewCache.TryGetValue(node.ImageSource.CacheKey, out cachedTexture) && cachedTexture != null)
            {
                ShowImageTooltip(row, node, cachedTexture);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _imagePreviewCancellation = cancellation;
            var previewVersion = ++_imagePreviewVersion;
            var source = node.ImageSource;
            _scheduler.RunInBackground(
                token => FileTreeImagePreviewLoader.Load(
                    source,
                    _fileSystemReader,
                    token),
                cancellation.Token,
                result =>
            {
                if (ReferenceEquals(_imagePreviewCancellation, cancellation))
                {
                    _imagePreviewCancellation = null;
                }

                if (previewVersion != _imagePreviewVersion ||
                    cancellation.IsCancellationRequested ||
                    result.Canceled ||
                    result.Error != null ||
                    result.Value == null ||
                    !ReferenceEquals(_hoveredImageRow, row) ||
                    !ReferenceEquals(_hoveredImageNode, node) ||
                    row.panel == null)
                {
                    cancellation.Dispose();
                    return;
                }

                cancellation.Dispose();
                var texture = result.Value.CreateTexture();
                if (texture == null)
                {
                    return;
                }

                CacheImagePreview(source.CacheKey, texture);
                ShowImageTooltip(row, node, texture);
            });
        }

        private void OnImageRowPointerMove(PointerMoveEvent evt)
        {
            var row = evt.currentTarget as VisualElement;
            if (row == null || !ReferenceEquals(row, _hoveredImageRow))
            {
                return;
            }

            _hoveredPanelPosition = row.LocalToWorld(evt.localPosition);
            if (_imageTooltipWindow != null)
            {
                _imageTooltipWindow.SetPointerPosition(row, _hoveredPanelPosition);
            }
        }

        private void OnImageRowPointerLeave(PointerLeaveEvent evt)
        {
            if (ReferenceEquals(evt.currentTarget, _hoveredImageRow))
            {
                HideImageTooltip();
            }
        }

        private void ShowImageTooltip(VisualElement row, FileTreeNode node, Texture2D texture)
        {
            if (row == null || node == null || texture == null || row.panel == null)
            {
                return;
            }

            _imageTooltipWindow = ImageTooltipWindow.Show(
                row,
                _hoveredPanelPosition,
                new ImageTooltipState(texture, node.Name));
        }

        private void HideImageTooltip()
        {
            _imagePreviewVersion++;
            CancelImagePreview(ref _imagePreviewCancellation);

            if (_imageTooltipWindow != null)
            {
                _imageTooltipWindow.Close();
                _imageTooltipWindow = null;
            }

            _hoveredImageRow = null;
            _hoveredImageNode = null;
        }

        internal static void CancelImagePreview(ref CancellationTokenSource cancellation)
        {
            var current = cancellation;
            cancellation = null;
            if (current == null)
            {
                return;
            }

            try
            {
                current.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Completion may have disposed the source before its delayed cleanup reached the Editor thread.
            }
        }

        private void CacheImagePreview(string key, Texture2D texture)
        {
            if (string.IsNullOrWhiteSpace(key) || texture == null)
            {
                return;
            }

            if (_imagePreviewCache.Count >= MaximumCachedImagePreviews)
            {
                ClearImagePreviewCache();
            }

            _imagePreviewCache[key] = texture;
        }

        private void ClearImagePreviewCache()
        {
            foreach (var texture in _imagePreviewCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            _imagePreviewCache.Clear();
        }

        private static FileTreeNode ResolveBoundNode(VisualElement row)
        {
            var item = row != null ? row.userData as SearchableTreeItemData<FileTreeNode> : null;
            return item != null ? item.Data : null;
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
            if (item == null || item.GroupKind == FileTreeGroupKind.Variant)
            {
                return;
            }

            var selected = selectedItems ?? Array.Empty<FileTreeNode>();
            var importTargetSelection = selected.Where(node => node != null && !node.IsGroup && node.CanSetImportTarget).ToArray();

            var menuItems = new List<ContextMenuItemState>();
            Action importAction;
            if (TryCreateImportAction(item, out importAction))
            {
                menuItems.Add(new ContextMenuItemState(
                    "import",
                    I18N.Get("assetManager.infomationPanel.fileTree.context.import"),
                    importAction));
            }

            if (item.CanSetAsVersionGroupPrimary)
            {
                menuItems.Add(new ContextMenuItemState(
                    "set-version-group-primary",
                    I18N.Get("assetManager.infomationPanel.fileTree.context.setPrimaryFile"),
                    () => _assetManager.SetVersionGroupPrimaryFile(item.VersionGroupId, item.AssetFileId)));
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

        private bool TryCreateImportAction(FileTreeNode item, out Action importAction)
        {
            importAction = null;
            if (item == null || string.IsNullOrWhiteSpace(_itemId))
            {
                return false;
            }

            var itemId = _itemId;
            if (item.IsAssetFileRoot || item.GroupKind == FileTreeGroupKind.Version)
            {
                var fileId = item.AssetFileId;
                if (string.IsNullOrWhiteSpace(fileId) || _assetManager.GetFileImportTargets(fileId).Count == 0)
                {
                    return false;
                }

                importAction = () => _assetManager.ImportFileTargets(itemId, fileId);
                return true;
            }

            if (item.IsGroup || item.IsDirectory || item.ImportTargetEntries.Count != 1)
            {
                return false;
            }

            var entry = item.ImportTargetEntries[0];
            if (string.IsNullOrWhiteSpace(entry.FileId) || string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                return false;
            }

            importAction = () => _assetManager.ImportFileEntry(itemId, entry.FileId, entry.RelativePath);
            return true;
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
                var targets = _assetManager.GetFileImportTargets(fileId)
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

                _assetManager.SetFileImportTargets(
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
            FileTreeGroupKind groupKind,
            string groupId,
            string cacheDirectory,
            string inaccessibleText,
            string zipText)
        {
            ItemId = itemId ?? string.Empty;
            FileId = fileId ?? string.Empty;
            GroupKind = groupKind;
            GroupId = groupId ?? string.Empty;
            CacheDirectory = cacheDirectory ?? string.Empty;
            InaccessibleText = inaccessibleText ?? string.Empty;
            ZipText = zipText ?? string.Empty;
        }

        private string ItemId { get; }

        private string FileId { get; }

        private FileTreeGroupKind GroupKind { get; }

        private string GroupId { get; }

        private string CacheDirectory { get; }

        private string InaccessibleText { get; }

        private string ZipText { get; }

        public bool Equals(FileTreeMemoryCacheKey other)
        {
            return string.Equals(ItemId, other.ItemId, StringComparison.Ordinal) &&
                   string.Equals(FileId, other.FileId, StringComparison.Ordinal) &&
                   GroupKind == other.GroupKind &&
                   string.Equals(GroupId, other.GroupId, StringComparison.Ordinal) &&
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
                hashCode = (hashCode * 397) ^ (int)GroupKind;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(GroupId);
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

        public static void SetImportTargets(string fileId, IReadOnlyList<AssetFileImportTarget> targets)
        {
            var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targets != null)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null)
                    {
                        targetPaths.Add(
                            FileTreeImportTargetPolicy
                                .NormalizeRelativePath(
                                    targets[i].RelativePath));
                    }
                }
            }

            lock (Gate)
            {
                foreach (var entry in Entries.Values)
                {
                    Visit(entry, node => node.SetImportTargetState(fileId, targetPaths));
                }
            }
        }

        public static void SetVersionGroupPrimaryFile(string versionGroupId, string primaryFileId)
        {
            lock (Gate)
            {
                foreach (var entry in Entries.Values)
                {
                    Visit(entry, node => node.SetVersionGroupPrimaryFile(versionGroupId, primaryFileId));
                }
            }
        }

        private static void Visit(IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items, Action<FileTreeNode> visitor)
        {
            if (items == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                visitor(items[i].Data);
                Visit(items[i].Children, visitor);
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
            string groupId = null,
            bool isAssetFileRoot = false,
            string assetFileId = null,
            string versionGroupId = null,
            bool isPrimaryFile = false,
            IReadOnlyList<FileTreeImportTargetEntry> importTargetEntries = null,
            FileTreeImageSource imageSource = null,
            string detailParentName = null,
            string detailArchivePath = null,
            string detailArchiveEntryPath = null)
        {
            Name = name ?? string.Empty;
            Meta = meta ?? string.Empty;
            Path = path ?? string.Empty;
            IsImportTarget = isImportTarget;
            HasAnyImportTarget = hasAnyImportTarget;
            RelativePath =
                FileTreeImportTargetPolicy.NormalizeRelativePath(
                    relativePath);
            IsDirectory = isDirectory;
            IsGroup = isGroup;
            GroupKind = groupKind;
            GroupId = groupId ?? string.Empty;
            IsAssetFileRoot = isAssetFileRoot;
            AssetFileId = assetFileId ?? string.Empty;
            VersionGroupId = versionGroupId ?? string.Empty;
            IsPrimaryFile = isPrimaryFile;
            ImportTargetEntries = importTargetEntries ?? Array.Empty<FileTreeImportTargetEntry>();
            ImageSource = imageSource;
            DetailParentName = detailParentName ?? string.Empty;
            DetailArchivePath =
                detailArchivePath ?? string.Empty;
            DetailArchiveEntryPath =
                detailArchiveEntryPath ?? string.Empty;
        }

        public string Name { get; }

        public string Meta { get; }

        public string Path { get; }

        public bool IsImportTarget { get; private set; }

        public bool HasAnyImportTarget { get; private set; }

        public string RelativePath { get; }

        public bool IsDirectory { get; }

        public bool IsGroup { get; }

        public FileTreeGroupKind GroupKind { get; }

        public string GroupId { get; }

        public bool IsAssetFileRoot { get; }

        public string AssetFileId { get; private set; }

        public string VersionGroupId { get; }

        public bool IsPrimaryFile { get; private set; }

        public IReadOnlyList<FileTreeImportTargetEntry> ImportTargetEntries { get; }

        public FileTreeImageSource ImageSource { get; }

        public string DetailParentName { get; }

        public string DetailArchivePath { get; }

        public string DetailArchiveEntryPath { get; }

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

        public FileTreeDetailState CreateDetailState(string itemId)
        {
            if (IsAssetFileRoot &&
                !string.IsNullOrWhiteSpace(
                    AssetFileId))
            {
                return FileTreeDetailState
                    .FromAssetFile(
                        AssetFileId,
                        Name,
                        Path);
            }

            var detailId = string.Join("|", new[]
            {
                itemId ?? string.Empty,
                AssetFileId ?? string.Empty,
                Path ?? string.Empty,
                Name ?? string.Empty
            });
            return new FileTreeDetailState(
                detailId,
                Name,
                DetailParentName,
                IsDirectory || IsGroup
                    ? string.Empty
                    : Path,
                sourceArchivePath:
                    DetailArchivePath,
                sourceArchiveEntryPath:
                    DetailArchiveEntryPath);
        }

        public void SetImportTargetState(string fileId, HashSet<string> targetPaths)
        {
            var entries = ImportTargetEntries
                .Where(entry => string.Equals(
                    entry.FileId,
                    fileId,
                    StringComparison.Ordinal))
                .ToArray();
            var appliesToFile =
                string.Equals(
                    AssetFileId,
                    fileId,
                    StringComparison.Ordinal) ||
                entries.Length > 0;

            if (!appliesToFile)
            {
                return;
            }

            var coverage =
                FileTreeImportTargetPolicy.GetCoverage(
                    targetPaths,
                    RelativePath,
                    entries);
            IsImportTarget =
                !IsAssetFileRoot &&
                coverage == FileTreeImportTargetCoverage.All;
            HasAnyImportTarget =
                !IsAssetFileRoot &&
                coverage != FileTreeImportTargetCoverage.None;
        }

        public void SetVersionGroupPrimaryFile(string versionGroupId, string primaryFileId)
        {
            if (!string.Equals(VersionGroupId, versionGroupId, StringComparison.Ordinal))
            {
                return;
            }

            if (GroupKind == FileTreeGroupKind.Version)
            {
                AssetFileId = primaryFileId ?? string.Empty;
                return;
            }

            if (!IsAssetFileRoot)
            {
                return;
            }

            IsPrimaryFile = string.Equals(AssetFileId, primaryFileId, StringComparison.Ordinal);
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
        private readonly IAssetManager _assetManager;
        private readonly ZipFileTreeReader _zipReader;
        private readonly IAssetFileSystemReader _fileSystemReader;
        private readonly string _inaccessibleText;
        private readonly string _zipText;
        private readonly CancellationToken _cancellationToken;
        private int _nextId;
        private IReadOnlyDictionary<string, IReadOnlyList<AssetFileImportTarget>> _importTargetsByFileId;

        public FileTreeBuilder(
            IAssetManager assetManager,
            IAssetArchiveReader archiveReader,
            IAssetFileSystemReader fileSystemReader,
            string inaccessibleText,
            string zipText,
            CancellationToken cancellationToken)
        {
            _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
            _zipReader = new ZipFileTreeReader(archiveReader, cancellationToken);
            _fileSystemReader = fileSystemReader ??
                                throw new ArgumentNullException(
                                    nameof(fileSystemReader));
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
                : CreateGroupItem(
                    variantGroup.Name,
                    FileTreeGroupKind.Variant,
                    variantGroup.Id,
                    children);
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
                : CreateGroupItem(
                    versionGroup.Name,
                    FileTreeGroupKind.Version,
                    versionGroup.Id,
                    children,
                    versionGroup.Id,
                    versionGroup.PrimaryFileId);
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
            string groupId,
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children,
            string versionGroupId = null,
            string primaryFileId = null)
        {
            var node = new FileTreeNode(
                name,
                string.Empty,
                name,
                isGroup: true,
                groupKind: groupKind,
                groupId: groupId,
                assetFileId: primaryFileId,
                versionGroupId: versionGroupId);
            return new SearchableTreeItemData<FileTreeNode>(
                _nextId++,
                node,
                node.Name,
                node.Name,
                children);
        }

        private SearchableTreeItemData<FileTreeNode> BuildAssetFileNode(AssetFile file, bool isPrimaryFile = false)
        {
            var resolution = _assetManager.ResolveFilePath(file.Id);
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
            if (_fileSystemReader.DirectoryExists(path))
            {
                return CreateDirectoryItem(path, Path.GetFileName(path), path, file, path, string.Empty, isAssetFileRoot: true, isPrimaryFile: isPrimaryFile);
            }

            if (_fileSystemReader.FileExists(path) && IsZipFile(file, path))
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
                var entries = _fileSystemReader.GetDirectoryEntries(
                    path,
                    _cancellationToken);
                for (var i = 0; i < entries.Count; i++)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    if (entry.Kind == AssetFileSystemEntryKind.Directory)
                    {
                        children.Add(CreateDirectoryItem(
                            entry.FullPath,
                            entry.Name,
                            entry.FullPath,
                            assetFile,
                            rootPath,
                            GetRelativePath(rootPath, entry.FullPath)));
                    }
                    else
                    {
                        var childRelativePath = GetRelativePath(
                            rootPath,
                            entry.FullPath);
                        if (IsZipPath(entry.FullPath))
                        {
                            children.Add(CreateZipItem(
                                entry.Name,
                                entry.FullPath,
                                assetFile,
                                childRelativePath));
                        }
                        else
                        {
                            children.Add(CreateFileItem(
                                entry.FullPath,
                                entry.Name,
                                entry.FullPath,
                                assetFile,
                                childRelativePath));
                        }
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
            var targetPath =
                FileTreeImportTargetPolicy.NormalizeRelativePath(
                    relativePath);
            var importTargetEntries =
                FileTreeImportTargetPolicy.CreateEntries(
                    assetFile == null ? null : assetFile.Id,
                    targetPath,
                    isDirectory,
                    children == null
                        ? null
                        : children.Select(item =>
                            item.Data.ImportTargetEntries));
            var targetPaths = GetTargetPathSet(assetFile);
            var coverage =
                FileTreeImportTargetPolicy.GetCoverage(
                    targetPaths,
                    targetPath,
                    importTargetEntries);
            var node = new FileTreeNode(
                name,
                meta,
                searchPath,
                !isAssetFileRoot &&
                coverage == FileTreeImportTargetCoverage.All,
                !isAssetFileRoot &&
                coverage != FileTreeImportTargetCoverage.None,
                targetPath,
                isDirectory,
                isAssetFileRoot: isAssetFileRoot,
                assetFileId: assetFile == null ? null : assetFile.Id,
                versionGroupId: assetFile == null ? null : assetFile.VersionGroupId,
                isPrimaryFile: isPrimaryFile,
                importTargetEntries: importTargetEntries,
                imageSource: isDirectory ? null : FileTreeImageSource.FromFile(name, searchPath));
            return new SearchableTreeItemData<FileTreeNode>(
                _nextId++,
                node,
                string.Join(" ", new[] { node.Name, node.Meta, node.Path }),
                node.Name,
                children);
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
                set.Add(
                    FileTreeImportTargetPolicy.NormalizeRelativePath(
                        targets[i].RelativePath));
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

            return FileTreeImportTargetPolicy.NormalizeRelativePath(
                Path.GetRelativePath(rootPath, path));
        }
    }

    internal sealed class ZipFileTreeReader
    {
        private readonly IAssetArchiveReader _archiveReader;
        private readonly CancellationToken _cancellationToken;

        public ZipFileTreeReader(
            IAssetArchiveReader archiveReader,
            CancellationToken cancellationToken)
        {
            _archiveReader = archiveReader ?? throw new ArgumentNullException(nameof(archiveReader));
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
            var entries = _archiveReader.ReadZipEntries(zipPath, _cancellationToken);
            for (var i = 0; i < entries.Count; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                AddEntry(root, entries[i]);
            }

            var snapshot = new ZipTreeCacheEntry(root.Children);
            return snapshot.CreateTree(nextId, assetFile, importTargetPaths, relativePathPrefix, zipPath);
        }

        private static void AddEntry(ZipVirtualDirectory root, AssetArchiveEntry entry)
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

                current.AddFile(parts[i], childPath, entry.Length, entry.ArchiveFullName);
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
                string relativePathPrefix,
                string archivePath)
            {
                var items = new List<SearchableTreeItemData<FileTreeNode>>(_children.Count);
                for (var i = 0; i < _children.Count; i++)
                {
                    items.Add(_children[i].CreateTreeItem(nextId, assetFile, importTargetPaths, relativePathPrefix, archivePath));
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
                string relativePathPrefix,
                string archivePath);

            protected SearchableTreeItemData<FileTreeNode> CreateItem(
                Func<int> nextId,
                string meta,
                AssetFile assetFile,
                bool isDirectory,
                HashSet<string> importTargetPaths,
                string relativePathPrefix,
                string archivePath,
                string archiveEntryPath = null,
                IReadOnlyList<SearchableTreeItemData<FileTreeNode>> children = null)
            {
                var targetPath =
                    FileTreeImportTargetPolicy.CombineRelativePath(
                        relativePathPrefix,
                        Path);
                var importTargetEntries =
                    FileTreeImportTargetPolicy.CreateEntries(
                        assetFile == null ? null : assetFile.Id,
                        targetPath,
                        isDirectory,
                        children == null
                            ? null
                            : children.Select(item =>
                                item.Data.ImportTargetEntries));
                var coverage =
                    FileTreeImportTargetPolicy.GetCoverage(
                        importTargetPaths,
                        targetPath,
                        importTargetEntries);
                var node = new FileTreeNode(
                    Name,
                    meta,
                    targetPath,
                    coverage == FileTreeImportTargetCoverage.All,
                    coverage != FileTreeImportTargetCoverage.None,
                    targetPath,
                    isDirectory,
                    importTargetEntries: importTargetEntries,
                    imageSource: isDirectory
                        ? null
                        : FileTreeImageSource.FromArchive(Name, archivePath, archiveEntryPath),
                    detailParentName: System.IO.Path.GetFileName(archivePath),
                    detailArchivePath: isDirectory
                        ? null
                        : archivePath,
                    detailArchiveEntryPath: isDirectory
                        ? null
                        : archiveEntryPath);
                return new SearchableTreeItemData<FileTreeNode>(
                    nextId(),
                    node,
                    string.Join(" ", new[] { node.Name, node.Meta, node.Path }),
                    node.Name,
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

            public void AddFile(string name, string path, long length, string archiveFullName)
            {
                _children.Add(new ZipVirtualFile(name, path, length, archiveFullName));
            }

            public override SearchableTreeItemData<FileTreeNode> CreateTreeItem(
                Func<int> nextId,
                AssetFile assetFile,
                HashSet<string> importTargetPaths,
                string relativePathPrefix,
                string archivePath)
            {
                var childItems = new List<SearchableTreeItemData<FileTreeNode>>(_children.Count);
                for (var i = 0; i < _children.Count; i++)
                {
                    childItems.Add(_children[i].CreateTreeItem(nextId, assetFile, importTargetPaths, relativePathPrefix, archivePath));
                }

                return CreateItem(nextId, string.Empty, assetFile, true, importTargetPaths, relativePathPrefix, archivePath, children: childItems);
            }
        }

        private sealed class ZipVirtualFile : ZipVirtualNode
        {
            private readonly long _length;
            private readonly string _archiveFullName;

            public ZipVirtualFile(string name, string path, long length, string archiveFullName)
                : base(name, path)
            {
                _length = length;
                _archiveFullName = archiveFullName ?? string.Empty;
            }

            public override SearchableTreeItemData<FileTreeNode> CreateTreeItem(
                Func<int> nextId,
                AssetFile assetFile,
                HashSet<string> importTargetPaths,
                string relativePathPrefix,
                string archivePath)
            {
                return CreateItem(
                    nextId,
                    string.Empty,
                    assetFile,
                    false,
                    importTargetPaths,
                    relativePathPrefix,
                    archivePath,
                    _archiveFullName);
            }
        }
    }
}
