using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class SearchableFileTree : VisualElement, IDisposable
    {
        private const string RootClassName =
            "ee4v-asset-manager-file-tree";
        internal const string RowClassName =
            "ee4v-asset-manager-file-tree__row";
        internal const string RowTitleClassName =
            "ee4v-asset-manager-file-tree__title";
        internal const string RowMetaClassName =
            "ee4v-asset-manager-file-tree__meta";
        private const string TitleElementName = "file-tree-title";
        private const string MetaElementName = "file-tree-meta";

        private readonly IAssetManager _manager;
        private readonly SearchableTreeView<FileTreeNode> _treeView;
        private readonly UiTextElement _feedback;
        private readonly Dictionary<string, CachedAnalysis> _analysisCache =
            new Dictionary<string, CachedAnalysis>(StringComparer.Ordinal);
        private CancellationTokenSource _reloadCancellation;
        private IReadOnlyList<AssetFile> _files = Array.Empty<AssetFile>();
        private string _itemId = string.Empty;
        private int _reloadVersion;
        private bool _suppressSelectionChanged;

        internal SearchableFileTree(IAssetManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            AddToClassList(RootClassName);

            var searchTooltip = I18N.Get("fileTree.searchTooltip");
            var clearTooltip = I18N.Get("toolbar.search.clear");
            _treeView = new SearchableTreeView<FileTreeNode>(
                CreateTreeItem,
                BindTreeItem,
                OnTreeSelectionChanged,
                I18N.Get("fileTree.empty"),
                I18N.Get("fileTree.searchPlaceholder"),
                SelectionType.Single,
                searchTooltip: searchTooltip,
                clearTooltip: clearTooltip,
                searchIconState: AssetManagerControls.LoadFluentIconState(
                    "search.png",
                    UiSizeTokens.Size14,
                    searchTooltip),
                clearIconState: AssetManagerControls.LoadFluentIconState(
                    "dismiss.png",
                    UiSizeTokens.Size10,
                    clearTooltip));
            _treeView.SetViewDataKey(
                "ee4v-asset-manager-item-detail-file-tree");
            Add(_treeView);

            _feedback = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormError,
                RootClassName + "__feedback");
            _feedback.SetWhiteSpace(WhiteSpace.Normal);
            _feedback.style.display = DisplayStyle.None;
            Add(_feedback);

            RegisterCallback<DetachFromPanelEvent>(_ => CancelReload());
        }

        internal event Action<FileTreeSelection> SelectionChanged;

        internal void SetItem(
            string itemId,
            IReadOnlyList<AssetFile> files)
        {
            CancelReload();
            var nextItemId = itemId ?? string.Empty;
            if (!string.Equals(_itemId, nextItemId, StringComparison.Ordinal))
            {
                _analysisCache.Clear();
            }
            _itemId = nextItemId;
            _files = (files ?? Array.Empty<AssetFile>())
                .Where(file => file != null)
                .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.Id, StringComparer.Ordinal)
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
            _feedback.SetText(string.Empty);
            _feedback.style.display = DisplayStyle.None;
            ApplyTreeItems(AssetFileTreeBuilder.Build(
                _files,
                null,
                CancellationToken.None));

            if (!_files.Any(AssetFileTreeBuilder.CanAnalyze))
            {
                return;
            }

            var version = ++_reloadVersion;
            var cancellation = new CancellationTokenSource();
            _reloadCancellation = cancellation;
            LoadAnalysesAsync(version, cancellation, _files);
        }

        public void Dispose()
        {
            CancelReload();
        }

        private async void LoadAnalysesAsync(
            int version,
            CancellationTokenSource cancellation,
            IReadOnlyList<AssetFile> files)
        {
            var analyses = new Dictionary<string, AssetFileAnalysis>(
                StringComparer.Ordinal);
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
                    var cacheKey = CreateCacheKey(file);
                    if (_analysisCache.TryGetValue(file.Id, out var cached) &&
                        string.Equals(
                            cached.Version,
                            cacheKey,
                            StringComparison.Ordinal))
                    {
                        analyses[file.Id] = cached.Analysis;
                        continue;
                    }

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

                var items = await Task.Run(
                    () => AssetFileTreeBuilder.Build(
                        files,
                        analyses,
                        cancellation.Token),
                    cancellation.Token);
                if (!IsCurrentReload(version, cancellation))
                {
                    return;
                }

                ApplyTreeItems(items);
                if (failures.Count > 0)
                {
                    _feedback.SetText(string.Format(
                        I18N.Get("fileTree.analysisFailed"),
                        string.Join(", ", failures)));
                    _feedback.style.display = DisplayStyle.Flex;
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
                    _feedback.SetText(I18N.Get("fileTree.analysisUnavailable"));
                    _feedback.style.display = DisplayStyle.Flex;
                }
            }
            finally
            {
                if (ReferenceEquals(_reloadCancellation, cancellation))
                {
                    cancellation.Dispose();
                    _reloadCancellation = null;
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
            _suppressSelectionChanged = true;
            try
            {
                _treeView.SetItems(
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
            var row = new VisualElement();
            row.AddToClassList(RowClassName);
            var title = UiTextFactory.Create(
                string.Empty,
                RowTitleClassName);
            title.name = TitleElementName;
            title.SetWhiteSpace(WhiteSpace.NoWrap);
            row.Add(title);
            var meta = UiTextFactory.Create(
                string.Empty,
                RowMetaClassName);
            meta.name = MetaElementName;
            meta.SetWhiteSpace(WhiteSpace.NoWrap);
            row.Add(meta);
            return row;
        }

        private static void BindTreeItem(
            VisualElement element,
            FileTreeNode node)
        {
            element.Q<UiTextElement>(TitleElementName)?.SetText(
                node?.Title ?? string.Empty);
            var meta = element.Q<UiTextElement>(MetaElementName);
            meta?.SetText(node?.Meta ?? string.Empty);
            if (meta != null)
            {
                meta.style.display = string.IsNullOrEmpty(node?.Meta)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
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
            _reloadCancellation.Dispose();
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
