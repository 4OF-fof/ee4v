using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class ArchiveFileDetailView :
        VisualElement
    {
        private const string RootClassName =
            "ee4v-asset-manager-archive-detail";
        private const string HeaderClassName =
            "ee4v-asset-manager-archive-detail__header";
        private const string NameClassName =
            "ee4v-asset-manager-archive-detail__name";
        private const string BodyClassName =
            "ee4v-asset-manager-archive-detail__body";
        private const string TreePaneClassName =
            "ee4v-asset-manager-archive-detail__tree-pane";
        private const string TreeClassName =
            "ee4v-asset-manager-archive-detail__tree";
        private const string PreviewPaneClassName =
            "ee4v-asset-manager-archive-detail__preview";
        private const string PreviewTitleClassName =
            "ee4v-asset-manager-archive-detail__preview-title";
        private const string PreviewNameClassName =
            "ee4v-asset-manager-archive-detail__preview-name";
        private const string PreviewContentClassName =
            "ee4v-asset-manager-archive-detail__preview-content";
        private const string PreviewImageClassName =
            "ee4v-asset-manager-archive-detail__preview-image";
        private const string PreviewIconClassName =
            "ee4v-asset-manager-archive-detail__preview-icon";
        private const string PreviewStatusClassName =
            "ee4v-asset-manager-archive-detail__preview-status";
        private readonly UiTextElement _name;
        private readonly VisualElement _body;
        private readonly ErrorScreen _errorScreen;
        private readonly SearchableTreeView<
            ArchiveFileDetailNode> _tree;
        private readonly UiTextElement _previewName;
        private readonly Image _previewImage;
        private readonly Icon _previewIcon;
        private readonly UiTextElement _previewStatus;
        private CancellationTokenSource _loadCancellation;
        private CancellationTokenSource _previewCancellation;
        private IAssetArchiveReader _archiveReader;
        private IAssetManagerUiScheduler _scheduler;
        private AssetArchiveContentKind _archiveKind;
        private string _archivePath = string.Empty;
        private string _packageEntryPath = string.Empty;
        private Texture2D _previewTexture;
        private int _loadVersion;
        private int _previewVersion;

        internal ArchiveFileDetailView()
        {
            AddToClassList(RootClassName);

            var header = new VisualElement();
            header.AddToClassList(HeaderClassName);
            _name = UiTextFactory.Create(
                string.Empty,
                NameClassName,
                UiClassNames.FileTreeDetailName);
            _name.SetWhiteSpace(WhiteSpace.Normal);
            header.Add(_name);
            Add(header);

            _body = new VisualElement();
            _body.AddToClassList(BodyClassName);

            var treePane = new VisualElement();
            treePane.AddToClassList(TreePaneClassName);
            var searchTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.search.tooltip");
            var clearTooltip =
                I18N.GetForScope(
                    "UI",
                    "ui.clear.tooltip");
            _tree =
                new SearchableTreeView<
                    ArchiveFileDetailNode>(
                    CreateTreeRow,
                    BindTreeRow,
                    OnTreeSelectionChanged,
                    emptyText: I18N.Get(
                        "assetManager.archiveDetail.empty"),
                    searchPlaceholder: I18N.Get(
                        "assetManager.archiveDetail.searchPlaceholder"),
                    searchTooltip: searchTooltip,
                    clearTooltip: clearTooltip,
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
            _tree.AddToClassList(TreeClassName);
            _tree.SetViewDataKey(
                "ee4v-asset-manager-archive-detail-tree");
            treePane.Add(_tree);
            _body.Add(treePane);

            var previewPane = new VisualElement();
            previewPane.AddToClassList(
                PreviewPaneClassName);
            previewPane.Add(
                UiTextFactory.Create(
                    I18N.Get(
                        "assetManager.archiveDetail.preview"),
                    PreviewTitleClassName,
                    UiClassNames.SectionTitle));
            _previewName = UiTextFactory.Create(
                string.Empty,
                PreviewNameClassName,
                UiClassNames.NavigationItemLabel);
            _previewName.SetWhiteSpace(
                WhiteSpace.Normal);
            previewPane.Add(_previewName);

            var previewContent = new VisualElement();
            previewContent.AddToClassList(
                PreviewContentClassName);
            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            _previewImage.AddToClassList(
                PreviewImageClassName);
            _previewIcon = new Icon();
            _previewIcon.AddToClassList(
                PreviewIconClassName);
            _previewStatus = UiTextFactory.Create(
                string.Empty,
                PreviewStatusClassName,
                UiClassNames.InfoCardDescription);
            _previewStatus.SetWhiteSpace(
                WhiteSpace.Normal);
            previewContent.Add(_previewImage);
            previewContent.Add(_previewIcon);
            previewContent.Add(_previewStatus);
            previewPane.Add(previewContent);
            _body.Add(previewPane);
            Add(_body);

            _errorScreen = new ErrorScreen();
            _errorScreen.style.display = DisplayStyle.None;
            Add(_errorScreen);

            RegisterCallback<DetachFromPanelEvent>(
                _ =>
                {
                    CancelLoad();
                    CancelPreview();
                    ReleasePreviewTexture();
                });
            ClearContent();
        }

        internal void SetState(FileTreeDetailState state)
        {
            CancelLoad();
            CancelPreview();
            ReleasePreviewTexture();
            ClearContent();
            if (state == null)
            {
                return;
            }

            _name.SetText(state.Name);
            if (state.ArchiveContent != null)
            {
                SetContent(state.ArchiveContent);
                return;
            }

            IAssetManager assetManager;
            IAssetArchiveReader archiveReader;
            IAssetManagerUiScheduler scheduler;
            if (!AssetManagerUiDependencies
                    .TryGetArchiveDetailDependencies(
                        out assetManager,
                        out archiveReader,
                        out scheduler))
            {
                SetError();
                return;
            }

            var sourcePath =
                state.SourceArchivePath;
            var sourceEntryPath =
                state.SourceArchiveEntryPath;
            if (!state.HasArchiveEntrySource)
            {
                if (string.IsNullOrWhiteSpace(
                        state.AssetFileId))
                {
                    SetError();
                    return;
                }

                AssetFilePathResolution resolution;
                try
                {
                    resolution =
                        assetManager.ResolveFilePath(
                            state.AssetFileId);
                }
                catch (Exception)
                {
                    SetError();
                    return;
                }

                if (resolution == null ||
                    !resolution.Found ||
                    string.IsNullOrWhiteSpace(
                        resolution.Path))
                {
                    SetError();
                    return;
                }

                sourcePath = resolution.Path;
                sourceEntryPath = string.Empty;
            }

            _archiveReader = archiveReader;
            _scheduler = scheduler;
            _archivePath = sourcePath;
            _packageEntryPath = sourceEntryPath;
            SetLoading();
            var cancellation =
                new CancellationTokenSource();
            _loadCancellation = cancellation;
            var version = ++_loadVersion;
            scheduler.RunInBackground(
                token => ReadContent(
                    archiveReader,
                    state.Extension,
                    sourcePath,
                    sourceEntryPath,
                    token),
                cancellation.Token,
                result =>
                {
                    if (ReferenceEquals(
                            _loadCancellation,
                            cancellation))
                    {
                        _loadCancellation = null;
                    }

                    if (version != _loadVersion ||
                        cancellation
                            .IsCancellationRequested ||
                        result.Canceled)
                    {
                        cancellation.Dispose();
                        return;
                    }

                    cancellation.Dispose();
                    if (result.Error != null ||
                        result.Value == null)
                    {
                        SetError();
                        return;
                    }

                    SetContent(result.Value);
                });
        }

        private static AssetArchiveContent ReadContent(
            IAssetArchiveReader archiveReader,
            string extension,
            string path,
            string entryPath,
            CancellationToken cancellationToken)
        {
            if (string.Equals(
                    FileExtensionUtility.Normalize(
                        extension),
                    "unitypackage",
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(
                    entryPath)
                    ? archiveReader
                        .ReadUnityPackageContent(
                            path,
                            cancellationToken)
                    : archiveReader
                        .ReadUnityPackageContentFromZip(
                            path,
                            entryPath,
                            cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(entryPath))
            {
                throw new InvalidDataException(
                    "Nested ZIP details are not supported.");
            }

            return archiveReader.ReadZipContent(
                path,
                cancellationToken);
        }

        private void SetContent(
            AssetArchiveContent content)
        {
            _archiveKind = content.Kind;
            _tree.SetItems(
                ArchiveFileDetailTreeBuilder.Build(
                    content.Entries));
            _body.style.display =
                DisplayStyle.Flex;
            _errorScreen.style.display =
                DisplayStyle.None;
            SetPreviewPlaceholder(
                null,
                I18N.Get(
                    "assetManager.archiveDetail.previewSelect"));
        }

        private void ClearContent()
        {
            _name.SetText(string.Empty);
            _archiveReader = null;
            _scheduler = null;
            _archivePath = string.Empty;
            _packageEntryPath = string.Empty;
            _tree.SetItems(null);
            _body.style.display =
                DisplayStyle.None;
            _errorScreen.style.display =
                DisplayStyle.None;
            SetPreviewPlaceholder(null, string.Empty);
        }

        private void SetLoading()
        {
            _body.style.display =
                DisplayStyle.None;
            _errorScreen.SetState(new ErrorScreenState(
                I18N.Get(
                    "assetManager.archiveDetail.loading"),
                ErrorScreenKind.Loading));
            _errorScreen.style.display =
                DisplayStyle.Flex;
        }

        private void SetError()
        {
            _body.style.display =
                DisplayStyle.None;
            _errorScreen.SetState(new ErrorScreenState(
                I18N.Get(
                    "assetManager.archiveDetail.loadFailed"),
                ErrorScreenKind.Error));
            _errorScreen.style.display =
                DisplayStyle.Flex;
        }

        private void OnTreeSelectionChanged(
            IReadOnlyList<ArchiveFileDetailNode>
                selected)
        {
            CancelPreview();
            ReleasePreviewTexture();
            var node =
                selected != null &&
                selected.Count == 1
                    ? selected[0]
                    : null;
            if (node == null)
            {
                SetPreviewPlaceholder(
                    null,
                    I18N.Get(
                        "assetManager.archiveDetail.previewSelect"));
                return;
            }

            if (node.Kind !=
                AssetArchiveContentEntryKind.File)
            {
                SetPreviewPlaceholder(
                    node,
                    I18N.Get(
                        "assetManager.archiveDetail.previewSelectFile"));
                return;
            }

            if (!FileTreeImageSource.IsSupported(
                    node.Path))
            {
                SetPreviewPlaceholder(
                    node,
                    I18N.Get(
                        "assetManager.archiveDetail.previewUnavailable"));
                return;
            }

            if (_archiveReader == null ||
                _scheduler == null ||
                string.IsNullOrWhiteSpace(
                    _archivePath) ||
                string.IsNullOrWhiteSpace(
                    node.SourcePath))
            {
                SetPreviewPlaceholder(
                    node,
                    I18N.Get(
                        "assetManager.archiveDetail.previewUnavailable"));
                return;
            }

            SetPreviewLoading();
            var cancellation =
                new CancellationTokenSource();
            _previewCancellation = cancellation;
            var version = ++_previewVersion;
            var archiveReader = _archiveReader;
            var archiveKind = _archiveKind;
            var archivePath = _archivePath;
            var packageEntryPath =
                _packageEntryPath;
            _scheduler.RunInBackground(
                token => ReadPreview(
                    archiveReader,
                    archiveKind,
                    archivePath,
                    packageEntryPath,
                    node,
                    token),
                cancellation.Token,
                result =>
                {
                    if (ReferenceEquals(
                            _previewCancellation,
                            cancellation))
                    {
                        _previewCancellation = null;
                    }

                    if (version != _previewVersion ||
                        cancellation
                            .IsCancellationRequested ||
                        result.Canceled)
                    {
                        cancellation.Dispose();
                        return;
                    }

                    cancellation.Dispose();
                    if (result.Error != null ||
                        result.Value == null)
                    {
                        SetPreviewPlaceholder(
                            node,
                            I18N.Get(
                                "assetManager.archiveDetail.previewUnavailable"));
                        return;
                    }

                    try
                    {
                        var texture =
                            result.Value.CreateTexture();
                        if (texture == null)
                        {
                            SetPreviewPlaceholder(
                                node,
                                I18N.Get(
                                    "assetManager.archiveDetail.previewUnavailable"));
                            return;
                        }

                        SetPreviewTexture(
                            node,
                            texture);
                    }
                    catch (Exception)
                    {
                        SetPreviewPlaceholder(
                            node,
                            I18N.Get(
                                "assetManager.archiveDetail.previewUnavailable"));
                    }
                });
        }

        private static FileTreeImagePreviewData
            ReadPreview(
            IAssetArchiveReader archiveReader,
            AssetArchiveContentKind archiveKind,
            string archivePath,
            string packageEntryPath,
            ArchiveFileDetailNode node,
            CancellationToken cancellationToken)
        {
            var bytes = archiveReader.ReadEntryBytes(
                archiveKind,
                archivePath,
                packageEntryPath,
                node.SourcePath,
                FileTreeImagePreviewLoader
                    .MaximumEncodedBytes,
                cancellationToken);
            return FileTreeImagePreviewLoader.Decode(
                node.Path,
                bytes,
                cancellationToken);
        }

        private void SetPreviewTexture(
            ArchiveFileDetailNode node,
            Texture2D texture)
        {
            ReleasePreviewTexture();
            _previewTexture = texture;
            _previewName.SetText(node.Name);
            _previewImage.image = texture;
            _previewImage.style.display =
                DisplayStyle.Flex;
            _previewIcon.style.display =
                DisplayStyle.None;
            _previewStatus.SetText(string.Empty);
            _previewStatus.style.display =
                DisplayStyle.None;
        }

        private void SetPreviewPlaceholder(
            ArchiveFileDetailNode node,
            string message)
        {
            _previewName.SetText(
                node == null
                    ? string.Empty
                    : node.Name);
            _previewImage.image = null;
            _previewImage.style.display =
                DisplayStyle.None;
            _previewIcon.SetState(
                FileIconCatalog.Resolve(
                        node == null ||
                        node.Kind ==
                        AssetArchiveContentEntryKind
                            .Directory
                            ? FileEntryKind.Directory
                            : FileEntryKind.File,
                        node == null
                            ? string.Empty
                            : node.Path)
                    .CreateArtworkIconState());
            _previewIcon.style.display =
                node == null
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            _previewStatus.SetText(message);
            _previewStatus.style.display =
                string.IsNullOrWhiteSpace(message)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
        }

        internal void SetPreviewLoading()
        {
            _previewName.SetText(string.Empty);
            _previewImage.image = null;
            _previewImage.style.display =
                DisplayStyle.None;
            _previewIcon.style.display =
                DisplayStyle.None;
            _previewStatus.SetText(string.Empty);
            _previewStatus.style.display =
                DisplayStyle.None;
        }

        private void CancelLoad()
        {
            _loadVersion++;
            Cancel(
                ref _loadCancellation);
        }

        private void CancelPreview()
        {
            _previewVersion++;
            Cancel(
                ref _previewCancellation);
        }

        private static void Cancel(
            ref CancellationTokenSource cancellation)
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
                // Completion can dispose the source first.
            }
        }

        private void ReleasePreviewTexture()
        {
            _previewImage.image = null;
            if (_previewTexture == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(
                _previewTexture);
            _previewTexture = null;
        }

        internal static VisualElement CreateTreeRow()
        {
            var row = SearchableFileTree.CreateRow();
            var meta = row.Q<UiTextElement>(
                className:
                SearchableFileTree.RowMetaClassName);
            meta.AddToClassList(
                SearchableFileTree.RowEmptyMetaClassName);
            return row;
        }

        private static void BindTreeRow(
            VisualElement element,
            ArchiveFileDetailNode node)
        {
            element.Q<UiTextElement>(
                    className:
                    SearchableFileTree.RowTitleClassName)
                ?.SetText(node.Name);
        }
    }

    internal sealed class ArchiveFileDetailNode
    {
        internal ArchiveFileDetailNode(
            string name,
            string path,
            AssetArchiveContentEntryKind kind,
            string sourcePath)
        {
            Name = name ?? string.Empty;
            Path = path ?? string.Empty;
            Kind = kind;
            SourcePath = sourcePath ?? string.Empty;
        }

        internal string Name { get; }
        internal string Path { get; }
        internal AssetArchiveContentEntryKind Kind
        {
            get;
        }
        internal string SourcePath { get; }
    }

    internal static class ArchiveFileDetailTreeBuilder
    {
        internal static IReadOnlyList<
                SearchableTreeItemData<
                    ArchiveFileDetailNode>>
            Build(
                IReadOnlyList<
                    AssetArchiveContentEntry> entries)
        {
            var root =
                new MutableArchiveNode(
                    string.Empty,
                    string.Empty,
                    true);
            if (entries != null)
            {
                for (var i = 0;
                     i < entries.Count;
                     i++)
                {
                    Add(root, entries[i]);
                }
            }

            var nextId = 1;
            return root.CreateChildren(
                () => nextId++);
        }

        private static void Add(
            MutableArchiveNode root,
            AssetArchiveContentEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var path = NormalizePath(entry.Path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var parts = path.Split(
                new[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            var currentPath = string.Empty;
            for (var i = 0;
                 i < parts.Length;
                 i++)
            {
                currentPath =
                    string.IsNullOrEmpty(currentPath)
                        ? parts[i]
                        : currentPath + "/" + parts[i];
                var isLast =
                    i == parts.Length - 1;
                current = current.GetOrCreateChild(
                    parts[i],
                    currentPath,
                    !isLast ||
                    entry.Kind ==
                    AssetArchiveContentEntryKind
                        .Directory);
                if (isLast)
                {
                    current.SetEntry(entry);
                }
            }
        }

        private static string NormalizePath(
            string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private sealed class MutableArchiveNode
        {
            private readonly Dictionary<
                    string,
                    MutableArchiveNode>
                _childrenByName =
                    new Dictionary<
                        string,
                        MutableArchiveNode>(
                        StringComparer
                            .OrdinalIgnoreCase);
            private readonly List<
                    MutableArchiveNode>
                _children =
                    new List<MutableArchiveNode>();
            private bool _isDirectory;
            private string _sourcePath =
                string.Empty;

            internal MutableArchiveNode(
                string name,
                string path,
                bool isDirectory)
            {
                Name = name ?? string.Empty;
                Path = path ?? string.Empty;
                _isDirectory = isDirectory;
            }

            private string Name { get; }
            private string Path { get; }

            internal MutableArchiveNode
                GetOrCreateChild(
                    string name,
                    string path,
                    bool isDirectory)
            {
                MutableArchiveNode child;
                if (_childrenByName.TryGetValue(
                        name,
                        out child))
                {
                    child._isDirectory |=
                        isDirectory;
                    return child;
                }

                child = new MutableArchiveNode(
                    name,
                    path,
                    isDirectory);
                _childrenByName.Add(name, child);
                _children.Add(child);
                return child;
            }

            internal void SetEntry(
                AssetArchiveContentEntry entry)
            {
                _isDirectory =
                    entry.Kind ==
                    AssetArchiveContentEntryKind
                        .Directory;
                _sourcePath = entry.SourcePath;
            }

            internal IReadOnlyList<
                    SearchableTreeItemData<
                        ArchiveFileDetailNode>>
                CreateChildren(Func<int> nextId)
            {
                var result =
                    new List<
                        SearchableTreeItemData<
                            ArchiveFileDetailNode>>(
                        _children.Count);
                for (var i = 0;
                     i < _children.Count;
                     i++)
                {
                    result.Add(
                        _children[i].CreateItem(
                            nextId));
                }

                return result;
            }

            private SearchableTreeItemData<
                    ArchiveFileDetailNode>
                CreateItem(Func<int> nextId)
            {
                var kind = _isDirectory
                    ? AssetArchiveContentEntryKind
                        .Directory
                    : AssetArchiveContentEntryKind
                        .File;
                var node =
                    new ArchiveFileDetailNode(
                        Name,
                        Path,
                        kind,
                        _sourcePath);
                return new SearchableTreeItemData<
                    ArchiveFileDetailNode>(
                    nextId(),
                    node,
                    Path,
                    Path,
                    CreateChildren(nextId));
            }
        }
    }
}
