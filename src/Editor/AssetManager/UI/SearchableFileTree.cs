using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class SearchableFileTree :
        SearchableTreeView<FileTreeNode>, IDisposable
    {
        private const string RootClassName =
            "ee4v-asset-manager-file-tree";
        internal const string RowClassName =
            "ee4v-asset-manager-file-tree__row";
        internal const string RowTitleClassName =
            "ee4v-asset-manager-file-tree__title";
        internal const string RowMetaClassName =
            "ee4v-asset-manager-file-tree__meta";
        private const string RowOverviewClassName =
            "ee4v-asset-manager-file-tree__row--overview";
        private const string RowGroupClassName =
            "ee4v-asset-manager-file-tree__row--group";
        private const string RowSectionClassName =
            "ee4v-asset-manager-file-tree__row--new-section";
        private const string TargetToggleElementName = "file-tree-target";

        private readonly IAssetManager _manager;
        private readonly UiTextElement _feedback;
        private readonly bool _showsTargetToggles;
        private readonly Action<FileTreeSelection> _importRequested;
        private readonly Dictionary<string, AssetFileTarget>
            _targetSelection =
                new Dictionary<string, AssetFileTarget>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CachedAnalysis> _analysisCache =
            new Dictionary<string, CachedAnalysis>(StringComparer.Ordinal);
        private CancellationTokenSource _reloadCancellation;
        private IReadOnlyList<SearchableTreeItemData<FileTreeNode>>
            _pendingTreeItems;
        private IReadOnlyList<AssetFile> _files = Array.Empty<AssetFile>();
        private IReadOnlyList<FileTreeGroup> _groups =
            Array.Empty<FileTreeGroup>();
        private string _itemId = string.Empty;
        private int _reloadVersion;
        private bool _isPointerOverTree;
        private bool _suppressSelectionChanged;

        internal SearchableFileTree(
            IAssetManager manager,
            Action registerFileRequested = null,
            Action<FileTreeSelection> importRequested = null,
            bool showTargetToggles = false)
            : base(
                CreateTreeItem,
                BindTreeItem,
                emptyText: I18N.Get("fileTree.empty"),
                searchPlaceholder: I18N.Get(
                    "fileTree.searchPlaceholder"),
                selectionType: SelectionType.Single,
                searchTooltip: I18N.Get("fileTree.searchTooltip"),
                clearTooltip: I18N.Get("toolbar.search.clear"),
                searchIconState:
                    AssetManagerControls.LoadFluentIconState(
                        "search.png",
                        UiSizeTokens.Size14,
                        I18N.Get("fileTree.searchTooltip")),
                clearIconState:
                    AssetManagerControls.LoadFluentIconState(
                        "dismiss.png",
                        UiSizeTokens.Size10,
                        I18N.Get("toolbar.search.clear")),
                fixedItemHeight: 30f,
                selectOnContextClick: false)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _showsTargetToggles = showTargetToggles;
            _importRequested = importRequested;
            AddToClassList(RootClassName);
            SetInteractionHandlers(
                OnTreeSelectionChanged,
                showTargetToggles ? null : OnTreeContextClick);

            var header = new SectionHeader(I18N.Get("fileTree.title"));
            header.AddToClassList(RootClassName + "__header");
            header.TitleText.AddToClassList(
                RootClassName + "__heading");
            if (registerFileRequested != null)
            {
                header.Actions.Add(AssetManagerControls.CreateButton(
                    I18N.Get("detail.file.register"),
                    registerFileRequested,
                    RootClassName + "__register"));
            }
            Insert(0, header);

            SetViewDataKey(
                "ee4v-asset-manager-item-detail-file-tree");

            _feedback = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                RootClassName + "__feedback");
            _feedback.SetColor(UiColorTokens.StatusFailedText);
            _feedback.SetWhiteSpace(WhiteSpace.Normal);
            _feedback.style.display = DisplayStyle.None;
            Add(_feedback);

            RegisterCallback<PointerEnterEvent>(_ =>
                _isPointerOverTree = true);
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _isPointerOverTree = false;
                ApplyPendingTreeItems();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _pendingTreeItems = null;
                CancelReload();
            });
        }

        internal event Action<FileTreeSelection> SelectionChanged;

        internal void SetItem(
            string itemId,
            IReadOnlyList<AssetFile> files,
            IReadOnlyList<AssetFileTarget> targets = null,
            IReadOnlyDictionary<string, AssetFileAnalysis>
                initialAnalyses = null,
            IReadOnlyList<FileTreeGroup> groups = null)
        {
            CancelReload();
            _pendingTreeItems = null;
            var nextItemId = itemId ?? string.Empty;
            if (!string.Equals(_itemId, nextItemId, StringComparison.Ordinal))
            {
                _analysisCache.Clear();
            }
            _itemId = nextItemId;
            _targetSelection.Clear();
            foreach (var target in targets ??
                     Array.Empty<AssetFileTarget>())
            {
                if (target != null)
                {
                    _targetSelection[TargetKey(
                        target.FileId,
                        target.TargetPath)] = target;
                }
            }
            _files = (files ?? Array.Empty<AssetFile>())
                .Where(file => file != null)
                .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.Id, StringComparer.Ordinal)
                .ToArray();
            _groups = (groups ?? Array.Empty<FileTreeGroup>())
                .Where(group => group != null)
                .ToArray();
            var currentFileIds = new HashSet<string>(
                _files.Select(file => file.Id),
                StringComparer.Ordinal);
            foreach (var cachedFileId in _analysisCache.Keys
                         .Where(fileId => !currentFileIds.Contains(fileId))
                         .ToArray())
            {
                _analysisCache.Remove(cachedFileId);
            }
            SeedAnalysisCache(initialAnalyses);
            var analyses = CreateCachedAnalyses();
            SetFeedback(string.Empty);
            ApplyTreeItems(AssetFileTreeBuilder.Build(
                _files,
                analyses,
                CancellationToken.None,
                I18N.Get("fileTree.overview"),
                I18N.Get("fileTree.itemMeta"),
                includeOverview: !_showsTargetToggles,
                groups: _groups));

            if (!_files.Any(AssetFileTreeBuilder.CanAnalyze))
            {
                return;
            }

            var version = ++_reloadVersion;
            var cancellation = new CancellationTokenSource();
            _reloadCancellation = cancellation;
            _ = LoadAnalysesAsync(
                version,
                cancellation,
                _files,
                analyses);
        }

        public void Dispose()
        {
            CancelReload();
        }

        internal IReadOnlyList<AssetFileTarget> GetTargetSelection()
        {
            return _targetSelection.Values
                .Select(target => new AssetFileTarget
                {
                    FileId = target.FileId,
                    TargetPath = target.TargetPath,
                    GroupName = target.GroupName
                })
                .ToArray();
        }

        internal IReadOnlyDictionary<string, AssetFileAnalysis>
            GetCachedAnalyses()
        {
            return CreateCachedAnalyses();
        }

        private Dictionary<string, AssetFileAnalysis>
            CreateCachedAnalyses()
        {
            var analyses = new Dictionary<string, AssetFileAnalysis>(
                StringComparer.Ordinal);
            foreach (var file in _files)
            {
                if (_analysisCache.TryGetValue(file.Id, out var cached) &&
                    string.Equals(
                        cached.Version,
                        CreateCacheKey(file),
                        StringComparison.Ordinal))
                {
                    analyses[file.Id] = cached.Analysis;
                }
            }
            return analyses;
        }

        private async Task LoadAnalysesAsync(
            int version,
            CancellationTokenSource cancellation,
            IReadOnlyList<AssetFile> files,
            Dictionary<string, AssetFileAnalysis> analyses)
        {
            var failures = new List<string>();
            try
            {
                for (var index = 0; index < files.Count; index++)
                {
                    var file = files[index];
                    if (!AssetFileTreeBuilder.CanAnalyze(file))
                    {
                        continue;
                    }

                    cancellation.Token.ThrowIfCancellationRequested();
                    if (analyses.ContainsKey(file.Id))
                    {
                        continue;
                    }
                    var cacheKey = CreateCacheKey(file);
                    try
                    {
                        var analysis = await _manager.AnalyzeFileAsync(
                            file.Id,
                            cancellation.Token);
                        if (!IsCurrentReload(version, cancellation))
                        {
                            return;
                        }
                        analyses[file.Id] = analysis;
                        _analysisCache[file.Id] = new CachedAnalysis(
                            cacheKey,
                            analysis);
                        if (_showsTargetToggles)
                        {
                            await ApplyAnalysesAsync(
                                version,
                                cancellation,
                                files,
                                analyses);
                        }
                    }
                    catch (AssetManagerException)
                    {
                        failures.Add(file.FileName);
                    }
                }

                if (!IsCurrentReload(version, cancellation))
                {
                    return;
                }

                if (!_showsTargetToggles)
                {
                    await ApplyAnalysesAsync(
                        version,
                        cancellation,
                        files,
                        analyses);
                }
                if (failures.Count > 0)
                {
                    SetFeedback(string.Format(
                        I18N.Get("fileTree.analysisFailed"),
                        string.Join(", ", failures)));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (IsCurrentReload(version, cancellation))
                {
                    Debug.LogException(exception);
                    SetFeedback(I18N.Get("fileTree.analysisUnavailable"));
                }
            }
            finally
            {
                if (ReferenceEquals(_reloadCancellation, cancellation))
                {
                    _reloadCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void SetFeedback(string text)
        {
            _feedback.SetText(text);
            _feedback.style.display = string.IsNullOrWhiteSpace(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private async Task ApplyAnalysesAsync(
            int version,
            CancellationTokenSource cancellation,
            IReadOnlyList<AssetFile> files,
            IReadOnlyDictionary<string, AssetFileAnalysis> analyses)
        {
            var overviewTitle = I18N.Get("fileTree.overview");
            var overviewMeta = I18N.Get("fileTree.itemMeta");
            var items = await Task.Run(
                () => AssetFileTreeBuilder.Build(
                    files,
                    analyses,
                    cancellation.Token,
                    overviewTitle,
                    overviewMeta,
                    includeOverview: !_showsTargetToggles,
                    groups: _groups),
                cancellation.Token);
            if (IsCurrentReload(version, cancellation))
            {
                if (_showsTargetToggles && _isPointerOverTree)
                {
                    _pendingTreeItems = items;
                }
                else
                {
                    ApplyTreeItems(items);
                }
            }
        }

        private void ApplyPendingTreeItems()
        {
            if (_pendingTreeItems == null)
            {
                return;
            }

            var items = _pendingTreeItems;
            _pendingTreeItems = null;
            ApplyTreeItems(items);
        }

        private void SeedAnalysisCache(
            IReadOnlyDictionary<string, AssetFileAnalysis> analyses)
        {
            if (analyses == null)
            {
                return;
            }

            foreach (var file in _files)
            {
                if (analyses.TryGetValue(file.Id, out var analysis))
                {
                    _analysisCache[file.Id] = new CachedAnalysis(
                        CreateCacheKey(file),
                        analysis);
                }
            }
        }

        private bool IsCurrentReload(
            int version,
            CancellationTokenSource cancellation)
        {
            return version == _reloadVersion &&
                   ReferenceEquals(_reloadCancellation, cancellation) &&
                   !cancellation.IsCancellationRequested;
        }

        private void ApplyTreeItems(
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items)
        {
            ConfigureTargetNodes(items);
            _suppressSelectionChanged = true;
            try
            {
                SetItems(
                    items,
                    preserveExpansion: true);
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private static VisualElement CreateTreeItem()
        {
            var row = new ItemRow();
            row.AddToClassList(RowClassName);
            var targetToggle = UiTextFactory.CreateToggle(
                string.Empty,
                RootClassName + "__target-toggle");
            targetToggle.name = TargetToggleElementName;
            targetToggle.RegisterValueChangedCallback(evt =>
            {
                if (targetToggle.userData is FileTreeNode node)
                {
                    node.TargetChanged?.Invoke(node, evt.newValue);
                }
            });
            row.Leading.Add(targetToggle);
            row.IconElement.AddToClassList(RootClassName + "__icon");
            row.TitleText.AddToClassList(RowTitleClassName);
            row.DescriptionText.AddToClassList(RowMetaClassName);
            return row;
        }

        private static void BindTreeItem(
            VisualElement element,
            FileTreeNode node)
        {
            element.EnableInClassList(
                RowOverviewClassName,
                node?.IsOverview == true);
            element.EnableInClassList(
                RowGroupClassName,
                node?.IsGroup == true);
            element.EnableInClassList(
                RowSectionClassName,
                node?.StartsNewSection == true);
            var targetToggle = element.Q<Toggle>(
                TargetToggleElementName);
            if (targetToggle != null)
            {
                targetToggle.userData = node;
                targetToggle.style.display = node != null &&
                                             node.ShowsTargetToggle
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                targetToggle.SetValueWithoutNotify(node?.IsTarget == true);
            }
            if (element is ItemRow row)
            {
                row.SetState(new ItemRowState(
                    node?.Title ?? string.Empty,
                    node?.Meta ?? string.Empty,
                    IconState.FromTexture(
                        ResolveTreeIcon(node),
                        UiSizeTokens.Size14)));
            }
        }

        private static Texture2D ResolveTreeIcon(FileTreeNode node)
        {
            var iconName = node?.IsGroup == true
                ? "folder.png"
                : node == null || node.IsOverview
                    ? "info.png"
                    : node.Entry == null
                        ? "folder_zip.png"
                        : node.Entry.Kind ==
                          AssetFileContentEntryKind.Directory
                            ? "folder.png"
                            : "cube.png";
            return AssetManagerControls.LoadFluentIconTexture(iconName);
        }

        private void OnTreeContextClick(
            VisualElement element,
            FileTreeNode node,
            IReadOnlyList<FileTreeNode> selection,
            Vector2 panelPosition)
        {
            if (node?.File == null)
            {
                return;
            }

            var menu = new GenericMenu();
            var targetPath = node.Entry?.Path ?? string.Empty;
            var canImport = _importRequested != null && CanImport(node);
            if (canImport)
            {
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(
                        I18N.Get("action.import")),
                    false,
                    () => _importRequested(new FileTreeSelection(
                        node.File,
                        node.Entry)));
            }
            else
            {
                menu.AddDisabledItem(UiTextFactory.CreateGuiContent(
                    I18N.Get("action.import")));
            }

            menu.AddSeparator(string.Empty);
            var configuredTargets = _manager
                .GetItemTargets(_itemId);
            var configuredTarget = configuredTargets
                .FirstOrDefault(target =>
                    string.Equals(
                        target.FileId,
                        node.File.Id,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        target.TargetPath,
                        targetPath,
                        StringComparison.OrdinalIgnoreCase));
            var canSetTarget = CanSetTarget(node);
            if (string.IsNullOrWhiteSpace(_itemId) || !canSetTarget)
            {
                menu.AddDisabledItem(UiTextFactory.CreateGuiContent(
                    I18N.Get("action.addTarget")));
            }
            else
            {
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(I18N.Get(
                        configuredTarget == null
                            ? "action.addTarget"
                            : "action.removeTarget")),
                    false,
                    () => SetTarget(
                        node.File.Id,
                        targetPath,
                        configuredTargets,
                        configuredTarget != null));
            }

            menu.ShowAsContext();
        }

        private void SetTarget(
            string fileId,
            string targetPath,
            IReadOnlyList<AssetFileTarget> configuredTargets,
            bool isConfigured)
        {
            _manager.SetItemTargets(
                _itemId,
                (isConfigured
                    ? (configuredTargets ??
                       Array.Empty<AssetFileTarget>())
                        .Where(target => !(
                            string.Equals(
                                target.FileId,
                                fileId,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                target.TargetPath,
                                targetPath,
                                StringComparison.OrdinalIgnoreCase)))
                    : (configuredTargets ??
                       Array.Empty<AssetFileTarget>())
                        .Concat(new[]
                        {
                            new AssetFileTarget
                            {
                                FileId = fileId,
                                TargetPath = targetPath
                            }
                        }))
                .ToArray());
        }

        private void ConfigureTargetNodes(
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items)
        {
            foreach (var item in items ??
                     Array.Empty<SearchableTreeItemData<FileTreeNode>>())
            {
                var node = item.Data;
                node.ShowsTargetToggle =
                    _showsTargetToggles && CanSetTarget(node);
                node.IsTarget = node.ShowsTargetToggle &&
                                _targetSelection.ContainsKey(TargetKey(
                                    node.File.Id,
                                    node.Entry?.Path));
                node.TargetChanged = _showsTargetToggles
                    ? OnTargetChanged
                    : null;
                ConfigureTargetNodes(item.Children);
            }
        }

        private void OnTargetChanged(FileTreeNode node, bool selected)
        {
            if (!CanSetTarget(node))
            {
                return;
            }

            var path = node.Entry?.Path ?? string.Empty;
            var key = TargetKey(node.File.Id, path);
            node.IsTarget = selected;
            if (selected)
            {
                _targetSelection[key] = new AssetFileTarget
                {
                    FileId = node.File.Id,
                    TargetPath = path
                };
            }
            else
            {
                _targetSelection.Remove(key);
            }
        }

        private static bool CanSetTarget(FileTreeNode node)
        {
            return CanImport(node);
        }

        private static bool CanImport(FileTreeNode node)
        {
            if (node?.File == null)
            {
                return false;
            }

            return node.Entry == null
                ? !IsZip(node.File)
                : node.Entry.Kind == AssetFileContentEntryKind.File &&
                  !string.Equals(
                      Path.GetExtension(node.Entry.Path),
                      ".zip",
                      StringComparison.OrdinalIgnoreCase);
        }

        private static string TargetKey(string fileId, string targetPath)
        {
            return (fileId ?? string.Empty) + "\n" +
                   (targetPath ?? string.Empty);
        }

        private static bool IsZip(AssetFile file)
        {
            var extension = file?.Extension;
            if (string.IsNullOrWhiteSpace(extension))
            {
                var path = string.IsNullOrWhiteSpace(file?.FileName)
                    ? file?.SourcePath
                    : file.FileName;
                extension = Path.GetExtension(path);
            }
            return string.Equals(
                extension?.Trim().TrimStart('.'),
                "zip",
                StringComparison.OrdinalIgnoreCase);
        }

        private void OnTreeSelectionChanged(
            IReadOnlyList<FileTreeNode> selection)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            var node = selection == null || selection.Count == 0
                ? null
                : selection[selection.Count - 1];
            SelectionChanged?.Invoke(node == null
                ? null
                : new FileTreeSelection(node.File, node.Entry));
        }

        private void CancelReload()
        {
            _reloadVersion++;
            if (_reloadCancellation == null)
            {
                return;
            }

            _reloadCancellation.Cancel();
            _reloadCancellation = null;
        }

        private static string CreateCacheKey(AssetFile file)
        {
            return (file.SourcePath ?? string.Empty) + "\n" +
                   file.UpdatedAt.Ticks;
        }

        private sealed class CachedAnalysis
        {
            internal CachedAnalysis(string version, AssetFileAnalysis analysis)
            {
                Version = version;
                Analysis = analysis;
            }

            internal string Version { get; }
            internal AssetFileAnalysis Analysis { get; }
        }
    }

    internal sealed class FileTreeSelection
    {
        internal FileTreeSelection(
            AssetFile file,
            AssetFileContentEntry entry)
        {
            File = file;
            Entry = entry;
        }

        internal AssetFile File { get; }
        internal AssetFileContentEntry Entry { get; }
    }
}
