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
        private const int MaxConcurrentAnalyses = 3;
        private const int MaximumCachedImagePreviews = 24;
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
        private readonly HashSet<string> _unavailableTargetKeys;
        private readonly Action<IReadOnlyList<FileTreeSelection>>
            _importRequested;
        private readonly Dictionary<string, AssetFileTarget>
            _targetSelection =
                new Dictionary<string, AssetFileTarget>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CachedAnalysis> _analysisCache =
            new Dictionary<string, CachedAnalysis>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _fileTreeIds =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _imagePreviewCache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _reloadCancellation;
        private CancellationTokenSource _imagePreviewCancellation;
        private FileTreeImageTooltipWindow _imageTooltipWindow;
        private VisualElement _hoveredImageRow;
        private FileTreeNode _hoveredImageNode;
        private Vector2 _hoveredPanelPosition;
        private int _imagePreviewVersion;
        private Queue<AssetFile> _pendingAnalyses = new Queue<AssetFile>();
        private HashSet<string> _requestedAnalysisIds =
            new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, AssetFileAnalysis> _currentAnalyses;
        private IReadOnlyList<SearchableTreeItemData<FileTreeNode>>
            _pendingTreeItems;
        private IReadOnlyList<AssetFile> _files = Array.Empty<AssetFile>();
        private IReadOnlyList<FileTreeGroup> _groups =
            Array.Empty<FileTreeGroup>();
        private string _itemId = string.Empty;
        private int _reloadVersion;
        private bool _isPointerPressedOnTree;
        private bool _isSearching;
        private bool _suppressSelectionChanged;

        internal SearchableFileTree(
            IAssetManager manager,
            Action registerFileRequested = null,
            Action<IReadOnlyList<FileTreeSelection>> importRequested = null,
            bool showTargetToggles = false,
            IReadOnlyList<AssetFileTarget> unavailableTargets = null)
            : base(
                CreateTreeItem,
                BindTreeItem,
                emptyText: I18N.Get("fileTree.empty"),
                searchPlaceholder: I18N.Get(
                    "fileTree.searchPlaceholder"),
                selectionType: showTargetToggles
                    ? SelectionType.None
                    : SelectionType.Multiple,
                canInteractWithItem: node => node?.IsLoading != true,
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
            _unavailableTargetKeys = new HashSet<string>(
                (unavailableTargets ?? Array.Empty<AssetFileTarget>())
                    .Where(target => target != null)
                    .Select(target => TargetKey(
                        target.FileId, target.TargetPath)),
                StringComparer.OrdinalIgnoreCase);
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
                _showsTargetToggles
                    ? string.Empty
                    : "ee4v-asset-manager-item-detail-file-tree");

            _feedback = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                RootClassName + "__feedback");
            _feedback.SetColor(UiColorTokens.StatusFailedText);
            _feedback.SetWhiteSpace(WhiteSpace.Normal);
            _feedback.style.display = DisplayStyle.None;
            Add(_feedback);

            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _isPointerPressedOnTree = false;
                ApplyPendingTreeItems();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                HideImageTooltip();
                ClearImagePreviewCache();
                _pendingTreeItems = null;
                CancelReload();
            });
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_showsTargetToggles &&
                    evt.button == (int)MouseButton.LeftMouse)
                {
                    _isPointerPressedOnTree = true;
                }
            }, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_showsTargetToggles &&
                    evt.button == (int)MouseButton.LeftMouse)
                {
                    _isPointerPressedOnTree = false;
                    schedule.Execute(ApplyPendingTreeItems).StartingIn(1);
                }
                schedule.Execute(QueueExpandedAnalyses);
            }, TrickleDown.TrickleDown);
            RegisterCallback<ClickEvent>(_ =>
            {
                _isPointerPressedOnTree = false;
                schedule.Execute(() =>
                {
                    ApplyPendingTreeItems();
                    QueueExpandedAnalyses();
                }).StartingIn(1);
            }, TrickleDown.TrickleDown);
            RegisterCallback<KeyUpEvent>(
                _ => schedule.Execute(QueueExpandedAnalyses),
                TrickleDown.TrickleDown);
        }

        internal event Action<IReadOnlyList<FileTreeSelection>> SelectionChanged;

        internal string ItemId => _itemId;

        internal IReadOnlyList<FileTreeSelection> SelectedSelections =>
            GetSelectedData()
                .Select(node => new FileTreeSelection(node.File, node.Entry))
                .ToArray();

        internal void SetItem(
            string itemId,
            IReadOnlyList<AssetFile> files,
            IReadOnlyList<AssetFileTarget> targets = null,
            IReadOnlyDictionary<string, AssetFileAnalysis>
                initialAnalyses = null,
            IReadOnlyList<FileTreeGroup> groups = null)
        {
            HideImageTooltip();
            ClearImagePreviewCache();
            CancelReload();
            _pendingTreeItems = null;
            var nextItemId = itemId ?? string.Empty;
            if (!string.Equals(_itemId, nextItemId, StringComparison.Ordinal))
            {
                _analysisCache.Clear();
            }
            var preserveSelection = string.Equals(
                _itemId,
                nextItemId,
                StringComparison.Ordinal);
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
            _currentAnalyses = analyses;
            SetFeedback(string.Empty);
            var items = AssetFileTreeBuilder.Build(
                _files,
                analyses,
                CancellationToken.None,
                I18N.Get("fileTree.overview"),
                I18N.Get("fileTree.itemMeta"),
                I18N.Get("fileTree.loading"),
                includeOverview: !_showsTargetToggles,
                groups: _groups);
            ApplyTreeItems(items, preserveSelection);

            if (!_isSearching)
            {
                return;
            }
            QueueAllAnalyses();
        }

        protected override void OnSearchValueChanged(string value)
        {
            _isSearching = !string.IsNullOrWhiteSpace(value);
            if (_isSearching)
            {
                QueueAllAnalyses();
            }
        }

        private void QueueAllAnalyses()
        {
            foreach (var file in _files)
            {
                QueueAnalysis(file, startImmediately: false);
            }
            StartQueuedAnalyses();
        }

        private void QueueExpandedAnalyses()
        {
            if (panel == null || _isSearching)
            {
                return;
            }
            foreach (var file in _files)
            {
                if (IsFileExpanded(file.Id))
                {
                    QueueAnalysis(file, startImmediately: false);
                }
            }
            StartQueuedAnalyses();
        }

        private bool IsFileExpanded(string fileId)
        {
            return _fileTreeIds.TryGetValue(fileId, out var treeId) &&
                   IsItemExpanded(treeId);
        }

        private void QueueAnalysis(
            AssetFile file,
            bool startImmediately = true)
        {
            if (_currentAnalyses == null ||
                !AssetFileTreeBuilder.CanAnalyze(file) ||
                _currentAnalyses.ContainsKey(file.Id))
            {
                return;
            }

            if (_requestedAnalysisIds.Add(file.Id))
            {
                _pendingAnalyses.Enqueue(file);
            }
            if (startImmediately)
            {
                StartQueuedAnalyses();
            }
        }

        private void StartQueuedAnalyses()
        {
            if (_reloadCancellation != null ||
                _pendingAnalyses.Count == 0)
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
                _currentAnalyses,
                _pendingAnalyses);
        }

        public void Dispose()
        {
            HideImageTooltip();
            ClearImagePreviewCache();
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
            Dictionary<string, AssetFileAnalysis> analyses,
            Queue<AssetFile> pending)
        {
            var failures = new List<string>();
            var hasUnrenderedResults = false;
            try
            {
                var running = new List<Task<AnalysisLoadResult>>();
                while (pending.Count > 0 || running.Count > 0)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    while (pending.Count > 0 &&
                           running.Count < MaxConcurrentAnalyses)
                    {
                        running.Add(AnalyzeForTreeAsync(
                            pending.Dequeue(),
                            cancellation.Token));
                    }

                    var completed = await Task.WhenAny(running);
                    running.Remove(completed);
                    var result = await completed;
                    if (!IsCurrentReload(version, cancellation))
                    {
                        return;
                    }
                    if (result.Error is OperationCanceledException)
                    {
                        throw result.Error;
                    }
                    if (result.Error != null || result.Analysis == null)
                    {
                        if (result.Error != null &&
                            !(result.Error is AssetManagerException))
                        {
                            Debug.LogException(result.Error);
                        }
                        failures.Add(result.File.FileName);
                        analyses[result.File.Id] = new AssetFileAnalysis
                        {
                            FileId = result.File.Id,
                            Entries = Array.Empty<AssetFileContentEntry>()
                        };
                    }
                    else
                    {
                        analyses[result.File.Id] = result.Analysis;
                        _analysisCache[result.File.Id] = new CachedAnalysis(
                            CreateCacheKey(result.File),
                            result.Analysis);
                    }

                    hasUnrenderedResults = true;
                    if ((!_isSearching && IsFileExpanded(result.File.Id)) ||
                        (_showsTargetToggles && _isSearching))
                    {
                        await ApplyAnalysesAsync(
                            version,
                            cancellation,
                            files,
                            analyses);
                        hasUnrenderedResults = false;
                    }
                }

                if (!IsCurrentReload(version, cancellation))
                {
                    return;
                }

                if (hasUnrenderedResults)
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
                    cancellation.Dispose();
                    if (pending.Count > 0)
                    {
                        StartQueuedAnalyses();
                    }
                }
                else
                {
                    cancellation.Dispose();
                }
            }
        }

        private async Task<AnalysisLoadResult> AnalyzeForTreeAsync(
            AssetFile file,
            CancellationToken cancellationToken)
        {
            try
            {
                var analysis = await _manager.AnalyzeFileAsync(
                    file.Id,
                    cancellationToken);
                return new AnalysisLoadResult(file, analysis, null);
            }
            catch (Exception exception)
            {
                return new AnalysisLoadResult(file, null, exception);
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
            var loadingTitle = I18N.Get("fileTree.loading");
            var items = await Task.Run(
                () => AssetFileTreeBuilder.Build(
                    files,
                    analyses,
                    cancellation.Token,
                    overviewTitle,
                    overviewMeta,
                    loadingTitle,
                    includeOverview: !_showsTargetToggles,
                    groups: _groups),
                cancellation.Token);
            if (IsCurrentReload(version, cancellation))
            {
                if (_showsTargetToggles && _isPointerPressedOnTree)
                {
                    _pendingTreeItems = items;
                }
                else
                {
                    ApplyTreeItems(items, preserveSelection: true);
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
            ApplyTreeItems(items, preserveSelection: true);
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
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items,
            bool preserveSelection = false)
        {
            if (_unavailableTargetKeys.Count > 0)
            {
                items = (items ??
                         Array.Empty<SearchableTreeItemData<FileTreeNode>>())
                    .Select(FilterUnavailableTarget)
                    .Where(item => item != null)
                    .ToArray();
            }
            _fileTreeIds.Clear();
            CollectFileTreeIds(items);
            ConfigureTargetNodes(items);
            _suppressSelectionChanged = true;
            try
            {
                SetItems(
                    items,
                    preserveExpansion: true,
                    preserveSelection: preserveSelection);
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private SearchableTreeItemData<FileTreeNode>
            FilterUnavailableTarget(
                SearchableTreeItemData<FileTreeNode> item)
        {
            var node = item.Data;
            if (node?.File != null &&
                _unavailableTargetKeys.Contains(TargetKey(
                    node.File.Id,
                    node.Entry?.Path)))
            {
                return null;
            }

            var children = item.Children
                .Select(FilterUnavailableTarget)
                .Where(child => child != null)
                .ToArray();
            if (children.Length == 0 &&
                (node?.IsGroup == true ||
                 item.Children.Count > 0 &&
                 (node?.Entry?.Kind ==
                      AssetFileContentEntryKind.Directory ||
                  node?.File != null && node.Entry == null &&
                  IsZip(node.File))))
            {
                return null;
            }

            return new SearchableTreeItemData<FileTreeNode>(
                item.Id,
                node,
                item.SearchText,
                item.TooltipText,
                children);
        }

        private void CollectFileTreeIds(
            IReadOnlyList<SearchableTreeItemData<FileTreeNode>> items)
        {
            foreach (var item in items)
            {
                var node = item.Data;
                if (node?.File != null && node.Entry == null)
                {
                    _fileTreeIds[node.File.Id] = item.Id;
                }
                if (node?.IsGroup == true)
                {
                    CollectFileTreeIds(item.Children);
                }
            }
        }

        private static VisualElement CreateTreeItem()
        {
            var row = new ItemRow();
            row.AddToClassList(RowClassName);
            row.RegisterCallback<PointerEnterEvent>(evt =>
                FindOwningTree(row)?.OnImageRowPointerEnter(row, evt));
            row.RegisterCallback<PointerMoveEvent>(evt =>
                FindOwningTree(row)?.OnImageRowPointerMove(row, evt));
            row.RegisterCallback<PointerLeaveEvent>(_ =>
                FindOwningTree(row)?.OnImageRowPointerLeave(row));
            row.RegisterCallback<PointerUpEvent>(evt =>
                FindOwningTree(row)?.OnTargetRowPointerUp(row, evt));
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
            var owner = FindOwningTree(element);
            if (owner != null &&
                ReferenceEquals(element, owner._hoveredImageRow) &&
                !ReferenceEquals(node, owner._hoveredImageNode))
            {
                var pointerPosition = owner._hoveredPanelPosition;
                owner.HideImageTooltip();
                element.schedule.Execute(() =>
                {
                    if (owner.panel != null &&
                        element.panel != null &&
                        element.worldBound.Contains(pointerPosition) &&
                        ReferenceEquals(ResolveBoundNode(element), node))
                    {
                        owner.BeginImagePreview(
                            element,
                            node,
                            pointerPosition);
                    }
                });
            }

            element.tooltip = owner != null &&
                              !owner._showsTargetToggles &&
                              ResolveImageSource(node) != null
                ? string.Empty
                : node?.Title ?? string.Empty;
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
                        UiSizeTokens.Size16)));
            }
        }

        private static SearchableFileTree FindOwningTree(
            VisualElement element)
        {
            for (var parent = element?.parent;
                 parent != null;
                 parent = parent.parent)
            {
                if (parent is SearchableFileTree tree)
                {
                    return tree;
                }
            }

            return null;
        }

        private static FileTreeNode ResolveBoundNode(
            VisualElement row)
        {
            return (row?.userData as SearchableTreeItemData<FileTreeNode>)
                ?.Data;
        }

        private static FileTreeImageSource ResolveImageSource(
            FileTreeNode node)
        {
            if (node?.File == null)
            {
                return null;
            }

            if (node.Entry == null)
            {
                return FileTreeImageSource.FromFile(
                    node.File.FileName,
                    node.File.SourcePath);
            }

            return node.Entry.Kind == AssetFileContentEntryKind.File
                ? FileTreeImageSource.FromArchive(
                    Path.GetFileName(node.Entry.Path),
                    node.File.SourcePath,
                    node.Entry.Path,
                    node.Entry.AssetGuid)
                : null;
        }

        private void OnImageRowPointerEnter(
            VisualElement row,
            PointerEnterEvent evt)
        {
            BeginImagePreview(
                row,
                ResolveBoundNode(row),
                row.LocalToWorld(evt.localPosition));
        }

        private void BeginImagePreview(
            VisualElement row,
            FileTreeNode node,
            Vector2 panelPosition)
        {
            if (_showsTargetToggles)
            {
                return;
            }

            var source = ResolveImageSource(node);
            if (source == null)
            {
                return;
            }

            HideImageTooltip();
            _hoveredImageRow = row;
            _hoveredImageNode = node;
            _hoveredPanelPosition = panelPosition;
            if (_imagePreviewCache.TryGetValue(
                    source.CacheKey,
                    out var cachedTexture) && cachedTexture != null)
            {
                ShowImageTooltip(row, node, cachedTexture);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _imagePreviewCancellation = cancellation;
            _ = LoadImagePreviewAsync(
                row,
                node,
                source,
                ++_imagePreviewVersion,
                cancellation);
        }

        private async Task LoadImagePreviewAsync(
            VisualElement row,
            FileTreeNode node,
            FileTreeImageSource source,
            int version,
            CancellationTokenSource cancellation)
        {
            try
            {
                var preview = await Task.Run(
                    () => FileTreeImagePreviewLoader.Load(
                        source,
                        cancellation.Token),
                    cancellation.Token);
                if (version != _imagePreviewVersion ||
                    cancellation.IsCancellationRequested ||
                    !ReferenceEquals(row, _hoveredImageRow) ||
                    !ReferenceEquals(node, _hoveredImageNode) ||
                    row.panel == null)
                {
                    return;
                }

                if (preview == null)
                {
                    row.tooltip = node.Title;
                    return;
                }

                var texture = preview.CreateTexture();
                if (texture == null)
                {
                    row.tooltip = node.Title;
                    return;
                }

                CacheImagePreview(source.CacheKey, texture);
                ShowImageTooltip(row, node, texture);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (ReferenceEquals(row, _hoveredImageRow))
                {
                    row.tooltip = node.Title;
                }
            }
            finally
            {
                if (ReferenceEquals(_imagePreviewCancellation, cancellation))
                {
                    _imagePreviewCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void OnImageRowPointerMove(
            VisualElement row,
            PointerMoveEvent evt)
        {
            if (!ReferenceEquals(row, _hoveredImageRow))
            {
                return;
            }

            _hoveredPanelPosition = row.LocalToWorld(evt.localPosition);
            _imageTooltipWindow?.SetPointerPosition(
                row,
                _hoveredPanelPosition);
        }

        private void OnImageRowPointerLeave(VisualElement row)
        {
            if (ReferenceEquals(row, _hoveredImageRow))
            {
                HideImageTooltip();
            }
        }

        private void ShowImageTooltip(
            VisualElement row,
            FileTreeNode node,
            Texture2D texture)
        {
            var window = FileTreeImageTooltipWindow.Show(
                row,
                _hoveredPanelPosition,
                texture,
                node.Title);
            if (!ReferenceEquals(row, _hoveredImageRow))
            {
                window?.Close();
                return;
            }

            _imageTooltipWindow = window;
        }

        private void HideImageTooltip()
        {
            _imagePreviewVersion++;
            var cancellation = _imagePreviewCancellation;
            _imagePreviewCancellation = null;
            cancellation?.Cancel();
            if (_imageTooltipWindow != null)
            {
                _imageTooltipWindow.Close();
                _imageTooltipWindow = null;
            }

            _hoveredImageRow = null;
            _hoveredImageNode = null;
        }

        private void CacheImagePreview(string key, Texture2D texture)
        {
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

        private static Texture2D ResolveTreeIcon(FileTreeNode node)
        {
            var iconName = node?.IsLoading == true
                ? "arrow_clockwise.png"
                : node?.IsGroup == true
                ? "folder.png"
                : node == null || node.IsOverview
                    ? "info_16.png"
                    : node.Entry == null
                        ? "folder_zip_16.png"
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

            var selectedNodes = selection != null &&
                                selection.Count > 1 &&
                                selection.Contains(node)
                ? selection
                : new[] { node };
            var selectedTargets = selectedNodes
                .Select(selected => new FileTreeSelection(
                    selected.File,
                    selected.Entry))
                .Where(CanImport)
                .ToArray();
            var menu = new GenericMenu();
            var targetPath = node.Entry?.Path ?? string.Empty;
            var canImport = _importRequested != null &&
                            selectedTargets.Length > 0;
            if (canImport)
            {
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(
                        I18N.Get("action.import")),
                    false,
                    () => _importRequested(selectedTargets));
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
            var canSetTarget = selectedTargets.Length > 0;
            if (string.IsNullOrWhiteSpace(_itemId) || !canSetTarget)
            {
                menu.AddDisabledItem(UiTextFactory.CreateGuiContent(
                    I18N.Get("action.addTarget")));
            }
            else if (selectedNodes.Count > 1)
            {
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(
                        I18N.Get("action.addTarget")),
                    false,
                    () => SetTargets(
                        selectedTargets,
                        configuredTargets,
                        true));
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(
                        I18N.Get("action.removeTarget")),
                    false,
                    () => SetTargets(
                        selectedTargets,
                        configuredTargets,
                        false));
            }
            else
            {
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(I18N.Get(
                        configuredTarget == null
                            ? "action.addTarget"
                            : "action.removeTarget")),
                    false,
                    () => SetTargets(
                        selectedTargets,
                        configuredTargets,
                        configuredTarget == null));
            }

            menu.ShowAsContext();
        }

        private void SetTargets(
            IReadOnlyList<FileTreeSelection> selections,
            IReadOnlyList<AssetFileTarget> configuredTargets,
            bool add)
        {
            var existingTargets = configuredTargets ??
                Array.Empty<AssetFileTarget>();
            var selectedTargets = selections
                .Select(selection => new AssetFileTarget
                {
                    FileId = selection.File.Id,
                    TargetPath = selection.Entry?.Path ?? string.Empty
                })
                .ToArray();
            _manager.SetItemTargets(
                _itemId,
                (add
                    ? existingTargets
                        .Concat(selectedTargets.Where(target =>
                            !existingTargets.Any(existing =>
                                AssetFileTarget.HasSameIdentity(
                                    existing,
                                    target))))
                    : existingTargets
                        .Where(existing => !selectedTargets.Any(target =>
                            AssetFileTarget.HasSameIdentity(
                                existing,
                                target))))
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

        private void OnTargetRowPointerUp(
            VisualElement row,
            PointerUpEvent evt)
        {
            if (!_showsTargetToggles ||
                evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            var node = ResolveBoundNode(row);
            if (node?.ShowsTargetToggle != true)
            {
                return;
            }

            var toggle = row.Q<Toggle>(TargetToggleElementName);
            for (var target = evt.target as VisualElement;
                 target != null && !ReferenceEquals(target, row);
                 target = target.parent)
            {
                if (ReferenceEquals(target, toggle))
                {
                    return;
                }
            }

            OnTargetChanged(node, !node.IsTarget);
            toggle?.SetValueWithoutNotify(node.IsTarget);
        }

        private bool CanSetTarget(FileTreeNode node)
        {
            return CanImport(node) &&
                   !_unavailableTargetKeys.Contains(TargetKey(
                       node.File.Id,
                       node.Entry?.Path));
        }

        internal static bool CanImport(FileTreeSelection selection)
        {
            if (selection?.File == null)
            {
                return false;
            }

            return selection.Entry == null
                ? !IsZip(selection.File)
                : selection.Entry.Kind == AssetFileContentEntryKind.File &&
                  !string.Equals(
                      Path.GetExtension(selection.Entry.Path),
                      ".zip",
                      StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanImport(FileTreeNode node)
        {
            return node != null && CanImport(
                new FileTreeSelection(node.File, node.Entry));
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

            SelectionChanged?.Invoke((selection ?? Array.Empty<FileTreeNode>())
                .Select(node => new FileTreeSelection(node.File, node.Entry))
                .ToArray());
        }

        private void CancelReload()
        {
            _reloadVersion++;
            _pendingAnalyses = new Queue<AssetFile>();
            _requestedAnalysisIds =
                new HashSet<string>(StringComparer.Ordinal);
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

        private sealed class AnalysisLoadResult
        {
            internal AnalysisLoadResult(
                AssetFile file,
                AssetFileAnalysis analysis,
                Exception error)
            {
                File = file;
                Analysis = analysis;
                Error = error;
            }

            internal AssetFile File { get; }
            internal AssetFileAnalysis Analysis { get; }
            internal Exception Error { get; }
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
