using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.Core.Images;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView : VisualElement, IDisposable
    {
        private const string TargetDragDataKey =
            "ee4v.asset-manager.target-drag";
        private const string CollectionDragDataKey =
            "ee4v.asset-manager.collection-drag";
        private const int MaximumConcurrentGridThumbnails = 4;
        private const float DerivedAssetCardWidth = 144f;

        private sealed class CollectionDragPayload
        {
            internal IAssetManager Manager { get; set; }
            internal string CollectionId { get; set; }
            internal string Name { get; set; }
        }

        private sealed class CollectionDragManipulator : PointerManipulator
        {
            private readonly Func<CollectionDragPayload> _createPayload;
            private Vector2 _start;
            private bool _ready;
            private bool _dragging;

            internal CollectionDragManipulator(
                Func<CollectionDragPayload> createPayload)
            {
                _createPayload = createPayload;
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(
                    OnPointerDown, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerMoveEvent>(
                    OnPointerMove, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerUpEvent>(
                    OnPointerUp, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(
                    OnPointerDown, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerMoveEvent>(
                    OnPointerMove, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerUpEvent>(
                    OnPointerUp, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                _ready = evt.button == (int)MouseButton.LeftMouse;
                _dragging = false;
                _start = evt.position;
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!_ready || (evt.pressedButtons & 1) == 0 ||
                    Vector2.Distance(_start, evt.position) < 4f)
                {
                    return;
                }
                _ready = false;
                var payload = _createPayload();
                _dragging = true;
                target.ReleasePointer(evt.pointerId);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(CollectionDragDataKey, payload);
                DragAndDrop.StartDrag(payload.Name);
                evt.StopImmediatePropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                _ready = false;
                if (_dragging)
                {
                    _dragging = false;
                    evt.StopImmediatePropagation();
                }
            }

            private void OnCaptureOut(PointerCaptureOutEvent evt)
            {
                _ready = false;
            }
        }

        private sealed class ItemTargetEntry
        {
            internal AssetFile File { get; set; }
            internal AssetFileTarget Target { get; set; }
        }

        private sealed class TargetImportChoice
        {
            internal string Label { get; set; }
            internal AssetFileTarget Target { get; set; }
        }

        private sealed class TargetImportGroup
        {
            internal string Name { get; set; }
            internal IReadOnlyList<TargetImportChoice> Choices { get; set; }
        }

        private sealed class TargetImportPopup : CustomPopupWindow
        {
            private IReadOnlyList<TargetImportGroup> _groups;
            private Action<IReadOnlyList<AssetFileTarget>> _import;
            private readonly List<PopupField<TargetImportChoice>> _fields =
                new List<PopupField<TargetImportChoice>>();

            internal static void Show(
                VisualElement anchor,
                IReadOnlyList<TargetImportGroup> groups,
                Action<IReadOnlyList<AssetFileTarget>> import)
            {
                if (anchor == null || groups == null || import == null)
                {
                    return;
                }

                var window = CreateInstance<TargetImportPopup>();
                window._groups = groups;
                window._import = import;
                window.ShowAsPopup(
                    anchor,
                    new Vector2(
                        480f,
                        Mathf.Min(420f, 116f + (groups.Count * 48f))));
            }

            private void CreateGUI()
            {
                var root = rootVisualElement;
                root.Clear();
                AssetManagerWindowSession.PrepareRoot(root);
                root.AddToClassList("ee4v-asset-manager");
                ConfigureCloseAndSubmitKeys(root, Submit);

                var popup = new CustomPopup(
                    I18N.Get("action.import"),
                    showFooter: true,
                    closeTooltip: I18N.Get("action.cancel"));
                var content = new ScrollView(ScrollViewMode.Vertical);
                content.AddToClassList(
                    "ee4v-asset-manager__target-import-popup-content");
                var note = UiTextFactory.Create(
                    I18N.Get("detail.targetImportChoiceNote"),
                    UiClassNames.SecondaryText,
                    "ee4v-asset-manager__target-import-popup-note");
                note.SetWhiteSpace(WhiteSpace.Normal);
                content.Add(note);

                _fields.Clear();
                for (var index = 0; index < _groups.Count; index++)
                {
                    var choices = _groups[index].Choices.ToList();
                    var field = UiTextFactory.CreatePopupField(
                        _groups[index].Name,
                        choices,
                        0,
                        FormatChoice,
                        FormatChoice,
                        "ee4v-asset-manager__target-import-popup-field");
                    _fields.Add(field);
                    content.Add(field);
                }
                popup.Content.Add(content);
                popup.Footer.Add(AssetManagerControls.CreateButton(
                    I18N.Get("action.cancel"),
                    Close));
                popup.Footer.Add(AssetManagerControls.CreateButton(
                    I18N.Get("action.import"),
                    Submit,
                    "ee4v-asset-manager__primary-action"));
                SetPopup(popup);
            }

            private static string FormatChoice(TargetImportChoice choice)
            {
                return choice?.Label ?? string.Empty;
            }

            private void Submit()
            {
                var selections = _fields
                    .Select(field => field.value?.Target)
                    .Where(target => target != null)
                    .ToArray();
                Close();
                _import(selections);
            }
        }

        private sealed class TargetEditorPopup : CustomPopupWindow
        {
            private IAssetManager _manager;
            private string _itemId;
            private string _title;
            private string _saveLabel;
            private IReadOnlyList<AssetFile> _files;
            private IReadOnlyList<AssetFileTarget> _targets;
            private IReadOnlyList<AssetFileTarget> _unavailableTargets;
            private IReadOnlyDictionary<string, AssetFileAnalysis>
                _initialAnalyses;
            private IReadOnlyList<FileTreeGroup> _groups;
            private Action<IReadOnlyList<AssetFileTarget>> _save;
            private SearchableFileTree _tree;

            internal static void Show(
                VisualElement anchor,
                IAssetManager manager,
                string itemId,
                IReadOnlyList<AssetFile> files,
                IReadOnlyList<AssetFileTarget> targets,
                string title,
                string saveLabel,
                Action<IReadOnlyList<AssetFileTarget>> save,
                IReadOnlyDictionary<string, AssetFileAnalysis>
                    initialAnalyses = null,
                IReadOnlyList<FileTreeGroup> groups = null,
                IReadOnlyList<AssetFileTarget> unavailableTargets = null)
            {
                if (anchor == null || manager == null || save == null)
                {
                    return;
                }

                var window = CreateInstance<TargetEditorPopup>();
                window._manager = manager;
                window._itemId = itemId;
                window._title = title;
                window._saveLabel = saveLabel;
                window._files = files ?? Array.Empty<AssetFile>();
                window._targets = targets ??
                                  Array.Empty<AssetFileTarget>();
                window._unavailableTargets = unavailableTargets;
                window._initialAnalyses = initialAnalyses;
                window._groups = groups ?? Array.Empty<FileTreeGroup>();
                window._save = save;
                window.ShowAsPopup(
                    anchor,
                    new Vector2(520f, 560f));
            }

            private void CreateGUI()
            {
                var root = rootVisualElement;
                root.Clear();
                AssetManagerWindowSession.PrepareRoot(root);
                root.AddToClassList("ee4v-asset-manager");
                ConfigureCloseAndSubmitKeys(root);

                var popup = new CustomPopup(
                    _title,
                    showFooter: true,
                    closeTooltip: I18N.Get("action.cancel"));
                _tree = new SearchableFileTree(
                    _manager,
                    showTargetToggles: true,
                    unavailableTargets: _unavailableTargets);
                _tree.AddToClassList(
                    "ee4v-asset-manager__item-target-picker-tree");
                _tree.SetItem(
                    _itemId,
                    _files,
                    _targets,
                    _initialAnalyses,
                    _groups);
                popup.Content.Add(_tree);
                popup.Footer.Add(AssetManagerControls.CreateButton(
                    I18N.Get("action.cancel"),
                    Close));
                popup.Footer.Add(AssetManagerControls.CreateButton(
                    _saveLabel,
                    Submit,
                    "ee4v-asset-manager__primary-action"));
                SetPopup(popup);
            }

            protected override void OnDisable()
            {
                base.OnDisable();
                _tree?.Dispose();
                _tree = null;
            }

            private void Submit()
            {
                var targets = _tree?.GetTargetSelection() ??
                              Array.Empty<AssetFileTarget>();
                Close();
                _save(targets);
            }
        }

        private sealed class TargetDragPayload
        {
            internal string GroupName { get; set; }
            internal IReadOnlyList<AssetFileTarget> Targets { get; set; }
        }

        private readonly VariantPageState _variantState = new VariantPageState();
        private Action<DerivedAssetInfo> _modifyVariant;

        private readonly IAssetManager _manager;
        private readonly AssetManagerViewState _viewState;
        private readonly AssetManagerViewMode _mode;
        private readonly Action<string> _createDerivedAsset;
        private readonly CachedImageCache _imageCache;
        private readonly bool _ownsImageCache;
        private readonly VisualElement _navigation;
        private readonly VisualElement _content;
        private readonly VisualElement _detail;
        private readonly AssetItemGridView _itemGrid;
        private VisualElement _toolbar;
        private AssetManagerGridSizeSlider _gridSizeSlider;
        private VisualElement _gridControls;
        private SearchField _search;
        private AssetTagListView _tagList;
        private UiButton _sortButton;
        private UiButton _reloadButton;
        private UiButton _backButton;
        private UiButton _forwardButton;
        private AssetManagerBreadcrumb _breadcrumbs;

        private AssetThumbnailStack _detailThumbnailStack;
        private AssetThumbnailStack _itemOverviewThumbnailStack;
        private SearchableFileTree _fileTree;
        private ScrollView _itemDetailPane;
        private IReadOnlyList<FileTreeSelection> _fileTreeSelections =
            Array.Empty<FileTreeSelection>();
        private CancellationTokenSource _thumbnailCancellation;
        private CancellationTokenSource _itemOverviewThumbnailCancellation;
        private CancellationTokenSource _gridThumbnailCancellation;
        private ISet<string> _importedItemIdsInProject;
        private bool _defersManagerRefresh;
        private bool _managerRefreshPending;
        private readonly IAssetVariantManager _variantManager;
        private sealed class VariantMetadataSaveQueue
        {
            internal Task Pending = Task.CompletedTask;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            IAssetVariantManager, VariantMetadataSaveQueue> VariantMetadataSaveQueues =
            new System.Runtime.CompilerServices.ConditionalWeakTable<IAssetVariantManager, VariantMetadataSaveQueue>();
        private bool _disposed;

        internal void SetVariantModificationHandler(Action<DerivedAssetInfo> handler)
        {
            _modifyVariant = handler;
            Refresh();
        }

        public AssetManagerView(
            IAssetManager manager,
            AssetManagerViewState viewState = null,
            AssetManagerViewMode mode = AssetManagerViewMode.Main,
            CachedImageCache imageCache = null,
            Action<string> createDerivedAsset = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _variantManager = AssetManagerWindowSession.TryGetVariantManager(manager);
            _viewState = viewState ?? new AssetManagerViewState();
            _mode = mode;
            _createDerivedAsset = createDerivedAsset;
            _ownsImageCache = imageCache == null;
            _imageCache = imageCache ?? new CachedImageCache();
            AddToClassList("ee4v-asset-manager");

            if (ShowsNavigation)
            {
                _navigation = new ScrollView();
                _navigation.AddToClassList(
                    "ee4v-asset-manager__navigation");
                _navigation.contentContainer.AddToClassList(
                    "ee4v-asset-manager__navigation-content");
                Add(_navigation);
            }
            else if (ShowsMain)
            {
                _itemGrid = new AssetItemGridView(_imageCache);
                _itemGrid.SelectionChanged += SelectItems;
                _itemGrid.ItemDoubleClicked += OpenItemDetail;
                _itemGrid.ContextMenuRequested += ShowItemContextMenu;
                _itemGrid.RecommendedMinimumItemsPerRowChanged +=
                    SetMinimumGridSize;
                _itemGrid.VisibleItemsChanged +=
                    OnGridVisibleItemsChanged;
                _content = new VisualElement();
                _content.AddToClassList("ee4v-asset-manager__content");
                _toolbar = BuildToolbar();
                Add(_toolbar);
                Add(_content);
            }
            else
            {
                _detail = new ScrollView(ScrollViewMode.Vertical)
                {
                    horizontalScrollerVisibility =
                        ScrollerVisibility.Hidden,
                    verticalScrollerVisibility =
                        ScrollerVisibility.Hidden
                };
                _detail.AddToClassList("ee4v-asset-manager__detail");
                Add(_detail);
            }

            _manager.Changed += OnManagerChanged;
            if (_variantManager != null) { _variantManager.Changed += OnVariantsChanged; }
            _viewState.Changed += OnViewStateChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => HideVariantRevisionTooltip());

            try
            {
                if (ShowsNavigation)
                {
                    RebuildNavigation();
                }
                Refresh();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_variantManager != null) { _variantManager.Changed -= OnVariantsChanged; }
            _manager.Changed -= OnManagerChanged;
            _viewState.Changed -= OnViewStateChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            if (_itemGrid != null)
            {
                _itemGrid.SelectionChanged -= SelectItems;
                _itemGrid.ItemDoubleClicked -= OpenItemDetail;
                _itemGrid.ContextMenuRequested -= ShowItemContextMenu;
                _itemGrid.RecommendedMinimumItemsPerRowChanged -=
                    SetMinimumGridSize;
                _itemGrid.VisibleItemsChanged -=
                    OnGridVisibleItemsChanged;
            }
            if (_search != null)
            {
                _search.SearchActionRequested -= ShowSearchTargetsMenu;
            }

            CancelGridThumbnails();
            HideVariantRevisionTooltip();
            Cancel(ref _variantState.VariantPreviewCancellation);
            _variantState.VariantPreviewLoads.Clear();
            _variantState.VariantRevisionDetails.Clear();
            CancelThumbnail();
            CancelItemOverviewThumbnail();
            ClearDetailThumbnail();
            ClearItemOverviewThumbnail();
            _breadcrumbs?.Dispose();
            _itemGrid?.Dispose();
            if (_fileTree != null)
            {
                _fileTree.SelectionChanged -= OnFileTreeSelectionChanged;
                _fileTree.Dispose();
            }
            if (_ownsImageCache)
            {
                _imageCache.Dispose();
            }
        }

        private bool ShowsNavigation =>
            _mode == AssetManagerViewMode.Navigation;

        private bool ShowsMain =>
            _mode == AssetManagerViewMode.Main;

        private bool ShowsInformation =>
            _mode == AssetManagerViewMode.Information;

        private bool ShowsFileList =>
            _viewState.Page == AssetManagerPage.UnassignedFiles;

        private bool ShowsVariants =>
            _viewState.Page == AssetManagerPage.Variants;

        private void Refresh()
        {
            try
            {
                if (ShowsMain)
                {
                    RefreshMain();
                }

                if (ShowsInformation)
                {
                    RefreshDetail();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Task RunImport(
            Func<Task<AssetImportResult>> operation)
        {
            try
            {
                var result = await operation();
                if (!result.Succeeded)
                {
                    Debug.LogError(
                        "Asset import failed: " + result.State + " · " +
                        result.ErrorMessage);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private bool Run(Action operation, bool refresh = true)
        {
            var succeeded = false;
            var wasDeferringRefresh = _defersManagerRefresh;
            _defersManagerRefresh = wasDeferringRefresh || refresh;
            try
            {
                operation();
                succeeded = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _defersManagerRefresh = wasDeferringRefresh;
                if (!wasDeferringRefresh && refresh &&
                    (succeeded || _managerRefreshPending))
                {
                    _managerRefreshPending = false;
                    RefreshAfterManagerChange();
                }
            }

            return succeeded;
        }

        private bool RunWithoutDetailRefresh(Action operation)
        {
            var succeeded = false;
            var wasDeferringRefresh = _defersManagerRefresh;
            _defersManagerRefresh = true;
            try
            {
                operation();
                succeeded = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _defersManagerRefresh = wasDeferringRefresh;
                if (!wasDeferringRefresh)
                {
                    _managerRefreshPending = false;
                }
            }

            return succeeded;
        }

        private static BadgeState CreateAssetStatusState(
            bool isArchived)
        {
            if (!isArchived)
            {
                return null;
            }

            return new BadgeState(
                I18N.Get("detail.item.archived"),
                UiStatusTone.Idle);
        }

        private void ShowEmptyDetail(string text)
        {
            _detail.Clear();
            _detail.Add(AssetManagerControls.CreateNotice(text));
        }

        private void OnManagerChanged(AssetManagerChange change)
        {
            _importedItemIdsInProject = null;
            if (change.Kind == AssetManagerChangeKind.SourceSynchronized)
            {
                _itemGrid?.ClearThumbnails();
            }

            if (_defersManagerRefresh)
            {
                _managerRefreshPending = true;
                return;
            }

            if (change.Kind ==
                AssetManagerChangeKind.FileImportedAssetGuidsChanged)
            {
                if (ShowsMain)
                {
                    if (!string.IsNullOrEmpty(_viewState.DetailItemId))
                    {
                        RefreshItemDetailPane();
                    }
                    else if (_viewState.Page == AssetManagerPage.Imported)
                    {
                        RefreshMain();
                    }
                }
                if (ShowsInformation)
                {
                    RefreshDetail();
                }
                return;
            }

            RefreshAfterManagerChange();
        }

        private void OnProjectChanged()
        {
            _importedItemIdsInProject = null;
            if (_variantState.VariantMetadataSaveCount > 0) { return; }
            if (ShowsVariants || !string.IsNullOrEmpty(_viewState.DetailVariantId) ||
                !string.IsNullOrEmpty(_viewState.DetailItemId))
            {
                Refresh();
                return;
            }
            if (ShowsMain &&
                _viewState.Page == AssetManagerPage.Imported)
            {
                Refresh();
            }
        }

        private void RefreshAfterManagerChange()
        {
            if (ShowsNavigation)
            {
                RebuildNavigation();
            }
            Refresh();
        }

        private void OnViewStateChanged(
            AssetManagerViewStateChange change)
        {
            switch (change)
            {
                case AssetManagerViewStateChange.VariantSelection:
                    if (ShowsMain)
                    {
                        _itemGrid.SetSelectedItemIds(
                            _viewState.SelectedVariantIds, _viewState.SelectedVariantId);
                    }
                    if (ShowsInformation)
                    {
                        RefreshDetail();
                    }
                    break;
                case AssetManagerViewStateChange.Navigation:
                    if (ShowsNavigation)
                    {
                        RebuildNavigation();
                    }
                    Refresh();
                    break;
                case AssetManagerViewStateChange.ItemSelection:
                    if (ShowsMain)
                    {
                        _itemGrid.SetSelectedItemIds(
                            _viewState.SelectedItemIds,
                            _viewState.SelectedItemId);
                    }
                    if (ShowsInformation)
                    {
                        RefreshDetail();
                    }
                    break;
                case AssetManagerViewStateChange.FileSelection:
                    if (ShowsMain)
                    {
                        if (!string.IsNullOrEmpty(_viewState.DetailItemId))
                        {
                            RefreshItemDetailPane();
                        }
                        else if (_viewState.Page ==
                                 AssetManagerPage.UnassignedFiles)
                        {
                            BuildUnassignedFiles();
                        }
                    }
                    if (ShowsInformation)
                    {
                        RefreshDetail();
                    }
                    break;
                case AssetManagerViewStateChange.ItemDetailPage:
                    if (ShowsMain)
                    {
                        RefreshMain();
                    }
                    break;
                case AssetManagerViewStateChange.ItemSort:
                    if (ShowsMain)
                    {
                        Refresh();
                    }
                    break;
                case AssetManagerViewStateChange.SearchTargets:
                    if (ShowsMain)
                    {
                        Refresh();
                    }
                    break;
            }
        }

        private void CancelThumbnail()
        {
            Cancel(ref _thumbnailCancellation);
        }

        private void CancelItemOverviewThumbnail()
        {
            Cancel(ref _itemOverviewThumbnailCancellation);
        }

        private void CancelGridThumbnails()
        {
            Cancel(ref _gridThumbnailCancellation);
        }

        private static void Cancel(ref CancellationTokenSource cancellation)
        {
            var current = cancellation;
            cancellation = null;
            current?.Cancel();
        }

        private void ClearDetailThumbnail()
        {
            _detailThumbnailStack?.Dispose();
            _detailThumbnailStack = null;
        }

        private void ClearItemOverviewThumbnail()
        {
            _itemOverviewThumbnailStack?.Dispose();
            _itemOverviewThumbnailStack = null;
        }

        private static bool Confirm(string title, string message)
        {
            return EditorUtility.DisplayDialog(
                title,
                message,
                I18N.Get("action.delete"),
                I18N.Get("action.cancel"));
        }

        private static IReadOnlyList<string> SplitLines(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

    }
}
