using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    internal sealed class AssetManagerView : VisualElement, IDisposable
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
        private CancellationTokenSource _variantPreviewCancellation;
        private readonly Dictionary<string, Task<AssetThumbnail>> _variantPreviewLoads =
            new Dictionary<string, Task<AssetThumbnail>>(StringComparer.Ordinal);
        private string _variantDetailVariantId;
        private string _variantDetailCurrentRevisionId;
        private string _selectedVariantRevisionId;
        private string _selectedVariantGalleryImageId;
        private VariantGalleryView _variantGalleryView;
        private string _variantGalleryOperationId;
        private VisualElement _hoveredVariantRevisionRow;
        private Vector2 _variantRevisionPointerPosition;
        private CancellationTokenSource _variantRevisionHoverCancellation;
        private FileTreeImageTooltipWindow _variantRevisionTooltip;
        private ISet<string> _importedItemIdsInProject;
        private bool _defersManagerRefresh;
        private bool _managerRefreshPending;
        private readonly IAssetVariantManager _variantManager;
        private bool _variantBusy;
        private sealed class VariantMetadataSaveQueue
        {
            internal Task Pending = Task.CompletedTask;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            IAssetVariantManager, VariantMetadataSaveQueue> VariantMetadataSaveQueues =
            new System.Runtime.CompilerServices.ConditionalWeakTable<IAssetVariantManager, VariantMetadataSaveQueue>();
        private Task _variantMetadataSaveTask = Task.CompletedTask;
        private int _variantMetadataSaveCount;
        private bool _disposed;
        private readonly Dictionary<string, AssetVariantRevisionDetails> _variantRevisionDetails =
            new Dictionary<string, AssetVariantRevisionDetails>(StringComparer.Ordinal);

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
            Cancel(ref _variantPreviewCancellation);
            _variantPreviewLoads.Clear();
            _variantRevisionDetails.Clear();
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

        private VisualElement BuildToolbar()
        {
            var toolbar = new ActionBar();
            toolbar.AddToClassList("ee4v-asset-manager__toolbar");

            var history = new VisualElement();
            history.AddToClassList(
                "ee4v-asset-manager__history-navigation");
            _backButton = AssetManagerControls.CreateIconButton(
                I18N.Get("toolbar.back"),
                "arrow_left.png",
                _viewState.GoBack,
                "ee4v-asset-manager__history-button");
            _forwardButton = AssetManagerControls.CreateIconButton(
                I18N.Get("toolbar.forward"),
                "arrow_right.png",
                _viewState.GoForward,
                "ee4v-asset-manager__history-button");
            _breadcrumbs = new AssetManagerBreadcrumb();
            history.Add(_backButton);
            history.Add(_forwardButton);
            history.Add(_breadcrumbs);
            toolbar.Leading.Add(history);

            _search = AssetManagerControls.CreateSearchField(
                I18N.Get("toolbar.search.placeholder"),
                true,
                "ee4v-asset-manager__search");
            _search.tooltip = I18N.Get("toolbar.search.tooltip");
            _search.ValueChanged += _ => Refresh();
            _search.SearchActionRequested += ShowSearchTargetsMenu;

            _gridControls = new VisualElement();
            _gridControls.AddToClassList(
                "ee4v-asset-manager__grid-controls");
            _gridSizeSlider = AssetManagerControls.CreateGridSizeSlider(
                _itemGrid.ItemsPerRow,
                _itemGrid.RecommendedMinimumItemsPerRow,
                AssetItemGridView.MaximumItemsPerRow,
                "ee4v-asset-manager__grid-size-slider");
            _gridSizeSlider.tooltip = I18N.Get("toolbar.gridColumns");
            _gridSizeSlider.ValueChanged += SetGridSize;
            _gridControls.Add(_gridSizeSlider);
            toolbar.Center.Add(_gridControls);

            toolbar.Actions.AddToClassList(
                "ee4v-asset-manager__toolbar-actions");
            _sortButton = AssetManagerControls.CreateSortButton(
                ShowSortMenu,
                "ee4v-asset-manager__sort");
            RefreshSortButton();
            toolbar.Actions.Add(_sortButton);
            toolbar.Actions.Add(_search);
            _reloadButton = AssetManagerControls.CreateReloadButton(
                Reload,
                "ee4v-asset-manager__reload");
            toolbar.Actions.Add(_reloadButton);
            return toolbar;
        }

        private void ShowSortMenu()
        {
            var menu = new GenericMenu();
            AddSortMenuItem(
                menu,
                GetSortLabel(AssetManagerItemSortField.Name),
                AssetManagerItemSortField.Name);
            if (!ShowsVariants)
            {
                AddSortMenuItem(
                    menu,
                    GetSortLabel(AssetManagerItemSortField.CreatedAt),
                    AssetManagerItemSortField.CreatedAt);
                AddSortMenuItem(
                    menu,
                    GetSortLabel(AssetManagerItemSortField.UpdatedAt),
                    AssetManagerItemSortField.UpdatedAt);
            }
            if (!ShowsFileList && !ShowsVariants)
            {
                AddSortMenuItem(
                    menu,
                    GetSortLabel(AssetManagerItemSortField.FileCount),
                    AssetManagerItemSortField.FileCount);
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                UiTextFactory.CreateGuiContent(
                    I18N.Get("toolbar.sort.reverse")),
                _viewState.IsItemSortReversed,
                _viewState.ToggleItemSortDirection);
            menu.DropDown(_sortButton.worldBound);
        }

        private void ShowSearchTargetsMenu()
        {
            var menu = new GenericMenu();
            AddSearchTargetMenuItem(
                menu,
                "toolbar.search.name",
                AssetManagerSearchTarget.Name);
            AddSearchTargetMenuItem(
                menu,
                "toolbar.search.description",
                AssetManagerSearchTarget.Description);
            if (!ShowsVariants)
            {
                AddSearchTargetMenuItem(
                    menu,
                    "toolbar.search.tags",
                    AssetManagerSearchTarget.Tags);
            }
            menu.DropDown(_search.SearchActionAnchor.worldBound);
        }

        private void AddSearchTargetMenuItem(
            GenericMenu menu,
            string labelKey,
            AssetManagerSearchTarget target)
        {
            var included = _viewState.IncludesSearchTarget(target);
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get(labelKey)),
                included,
                () => _viewState.SetSearchTarget(target, !included));
        }

        private void AddSortMenuItem(
            GenericMenu menu,
            string label,
            AssetManagerItemSortField field)
        {
            menu.AddItem(
                UiTextFactory.CreateGuiContent(label),
                GetActiveSortField() == field,
                () => _viewState.SetItemSortField(field));
        }

        private void RefreshSortButton()
        {
            _sortButton.tooltip = I18N.Get(
                "toolbar.sort.tooltip",
                GetSortLabel(GetActiveSortField()),
                I18N.Get(
                    _viewState.IsItemSortReversed
                        ? "toolbar.sort.reverse"
                        : "toolbar.sort.forward"));
        }

        private AssetManagerItemSortField GetActiveSortField()
        {
            if (ShowsVariants)
            {
                return AssetManagerItemSortField.Name;
            }
            return ShowsFileList
                ? AssetManagerItemSort.GetFileSortField(
                    _viewState.ItemSortField)
                : _viewState.ItemSortField;
        }

        private static string GetSortLabel(
            AssetManagerItemSortField field)
        {
            switch (field)
            {
                case AssetManagerItemSortField.CreatedAt:
                    return I18N.Get("toolbar.sort.createdAt");
                case AssetManagerItemSortField.UpdatedAt:
                    return I18N.Get("toolbar.sort.updatedAt");
                case AssetManagerItemSortField.FileCount:
                    return I18N.Get("toolbar.sort.fileCount");
                default:
                    return I18N.Get("toolbar.sort.name");
            }
        }

        private void RefreshHistoryNavigation()
        {
            _backButton.SetEnabled(_viewState.CanGoBack);
            _forwardButton.SetEnabled(_viewState.CanGoForward);
            var pageTitle = GetPageTitle();
            if (!string.IsNullOrEmpty(_viewState.DetailVariantId))
            {
                var variant = GetVariants().FirstOrDefault(candidate =>
                    candidate.VariantId == _viewState.DetailVariantId);
                _breadcrumbs.SetItems(new[]
                {
                    new AssetManagerBreadcrumbItem(pageTitle, _viewState.ShowPageRoot),
                    new AssetManagerBreadcrumbItem(variant?.Name ?? I18N.Get("navigation.variants"))
                });
                return;
            }
            if (string.IsNullOrEmpty(_viewState.DetailItemId))
            {
                _breadcrumbs.SetItems(new[]
                {
                    new AssetManagerBreadcrumbItem(pageTitle)
                });
                return;
            }

            var itemName =
                _manager.GetItem(_viewState.DetailItemId)?.Name ??
                I18N.Get("common.item");
            _breadcrumbs.SetItems(new[]
            {
                new AssetManagerBreadcrumbItem(
                    pageTitle,
                    _viewState.ShowPageRoot),
                new AssetManagerBreadcrumbItem(itemName)
            });
        }

        private void RebuildNavigation()
        {
            _navigation.Clear();
            var primary = new VisualElement();
            primary.AddToClassList("ee4v-asset-manager__nav-primary");
            primary.Add(CreateNavigationButton(
                I18N.Get("navigation.imported"),
                AssetManagerPage.Imported,
                "checkmark.png"));
            primary.Add(CreateNavigationButton(
                I18N.Get("navigation.library"),
                AssetManagerPage.Library,
                "library.png"));
            primary.Add(CreateNavigationButton(
                I18N.Get("navigation.variants"),
                AssetManagerPage.Variants,
                "cube.png"));
            primary.Add(CreateNavigationButton(
                I18N.Get("navigation.unassignedFiles"),
                AssetManagerPage.UnassignedFiles,
                "mail_inbox.png"));
            primary.Add(CreateNavigationButton(
                I18N.Get("navigation.archived"),
                AssetManagerPage.Archived,
                "archive.png"));
            primary.Add(CreateNavigationButton(
                I18N.Get("navigation.tags"),
                AssetManagerPage.Tags,
                "tag.png"));
            _navigation.Add(primary);

            var collections = _manager.GetCollections();
            var collectionSection = new VisualElement();
            collectionSection.AddToClassList(
                "ee4v-asset-manager__collection-section");
            var section = new SectionHeader(I18N.Get("navigation.collections"));
            section.AddToClassList(
                "ee4v-asset-manager__collection-header");
            section.TitleText.SetFontSize(UiTypographyTokens.SmallFontSize);
            section.TitleText.SetColor(UiColorTokens.TextMuted);
            UiButton createCollectionButton = null;
            createCollectionButton = AssetManagerControls.CreateIconTextButton(
                I18N.Get("navigation.createCollection"),
                "add.png",
                () => ShowNewCollection(createCollectionButton),
                "ee4v-asset-manager__collection-create");
            createCollectionButton.tooltip = I18N.Get("navigation.newCollection");
            createCollectionButton.SetLabelFontSize(UiTypographyTokens.SmallFontSize);
            createCollectionButton.SetLabelColor(UiColorTokens.TextSecondary);
            section.Actions.Add(createCollectionButton);
            collectionSection.Add(section);

            if (collections.Count == 0)
            {
                var empty = UiTextFactory.Create(
                    I18N.Get("navigation.noCollections"),
                    UiClassNames.SecondaryText,
                    "ee4v-asset-manager__collection-empty");
                empty.SetWhiteSpace(WhiteSpace.Normal);
                collectionSection.Add(empty);
            }

            for (var i = 0; i < collections.Count; i++)
            {
                var collection = collections[i];
                var button = AssetManagerControls.CreateNavigationButton(
                    collection.Name,
                    AssetManagerControls.GetCollectionIconFileName(collection.Icon),
                    () => SelectCollection(collection.Id),
                    "ee4v-asset-manager__nav-button",
                    "ee4v-asset-manager__collection-row");
                AssetManagerControls.SetNavigationSelected(
                    button,
                    _viewState.Page == AssetManagerPage.Collection &&
                    string.Equals(
                        _viewState.CollectionId,
                        collection.Id,
                        StringComparison.Ordinal));
                RegisterCollectionReordering(button, collection);
                button.RegisterCallback<ContextClickEvent>(evt =>
                {
                    ShowCollectionContextMenu(button, collection);
                    evt.StopPropagation();
                });
                var count = UiTextFactory.Create(
                    _manager.SearchCollection(collection.Id, limit: 1)
                        .TotalCount.ToString(),
                    UiClassNames.SecondaryText,
                    "ee4v-asset-manager__collection-count");
                count.pickingMode = PickingMode.Ignore;
                count.SetFontSize(UiTypographyTokens.CaptionFontSize);
                count.SetColor(button.Selected
                    ? UiColorTokens.TextSecondary : UiColorTokens.TextMuted);
                count.SetWhiteSpace(WhiteSpace.NoWrap);
                count.SetTextAlign(TextAnchor.MiddleRight);
                button.Trailing.Add(count);
                collectionSection.Add(button);
            }
            _navigation.Add(collectionSection);
        }

        private NavigationItem CreateNavigationButton(
            string label,
            AssetManagerPage page,
            string iconFileName)
        {
            var button = AssetManagerControls.CreateNavigationButton(
                label,
                iconFileName,
                () => SelectPage(page),
                "ee4v-asset-manager__nav-button");
            AssetManagerControls.SetNavigationSelected(
                button,
                _viewState.Page == page);
            return button;
        }

        private void SelectPage(AssetManagerPage page)
        {
            _viewState.SelectPage(page);
        }

        private void SelectCollection(string collectionId)
        {
            _viewState.SelectCollection(collectionId);
        }

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

        private void RefreshMain()
        {
            _variantGalleryView = null;
            HideVariantRevisionTooltip();
            Cancel(ref _variantPreviewCancellation);
            _variantPreviewCancellation = new CancellationTokenSource();
            _search.SetPlaceholder(I18N.Get(ShowsVariants
                ? "variant.searchPlaceholder" : "toolbar.search.placeholder"));
            _search.tooltip = I18N.Get(ShowsVariants
                ? "variant.searchPlaceholder" : "toolbar.search.tooltip");
            _reloadButton.tooltip = I18N.Get(ShowsVariants
                ? "variant.reload" : "toolbar.reload");
            _toolbar.style.display = _viewState.IsDerivedAssetsPage
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            RefreshHistoryNavigation();
            RefreshSortButton();
            var showsItemDetail =
                !string.IsNullOrEmpty(_viewState.DetailItemId);
            var showsVariantDetail =
                !string.IsNullOrEmpty(_viewState.DetailVariantId);
            _content.EnableInClassList(
                "ee4v-asset-manager__content--item-detail",
                showsItemDetail || showsVariantDetail);
            if (showsVariantDetail)
            {
                BuildVariantDetailPage();
                return;
            }
            if (showsItemDetail)
            {
                BuildItemDetail();
                return;
            }

            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            switch (_viewState.Page)
            {
                case AssetManagerPage.Variants:
                    BuildVariants();
                    break;
                case AssetManagerPage.Tags:
                    if (string.IsNullOrEmpty(_viewState.TagPath))
                    {
                        BuildTags();
                    }
                    else
                    {
                        BuildItems();
                    }
                    break;
                case AssetManagerPage.UnassignedFiles:
                    BuildUnassignedFiles();
                    break;
                default:
                    BuildItems();
                    break;
            }
        }

        private void Reload()
        {
            CancelGridThumbnails();
            _itemGrid.ClearThumbnails();
            if (ShowsVariants)
            {
                _variantManager?.RebuildIndex();
                Refresh();
                return;
            }
            _defersManagerRefresh = true;
            try
            {
                SyncSource(
                    "Eagle",
                    () => _manager.SyncEagle(new EagleSyncRequest(
                        AssetManagerSettings.EagleLibraryPath,
                        AssetManagerSettings.EagleTargetRoot)));
                SyncSource(
                    "ee4v",
                    () => _manager.SyncEe4v(new Ee4vSyncRequest(
                        AssetManagerSettings.Ee4vLibraryPath)));
            }
            finally
            {
                _defersManagerRefresh = false;
                _managerRefreshPending = false;
                RefreshAfterManagerChange();
            }
        }

        private void SyncSource(
            string sourceName,
            Func<AssetSyncResult> synchronize)
        {
            Run(() =>
            {
                var result = synchronize();
                if (result == null || result.ErrorCount == 0)
                {
                    return;
                }

                Debug.LogWarning(
                    "Asset Manager " + sourceName + " sync: " +
                    string.Join(
                        Environment.NewLine,
                        result.ErrorMessages));
            }, refresh: false);
        }

        private void BuildItems()
        {
            var items = GetVisibleItems();
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.Flex;

            if (items.Count == 0)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.noItems")));
            }

            _itemGrid.SetItems(items.Select(item =>
                    new AssetItemGridEntry(
                        item.Id,
                        item.Name))
                .ToArray());
            _itemGrid.SetSelectedItemIds(
                _viewState.SelectedItemIds,
                _viewState.SelectedItemId);
            _content.Add(_itemGrid);
            OnGridVisibleItemsChanged(_itemGrid.GetVisibleItemIds());
        }

        private void BuildTags()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.None;
            _sortButton.style.display = DisplayStyle.None;
            _gridControls.style.display = DisplayStyle.None;

            if (_tagList == null)
            {
                _tagList = new AssetTagListView(_viewState.SelectTag);
            }
            _tagList.SetData(
                _manager.GetTags(),
                _manager.SearchItems(new AssetItemQuery()).Items);
            _content.Add(_tagList);
        }

        private void BuildVariants()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.Flex;
            var variants = GetVariants()
                .Where(variant => AssetManagerSearch.MatchesVariant(
                    variant, _search.Value, _viewState.SearchTargets))
                .OrderBy(variant => variant.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(variant => variant.AssetPath, StringComparer.Ordinal)
                .ToArray();
            if (_viewState.IsItemSortReversed)
            {
                Array.Reverse(variants);
            }
            if (variants.Length == 0)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.noVariants")));
            }
            var icon = AssetManagerControls.LoadFluentIconState(
                "cube.png", UiSizeTokens.Size24,
                I18N.Get("navigation.variants"), UiColorTokens.TextMuted);
            _itemGrid.SetItems(variants.Select(variant => new AssetItemGridEntry(
                variant.VariantId, variant.Name)
            {
                PlaceholderIcon = icon
            }).ToArray());
            _itemGrid.SetSelectedItemIds(
                _viewState.SelectedVariantIds, _viewState.SelectedVariantId);
            _content.Add(_itemGrid);
            OnGridVisibleItemsChanged(_itemGrid.GetVisibleItemIds());
        }

        private void SelectItems(
            IReadOnlyList<string> itemIds,
            string primaryItemId)
        {
            if (ShowsVariants)
            {
                _viewState.SelectVariants(itemIds, primaryItemId);
                return;
            }
            _viewState.SelectItems(itemIds, primaryItemId);
        }

        private void OpenItemDetail(string itemId)
        {
            if (ShowsVariants)
            {
                _viewState.OpenVariantDetail(itemId);
                return;
            }
            var item = _manager.GetItem(itemId);
            if (item == null || item.IsArchived)
            {
                return;
            }

            _viewState.OpenItemDetail(itemId);
        }

        private void ShowItemContextMenu(
            IReadOnlyList<string> itemIds,
            VisualElement anchor)
        {
            if (ShowsVariants)
            {
                return;
            }
            var items = (itemIds ?? Array.Empty<string>())
                .Select(_manager.GetItem)
                .Where(item => item != null)
                .ToArray();
            if (items.Length == 0)
            {
                return;
            }

            var ids = items.Select(item => item.Id).ToArray();
            var archived = items.All(item => item.IsArchived);
            var menu = new GenericMenu();
            if (!archived)
            {
                var importContent = UiTextFactory.CreateGuiContent(
                    I18N.Get("action.import"));
                if (items.Length == 1 && HasItemTargets(items[0].Id))
                {
                    var itemId = items[0].Id;
                    menu.AddItem(
                        importContent,
                        false,
                        () => ShowItemTargetImport(
                            anchor,
                            itemId,
                            _manager.GetFiles(itemId, true)));
                }
                else
                {
                    menu.AddDisabledItem(importContent);
                }
                menu.AddSeparator(string.Empty);
            }
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get(
                    ids.Length == 1
                        ? archived
                            ? "action.restore"
                            : "action.archive"
                        : archived
                            ? "action.restoreItems"
                            : "action.archiveItems",
                    ids.Length)),
                false,
                () => SetItemsArchived(ids, !archived));
            if (archived)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(I18N.Get(
                        ids.Length == 1
                            ? "action.delete"
                            : "action.deleteItems",
                        ids.Length)),
                    false,
                    () => DeleteItems(ids));
            }
            menu.ShowAsContext();
        }

        private void SetGridSize(int value)
        {
            _itemGrid.SetItemsPerRow(value);
            _gridSizeSlider.SetValueWithoutNotify(
                _itemGrid.ItemsPerRow);
        }

        private void SetMinimumGridSize(int value)
        {
            _gridSizeSlider.SetRangeWithoutNotify(
                value,
                AssetItemGridView.MaximumItemsPerRow);
            _gridSizeSlider.SetValueWithoutNotify(
                _itemGrid.ItemsPerRow);
        }

        private void OnGridVisibleItemsChanged(
            IReadOnlyList<string> itemIds)
        {
            if (_itemGrid.parent != _content)
            {
                return;
            }

            CancelGridThumbnails();
            var pending = (itemIds ?? Array.Empty<string>())
                .Where(itemId => !_itemGrid.HasThumbnailResult(itemId))
                .ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            _ = LoadGridThumbnailsAsync(pending);
        }

        private async Task LoadGridThumbnailsAsync(
            IReadOnlyList<string> itemIds)
        {
            var cancellation = new CancellationTokenSource();
            _gridThumbnailCancellation = cancellation;
            var savedVariants = ShowsVariants && _variantManager != null
                ? _variantManager.GetVariants().ToDictionary(variant => variant.Id, StringComparer.Ordinal)
                : null;
            try
            {
                await Task.Yield();
                cancellation.Token.ThrowIfCancellationRequested();
                using (var gate = new SemaphoreSlim(
                           MaximumConcurrentGridThumbnails,
                           MaximumConcurrentGridThumbnails))
                {
                    var tasks = itemIds.Select(async itemId =>
                    {
                        await gate.WaitAsync(cancellation.Token);
                        try
                        {
                            AssetThumbnail thumbnail;
                            if (ShowsVariants)
                            {
                                thumbnail = savedVariants != null && savedVariants.TryGetValue(itemId, out var variant)
                                    ? await _variantManager.GetRevisionThumbnail(itemId, variant.HeadRevisionId, cancellation.Token)
                                    : null;
                            }
                            else
                            {
                                thumbnail = await _manager.GetThumbnail(itemId, cancellation.Token);
                            }
                            if (!ReferenceEquals(
                                    _gridThumbnailCancellation,
                                    cancellation))
                            {
                                return;
                            }

                            _itemGrid.SetThumbnail(
                                itemId,
                                thumbnail != null && thumbnail.Found
                                    ? thumbnail.Data
                                    : null);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }).ToArray();
                    await Task.WhenAll(tasks);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (ReferenceEquals(_gridThumbnailCancellation, cancellation))
                {
                    _gridThumbnailCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void ShowItemDetail(AssetItem item)
        {
            _detail.Clear();
            if (item == null)
            {
                ShowEmptyDetail(I18N.Get("notice.selectedItemMissing"));
                return;
            }

            var thumbnailStack = CreateDetailThumbnailStack(
                new[] { item.Id });
            _detail.Add(thumbnailStack);

            var name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"),
                "ee4v-asset-manager__item-metadata-field");
            name.value = item.Name;
            var descriptionContainer = new VisualElement();
            descriptionContainer.AddToClassList(
                "ee4v-asset-manager-control-field");
            descriptionContainer.AddToClassList(
                "ee4v-asset-manager__item-metadata-field");
            descriptionContainer.Add(UiTextFactory.Create(
                I18N.Get("field.description"),
                UiClassNames.FormLabel,
                "ee4v-asset-manager-control-field__label"));
            var description = new InputField(new InputFieldState(
                item.Description,
                multiline: true,
                maxHeight: 144f));
            description.AddToClassList(
                "ee4v-asset-manager__description-field");
            descriptionContainer.Add(description);
            var tagsContainer = new VisualElement();
            tagsContainer.AddToClassList(
                "ee4v-asset-manager-control-field");
            tagsContainer.AddToClassList(
                "ee4v-asset-manager__item-metadata-field");
            tagsContainer.Add(UiTextFactory.Create(
                I18N.Get("field.tags"),
                UiClassNames.FormLabel,
                "ee4v-asset-manager-control-field__label"));
            var tags = new AssetTagField(
                _viewState.SelectTag,
                item.IsArchived);
            tags.SetValues(
                GetAvailableTagOptions(),
                GetTagPaths(item),
                GetSourceTagPaths(item));
            tagsContainer.Add(tags);
            var canEditMetadata = !item.IsArchived &&
                                  (!item.SourceType.HasValue ||
                                   item.SourceType == AssetSourceType.Ee4v);
            name.isReadOnly = !canEditMetadata;
            description.IsReadOnly = !canEditMetadata;
            if (canEditMetadata)
            {
                Action save = () => SaveItemMetadataAutomatically(
                    item.Id,
                    name.value,
                    description.Value);
                name.RegisterCallback<FocusOutEvent>(_ => save());
                description.RegisterCallback<FocusOutEvent>(_ => save());
            }
            if (!item.IsArchived)
            {
                tags.ValuesCommitted += () =>
                {
                    SaveItemTagsAutomatically(item.Id, tags.Values);
                    var current = _manager.GetItem(item.Id);
                    tags.SetValues(
                        GetAvailableTagOptions(),
                        current == null
                            ? Array.Empty<string>()
                            : GetTagPaths(current),
                        GetSourceTagPaths(current));
                };
            }
            _detail.Add(name);
            _detail.Add(descriptionContainer);
            _detail.Add(tagsContainer);
            AddItemInformation(
                _detail,
                item,
                GetFiles(item.Id));
            _ = LoadDetailThumbnailsAsync(new[] { item }, thumbnailStack);
        }

        private void RefreshDetail()
        {
            CancelThumbnail();
            ClearDetailThumbnail();
            if (ShowsVariants || !string.IsNullOrEmpty(_viewState.DetailVariantId))
            {
                ShowVariantDetail();
                return;
            }
            if (!string.IsNullOrEmpty(_viewState.DetailItemId))
            {
                ShowItemDetail(_manager.GetItem(
                    _viewState.DetailItemId));
                return;
            }

            if (!string.IsNullOrEmpty(_viewState.SelectedFileId))
            {
                ShowFileDetail(_manager.GetFile(
                    _viewState.SelectedFileId));
                return;
            }

            if (!string.IsNullOrEmpty(_viewState.SelectedItemId))
            {
                if (_viewState.SelectedItemIds.Count > 1)
                {
                    ShowItemSelectionDetail(
                        _viewState.SelectedItemIds
                            .Select(_manager.GetItem)
                            .Where(item => item != null)
                            .ToArray());
                }
                else
                {
                    ShowItemDetail(_manager.GetItem(
                        _viewState.SelectedItemId));
                }
                return;
            }

            ShowEmptyDetail(
                _viewState.Page == AssetManagerPage.UnassignedFiles
                    ? I18N.Get("notice.selectFile")
                    : I18N.Get("notice.selectItem"));
        }

        private void ShowVariantDetail()
        {
            _detail.Clear();
            var variant = GetVariants().FirstOrDefault(candidate =>
                candidate.VariantId == (_viewState.DetailVariantId ?? _viewState.SelectedVariantId));
            if (variant == null)
            {
                ShowEmptyDetail(I18N.Get("notice.selectVariant"));
                return;
            }
            if (_viewState.SelectedVariantIds.Count > 1)
            {
                _detail.Add(UiTextFactory.Create(
                    I18N.Get("variant.selectedCount", _viewState.SelectedVariantIds.Count),
                    UiClassNames.SelectionCount));
                _detail.Add(new InfoCard(new InfoCardState(variant.Name, variant.Description)));
            }
            else { AddVariantMetadataEditor(_detail, variant); }
        }

        private void AddVariantMetadataEditor(VisualElement detail, DerivedAssetInfo variant)
        {
            var latest = _variantManager?.GetRevisions(variant.VariantId)
                .OrderByDescending(revision => revision.Number).FirstOrDefault();
            var thumbnail = CreateVariantRevisionPreview(variant.VariantId, latest?.Id);
            thumbnail.AddToClassList("ee4v-asset-manager__variant-information-thumbnail");
            thumbnail.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var size = evt.newRect.width;
                if (float.IsNaN(size) || size <= 0f) { return; }
                thumbnail.style.height = size;
                thumbnail.style.minHeight = size;
                thumbnail.style.maxHeight = size;
            });
            detail.Add(thumbnail);
            var name = AssetManagerControls.CreateTextField(I18N.Get("field.name"),
                "ee4v-asset-manager__item-metadata-field", "ee4v-asset-manager__variant-name-field");
            name.value = variant.Name;
            var descriptionContainer = new VisualElement();
            descriptionContainer.AddToClassList("ee4v-asset-manager-control-field");
            descriptionContainer.AddToClassList("ee4v-asset-manager__item-metadata-field");
            descriptionContainer.Add(UiTextFactory.Create(I18N.Get("field.description"),
                UiClassNames.FormLabel, "ee4v-asset-manager-control-field__label"));
            var description = new InputField(new InputFieldState(variant.Description,
                multiline: true, maxHeight: 144f));
            description.AddToClassList("ee4v-asset-manager__description-field");
            description.AddToClassList("ee4v-asset-manager__variant-description-field");
            descriptionContainer.Add(description);
            name.isReadOnly = description.IsReadOnly = _variantManager == null || _variantBusy;
            var metadataError = new VisualElement();
            Action saveMetadata = () =>
            {
                var normalizedName = name.value.Trim();
                if (normalizedName.Length == 0)
                {
                    ((InputField)name.Input).SetValueWithoutNotify(variant.Name);
                    return;
                }
                ((InputField)name.Input).SetValueWithoutNotify(normalizedName);
                SaveVariantMetadataAutomatically(variant, normalizedName, description.Value, metadataError);
            };
            name.RegisterCallback<FocusOutEvent>(_ => saveMetadata());
            description.RegisterCallback<FocusOutEvent>(_ => saveMetadata());
            detail.Add(name);
            detail.Add(descriptionContainer);
            detail.Add(metadataError);
        }

        private void BuildVariantDetailPage()
        {
            CancelGridThumbnails();
            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            _itemDetailPane = null;
            _fileTreeSelections = Array.Empty<FileTreeSelection>();
            _content.Clear();
            _search.style.display = DisplayStyle.None;
            _sortButton.style.display = DisplayStyle.None;
            _gridControls.style.display = DisplayStyle.None;
            var variant = GetVariants().FirstOrDefault(candidate =>
                candidate.VariantId == _viewState.DetailVariantId);
            if (variant == null)
            {
                _content.Add(AssetManagerControls.CreateNotice(I18N.Get("variant.missing")));
                return;
            }

            var detail = new VisualElement();
            detail.AddToClassList("ee4v-asset-manager__variant-detail-page");
            _content.Add(detail);
            var revisions = _variantManager?.GetRevisions(variant.VariantId) ??
                Array.Empty<AssetVariantRevision>();
            var latest = revisions.OrderByDescending(revision => revision.Number).FirstOrDefault();
            AssetVariantRevisionDetails savedDetails = null;
            IReadOnlyList<AssetVariantFileDependency> dependencies = null;
            string dependencyError = null;
            try
            {
                if (latest != null)
                {
                    if (!_variantRevisionDetails.TryGetValue(latest.Id, out savedDetails))
                    {
                        savedDetails = _variantManager.GetRevisionDetails(variant.VariantId, latest.Id);
                        _variantRevisionDetails.Add(latest.Id, savedDetails);
                    }
                    dependencies = savedDetails.Dependencies;
                }
                else
                {
                    dependencies = GetCurrentVariantDependencies(variant);
                }
            }
            catch (Exception exception)
            {
                dependencyError = exception.Message;
            }

            var currentRevisionId = variant.Prefab != null
                ? _variantManager?.GetCurrentRevisionId(variant.VariantId) : null;
            if (_variantDetailVariantId != variant.VariantId || _variantDetailCurrentRevisionId != currentRevisionId)
            {
                if (_variantDetailVariantId != variant.VariantId) { _selectedVariantGalleryImageId = null; }
                _variantDetailVariantId = variant.VariantId;
                _variantDetailCurrentRevisionId = currentRevisionId;
                _selectedVariantRevisionId = null;
            }
            var selectedRevision = revisions.FirstOrDefault(revision => revision.Id == _selectedVariantRevisionId)
                ?? revisions.FirstOrDefault(revision => revision.Id == currentRevisionId) ?? latest;
            _selectedVariantRevisionId = selectedRevision?.Id;
            var layout = new VisualElement();
            layout.AddToClassList("ee4v-asset-manager__variant-detail-layout");
            layout.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var available = Mathf.Max(0f, evt.newRect.height - 120f);
                var history = layout.Q<ScrollView>(className: "ee4v-asset-manager__variant-history-scroll");
                var sources = layout.Q<ScrollView>(className: "ee4v-asset-manager__variant-dependency-scroll");
                if (history != null) { history.style.height = Mathf.Min(240f, available * .42f); }
                if (sources != null) { sources.style.height = Mathf.Min(320f, available * .58f); }
            });
            var overview = new VisualElement();
            overview.AddToClassList("ee4v-asset-manager__variant-overview");
            var management = new VisualElement();
            management.AddToClassList("ee4v-asset-manager__variant-management");
            layout.Add(overview);
            layout.Add(management);
            detail.Add(layout);
            var selectRevision = AddVariantOverview(overview, variant, selectedRevision, currentRevisionId, out var action);
            AddVariantHistory(management, variant, revisions, selectRevision, action);
            AddVariantDependencies(management, dependencies, dependencyError);
        }

        private Action<AssetVariantRevision> AddVariantOverview(VisualElement detail, DerivedAssetInfo variant,
            AssetVariantRevision selectedRevision, string currentRevisionId, out UiButton action)
        {
            var title = UiTextFactory.Create(variant.Name, UiClassNames.InfoCardTitle,
                "ee4v-asset-manager__variant-overview-title");
            title.SetFontSize(UiTypographyTokens.TitleFontSize);
            title.SetWhiteSpace(WhiteSpace.Normal);
            detail.Add(title);
            var descriptionSlot = new VisualElement();
            descriptionSlot.AddToClassList("ee4v-asset-manager__variant-overview-description-slot");
            descriptionSlot.style.display = string.IsNullOrWhiteSpace(variant.Description)
                ? DisplayStyle.None : DisplayStyle.Flex;
            var description = UiTextFactory.Create(variant.Description, UiClassNames.InfoCardDescription,
                "ee4v-asset-manager__variant-overview-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            description.tooltip = variant.Description;
            descriptionSlot.Add(description);
            detail.Add(descriptionSlot);
            var selectImage = AddVariantGallery(detail, variant, selectedRevision);
            var isImported = variant.Prefab != null;
            Func<bool> canModify = () => isImported && _modifyVariant != null &&
                (selectedRevision == null || selectedRevision.Id == currentRevisionId);
            var operation = AssetManagerControls.CreateButton(string.Empty, async () =>
            {
                var revision = selectedRevision;
                var modify = canModify();
                await PendingVariantMetadataSave;
                if (_disposed) { return; }
                if (modify) { ModifyVariant(variant.VariantId); }
                else if (revision != null) { RestoreVariant(variant.VariantId, revision.Id, confirm: isImported); }
            }, "ee4v-asset-manager__variant-overview-action");
            Action refreshAction = () =>
            {
                var modify = canModify();
                operation.SetLabel(I18N.Get(modify ? "variant.modify" : isImported ? "variant.restore" : "action.import"));
                operation.SetLabelColor(modify || !isImported ? UiColorTokens.TextOnState : UiColorTokens.TextPrimary);
                operation.EnableInClassList("ee4v-asset-manager__primary-action", modify || !isImported);
                operation.EnableInClassList("ee4v-asset-manager__variant-modify", modify);
                operation.EnableInClassList("ee4v-asset-manager__variant-import", !isImported);
                operation.EnableInClassList("ee4v-asset-manager__variant-restore", isImported && !modify);
                operation.style.display = modify || selectedRevision != null ? DisplayStyle.Flex : DisplayStyle.None;
                operation.SetEnabled(!_variantBusy && (modify || selectedRevision != null));
            };
            refreshAction();
            if (_variantGalleryView != null) { _variantGalleryView.RefreshControls += refreshAction; }
            action = operation;
            return revision =>
            {
                if (_variantBusy) { return; }
                selectedRevision = revision;
                _selectedVariantRevisionId = revision.Id;
                selectImage(revision);
                refreshAction();
            };
        }

        private sealed class VariantGalleryEntry
        {
            internal string Id;
            internal string Key;
            internal string Label;
            internal Func<Task<AssetThumbnail>> Load;
        }

        private sealed class VariantGalleryView
        {
            internal string VariantId;
            internal Action RefreshControls;
            internal Func<IReadOnlyList<AssetVariantGalleryUpload>, Action> AddImages;
            internal Func<string, Action> RemoveImage;
        }

        private Action<AssetVariantRevision> AddVariantGallery(VisualElement detail, DerivedAssetInfo variant,
            AssetVariantRevision selectedRevision)
        {
            var gallery = new VisualElement();
            gallery.AddToClassList("ee4v-asset-manager__variant-gallery");
            detail.Add(gallery);
            var preview = CreateVariantPreview(null, null);
            preview.AddToClassList("ee4v-asset-manager__variant-gallery-preview");
            var stage = new VisualElement();
            stage.AddToClassList("ee4v-asset-manager__variant-gallery-stage");
            var images = new VisualElement();
            images.AddToClassList("ee4v-asset-manager__variant-gallery-images");
            images.Add(preview);
            stage.Add(images);
            gallery.Add(stage);
            var entries = new List<VariantGalleryEntry>();
            Func<AssetVariantRevision, VariantGalleryEntry> automatic = revision => new VariantGalleryEntry
            {
                Key = "variant-revision:" + revision.Id,
                Label = I18N.Get("variant.automaticImage"),
                Load = () => _variantManager.GetRevisionThumbnail(variant.VariantId, revision.Id)
            };
            if (selectedRevision != null) { entries.Add(automatic(selectedRevision)); }
            try
            {
                foreach (var image in _variantManager?.GetGalleryImages(variant.VariantId) ??
                         Array.Empty<AssetVariantGalleryImage>())
                {
                    entries.Add(new VariantGalleryEntry
                    {
                        Id = image.Id, Key = "variant-gallery:" + image.Id, Label = image.FileName,
                        Load = () => _variantManager.GetGalleryImage(variant.VariantId, image.Id)
                    });
                }
            }
            catch (Exception exception) { gallery.Add(UiTextFactory.CreateHelpBox(exception.Message, HelpBoxMessageType.Error)); }
            var controls = new VisualElement { pickingMode = PickingMode.Ignore };
            controls.AddToClassList("ee4v-asset-manager__variant-gallery-controls");
            var index = Math.Max(0, entries.FindIndex(entry => entry.Id == _selectedVariantGalleryImageId));
            Action render = null;
            Action<int> select = next =>
            {
                if (entries.Count == 0) { return; }
                index = (next + entries.Count) % entries.Count;
                _selectedVariantGalleryImageId = entries[index].Id;
                render();
            };
            RegisterVariantGalleryImageContextMenu(preview, variant.VariantId,
                () => entries.Count > 0 ? entries[index] : null);
            Action resize = () =>
            {
                var management = detail.parent.Q<VisualElement>(className: "ee4v-asset-manager__variant-management");
                var availableWidth = (detail.parent.layout.width - management.resolvedStyle.marginLeft) * .5f;
                var size = Mathf.Min(500f, availableWidth, stage.layout.height);
                if (size <= 0 || float.IsNaN(size)) { return; }
                detail.style.width = size;
                images.style.width = size;
                preview.style.height = size;
            };
            stage.RegisterCallback<GeometryChangedEvent>(_ => resize());
            detail.parent.RegisterCallback<GeometryChangedEvent>(_ => resize());
            var previous = AssetManagerControls.CreateIconButton(I18N.Get("variant.previousImage"),
                "arrow_left.png", () => select(index - 1), "ee4v-asset-manager__variant-gallery-previous");
            var nextButton = AssetManagerControls.CreateIconButton(I18N.Get("variant.nextImage"),
                "arrow_right.png", () => select(index + 1), "ee4v-asset-manager__variant-gallery-next");
            var position = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                "ee4v-asset-manager__variant-gallery-position");
            position.SetTextAlign(TextAnchor.MiddleCenter);
            position.pickingMode = PickingMode.Ignore;
            var add = AssetManagerControls.CreateIconButton(I18N.Get("variant.addImages"), "add.png",
                UiSizeTokens.Size24, UiButtonVariant.Solid, () =>
            {
                var path = EditorUtility.OpenFilePanelWithFilters(I18N.Get("variant.addImages"), string.Empty,
                    new[] { I18N.Get("variant.imageFiles"), "png,jpg,jpeg" });
                if (!string.IsNullOrEmpty(path)) { AddVariantGalleryFiles(variant, new[] { path }); }
            }, "ee4v-asset-manager__variant-gallery-tile", "ee4v-asset-manager__variant-gallery-add");
            add.SetEnabled(_variantManager != null && !_variantBusy && !string.IsNullOrEmpty(variant.ParentItemId));
            controls.Add(previous);
            controls.Add(position);
            controls.Add(nextButton);
            preview.Overlay.Add(controls);
            var thumbnails = new ScrollView(ScrollViewMode.Horizontal)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Auto
            };
            thumbnails.AddToClassList("ee4v-asset-manager__variant-gallery-thumbnails");
            thumbnails.contentContainer.AddToClassList("ee4v-asset-manager__variant-gallery-thumbnail-row");
            gallery.Add(thumbnails);
            var tiles = new List<UiButton>();
            var tileById = new Dictionary<string, UiButton>(StringComparer.Ordinal);
            PreviewContainer automaticThumbnail = null;
            thumbnails.Add(add);
            Action rebuildTiles = () =>
            {
                var ids = new HashSet<string>(entries.Select(entry => entry.Id ?? "automatic"), StringComparer.Ordinal);
                foreach (var removed in tileById.Keys.Where(id => !ids.Contains(id)).ToArray())
                {
                    tileById[removed].RemoveFromHierarchy();
                    tileById.Remove(removed);
                }
                tiles.Clear();
                for (var imageIndex = 0; imageIndex < entries.Count; imageIndex++)
                {
                    var entry = entries[imageIndex];
                    var id = entry.Id ?? "automatic";
                    if (!tileById.TryGetValue(id, out var tile))
                    {
                        tile = AssetManagerControls.CreateButton(string.Empty,
                            () => select(entries.FindIndex(candidate => (candidate.Id ?? "automatic") == id)),
                            "ee4v-asset-manager__variant-gallery-tile");
                        tile.tooltip = entry.Label;
                        var thumbnail = CreateVariantPreview(entry.Key, entry.Load);
                        thumbnail.AddToClassList("ee4v-asset-manager__variant-gallery-tile-image");
                        thumbnail.pickingMode = PickingMode.Ignore;
                        thumbnail.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
                        tile.Content.Add(thumbnail);
                        RegisterVariantGalleryImageContextMenu(tile, variant.VariantId,
                            () => entries.FirstOrDefault(candidate => (candidate.Id ?? "automatic") == id));
                        if (entry.Id == null) { automaticThumbnail = thumbnail; }
                        tileById.Add(id, tile);
                        thumbnails.Insert(imageIndex, tile);
                    }
                    tiles.Add(tile);
                }
            };
            render = () =>
            {
                position.SetText(entries.Count == 0 ? "0 / 0" : (index + 1) + " / " + entries.Count);
                previous.SetEnabled(entries.Count > 1);
                nextButton.SetEnabled(entries.Count > 1);
                for (var tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
                {
                    tiles[tileIndex].EnableInClassList("ee4v-asset-manager__variant-gallery-tile--selected", tileIndex == index);
                }
                if (entries.Count == 0) { SetVariantPreview(preview, null, null); return; }
                if (thumbnails.panel != null && thumbnails.layout.width > 0) { thumbnails.ScrollTo(tiles[index]); }
                var entry = entries[index];
                SetVariantPreview(preview, entry.Key, entry.Load);
            };
            Func<Action> checkpoint = () =>
            {
                var savedEntries = entries.ToArray();
                var savedIndex = index;
                var savedSelection = _selectedVariantGalleryImageId;
                return () =>
                {
                    entries.Clear();
                    entries.AddRange(savedEntries);
                    index = savedIndex;
                    _selectedVariantGalleryImageId = savedSelection;
                    rebuildTiles();
                    render();
                };
            };
            _variantGalleryView = new VariantGalleryView
            {
                VariantId = variant.VariantId,
                RefreshControls = () =>
                {
                    add.SetEnabled(_variantManager != null && !_variantBusy && !string.IsNullOrEmpty(variant.ParentItemId));
                    _content.Query<UiButton>(className: "ee4v-asset-manager__variant-revision-row")
                        .ForEach(row => row.SetEnabled(!_variantBusy));
                },
                AddImages = uploads =>
                {
                    var rollback = checkpoint();
                    foreach (var upload in uploads)
                    {
                        string id;
                        using (var hash = SHA256.Create())
                        {
                            id = BitConverter.ToString(hash.ComputeHash(upload.Data)).Replace("-", string.Empty).ToLowerInvariant();
                        }
                        if (entries.Any(entry => entry.Id == id)) { continue; }
                        var key = "variant-gallery:" + id;
                        _imageCache.SetSource(key, upload.Data);
                        entries.Add(new VariantGalleryEntry
                        {
                            Id = id, Key = key, Label = upload.FileName,
                            Load = () => Task.FromResult(new AssetThumbnail { Found = true, Data = upload.Data })
                        });
                    }
                    rebuildTiles();
                    render();
                    thumbnails.schedule.Execute(() =>
                    {
                        if (thumbnails.panel != null) { thumbnails.ScrollTo(add); }
                    });
                    return rollback;
                },
                RemoveImage = id =>
                {
                    var rollback = checkpoint();
                    var selectedId = entries.Count > 0 ? entries[index].Id : null;
                    entries.RemoveAll(entry => entry.Id == id);
                    index = selectedId == id ? Math.Min(index, Math.Max(0, entries.Count - 1))
                        : Math.Max(0, entries.FindIndex(entry => entry.Id == selectedId));
                    _selectedVariantGalleryImageId = entries.Count > 0 ? entries[index].Id : null;
                    rebuildTiles();
                    render();
                    return rollback;
                }
            };
            rebuildTiles();
            render();
            gallery.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.LeftArrow && evt.keyCode != KeyCode.RightArrow) { return; }
                select(index + (evt.keyCode == KeyCode.LeftArrow ? -1 : 1));
                evt.StopPropagation();
            });
            gallery.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!add.enabledInHierarchy || GetDraggedGalleryPaths().Length == 0) { return; }
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });
            gallery.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!add.enabledInHierarchy) { return; }
                var paths = GetDraggedGalleryPaths();
                if (paths.Length == 0) { return; }
                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
                AddVariantGalleryFiles(variant, paths);
            });
            return revision =>
            {
                if (automaticThumbnail == null) { return; }
                entries[0] = automatic(revision);
                SetVariantRevisionPreview(automaticThumbnail, variant.VariantId, revision.Id);
                select(0);
            };
        }

        private void RegisterVariantGalleryImageContextMenu(VisualElement target, string variantId,
            Func<VariantGalleryEntry> getEntry)
        {
            target.RegisterCallback<ContextClickEvent>(evt =>
            {
                var entry = getEntry();
                if (entry?.Id == null) { return; }
                var menu = new GenericMenu();
                var label = UiTextFactory.CreateGuiContent(I18N.Get("variant.removeImage"));
                if (!_variantBusy && _variantManager != null)
                {
                    menu.AddItem(label, false, () => RemoveVariantGalleryImage(variantId, entry.Id));
                }
                else { menu.AddDisabledItem(label); }
                menu.ShowAsContext();
                evt.StopPropagation();
            });
        }

        private static string[] GetDraggedGalleryPaths() => DragAndDrop.paths
            .Concat(DragAndDrop.objectReferences.Select(AssetDatabase.GetAssetPath))
            .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path) &&
                new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        private async void AddVariantGalleryFiles(DerivedAssetInfo variant, IReadOnlyList<string> paths)
        {
            await PendingVariantMetadataSave;
            if (_disposed || _variantBusy || _variantManager == null) { return; }
            _variantBusy = true;
            _variantGalleryOperationId = variant.VariantId;
            var gallery = _variantGalleryView?.VariantId == variant.VariantId ? _variantGalleryView : null;
            Action rollback = null;
            try
            {
                RefreshVariantGalleryControls();
                var uploads = new List<AssetVariantGalleryUpload>();
                foreach (var path in paths)
                {
                    var bytes = await Task.Run(() =>
                    {
                        var file = new FileInfo(path);
                        if (file.Length > 16 * 1024 * 1024) { throw new InvalidDataException(I18N.Get("variant.imageTooLarge")); }
                        return File.ReadAllBytes(path);
                    });
                    var texture = new Texture2D(2, 2);
                    try
                    {
                        if (!texture.LoadImage(bytes, true)) { throw new InvalidDataException(I18N.Get("variant.invalidImage", Path.GetFileName(path))); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                    uploads.Add(new AssetVariantGalleryUpload { FileName = Path.GetFileName(path), Data = bytes });
                }
                if (_disposed) { return; }
                if (ReferenceEquals(gallery, _variantGalleryView)) { rollback = gallery?.AddImages(uploads); }
                await _variantManager.AddGalleryImages(variant.VariantId, variant.ParentItemId, uploads);
            }
            catch (Exception exception)
            {
                if (!_disposed && ReferenceEquals(gallery, _variantGalleryView)) { rollback?.Invoke(); }
                if (!_disposed) { EditorUtility.DisplayDialog(I18N.Get("variant.addImages"), exception.Message, "OK"); }
            }
            finally
            {
                _variantBusy = false;
                _variantGalleryOperationId = null;
                if (!_disposed)
                {
                    if (gallery == null || !ReferenceEquals(gallery, _variantGalleryView)) { Refresh(); }
                    else { RefreshVariantGalleryControls(); }
                }
            }
        }

        private async void RemoveVariantGalleryImage(string variantId, string imageId)
        {
            await PendingVariantMetadataSave;
            if (_disposed || _variantBusy || _variantManager == null) { return; }
            _variantBusy = true;
            _variantGalleryOperationId = variantId;
            var gallery = _variantGalleryView?.VariantId == variantId ? _variantGalleryView : null;
            Action rollback = null;
            try
            {
                rollback = gallery?.RemoveImage(imageId);
                RefreshVariantGalleryControls();
                await _variantManager.RemoveGalleryImage(variantId, imageId);
            }
            catch (Exception exception)
            {
                if (!_disposed && ReferenceEquals(gallery, _variantGalleryView)) { rollback?.Invoke(); }
                if (!_disposed) { EditorUtility.DisplayDialog(I18N.Get("variant.removeImage"), exception.Message, "OK"); }
            }
            finally
            {
                _variantBusy = false;
                _variantGalleryOperationId = null;
                if (!_disposed)
                {
                    if (gallery == null || !ReferenceEquals(gallery, _variantGalleryView)) { Refresh(); }
                    else { RefreshVariantGalleryControls(); }
                }
            }
        }

        private void RefreshVariantGalleryControls()
        {
            _variantGalleryView?.RefreshControls();
            if (ShowsInformation) { RefreshDetail(); }
        }

        private void AddVariantHistory(VisualElement detail, DerivedAssetInfo variant,
            IReadOnlyList<AssetVariantRevision> revisions, Action<AssetVariantRevision> selectRevision, UiButton action)
        {
            var section = new AssetDetailSection(I18N.Get("variant.history"));
            section.AddToClassList("ee4v-asset-manager__variant-history");
            section.Q<SectionHeader>().Actions.Add(action);
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("ee4v-asset-manager__variant-history-scroll");
            section.Add(scroll);
            if (revisions.Count == 0)
            {
                scroll.Add(UiTextFactory.Create(I18N.Get("variant.noHistory"), UiClassNames.SecondaryText));
            }
            var list = new VisualElement();
            list.AddToClassList("ee4v-asset-manager__variant-revision-list");
            list.RegisterCallback<GeometryChangedEvent>(evt => list.EnableInClassList(
                "ee4v-asset-manager__variant-revision-list--compact", evt.newRect.width < 400f));
            var rows = new Dictionary<string, UiButton>(StringComparer.Ordinal);
            foreach (var revision in revisions)
            {
                var row = AssetManagerControls.CreateButton(string.Empty, () =>
                {
                    if (_variantBusy) { return; }
                    selectRevision(revision);
                    foreach (var entry in rows)
                    {
                        entry.Value.EnableInClassList("ee4v-asset-manager__variant-revision-row--selected",
                            entry.Key == revision.Id);
                    }
                }, "ee4v-asset-manager__variant-revision-row");
                row.SetContentAlignment(Justify.FlexStart);
                row.EnableInClassList("ee4v-asset-manager__variant-revision-row--selected",
                    revision.Id == _selectedVariantRevisionId);
                row.SetEnabled(!_variantBusy);
                var number = UiTextFactory.Create("v" + revision.Number, UiClassNames.NavigationItemLabel,
                    "ee4v-asset-manager__variant-revision-number");
                number.pickingMode = PickingMode.Ignore;
                row.Content.Add(number);
                var memoSlot = new VisualElement { pickingMode = PickingMode.Ignore };
                memoSlot.AddToClassList("ee4v-asset-manager__variant-revision-memo-slot");
                var memo = UiTextFactory.Create(revision.Memo, UiClassNames.InfoCardDescription,
                    "ee4v-asset-manager__variant-revision-memo");
                memo.SetWhiteSpace(WhiteSpace.NoWrap);
                memo.pickingMode = PickingMode.Ignore;
                memoSlot.Add(memo);
                row.Content.Add(memoSlot);
                var date = UiTextFactory.Create(revision.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm"),
                    UiClassNames.SecondaryText, "ee4v-asset-manager__variant-revision-date");
                date.SetWhiteSpace(WhiteSpace.NoWrap);
                date.SetTextAlign(TextAnchor.MiddleRight);
                date.pickingMode = PickingMode.Ignore;
                row.Content.Add(date);
                row.RegisterCallback<PointerEnterEvent>(evt => BeginVariantRevisionTooltip(row, variant.VariantId,
                    revision, row.LocalToWorld(evt.localPosition)));
                row.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!ReferenceEquals(row, _hoveredVariantRevisionRow)) { return; }
                    _variantRevisionPointerPosition = row.LocalToWorld(evt.localPosition);
                    _variantRevisionTooltip?.SetPointerPosition(row, _variantRevisionPointerPosition);
                });
                row.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (ReferenceEquals(row, _hoveredVariantRevisionRow)) { HideVariantRevisionTooltip(); }
                });
                row.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    if (ReferenceEquals(row, _hoveredVariantRevisionRow)) { HideVariantRevisionTooltip(); }
                });
                rows.Add(revision.Id, row);
                list.Add(row);
            }
            scroll.Add(list);
            detail.Add(section);
        }

        private void BeginVariantRevisionTooltip(VisualElement row, string variantId,
            AssetVariantRevision revision, Vector2 panelPosition)
        {
            if (_variantManager == null || ReferenceEquals(row, _hoveredVariantRevisionRow)) { return; }
            HideVariantRevisionTooltip();
            _hoveredVariantRevisionRow = row;
            _variantRevisionPointerPosition = panelPosition;
            var cancellation = new CancellationTokenSource();
            _variantRevisionHoverCancellation = cancellation;
            _ = ShowVariantRevisionTooltipAsync(row, variantId, revision, cancellation);
        }

        private async Task ShowVariantRevisionTooltipAsync(VisualElement row, string variantId,
            AssetVariantRevision revision, CancellationTokenSource cancellation)
        {
            try
            {
                var key = "variant-revision:" + revision.Id;
                if (!await EnsureVariantPreviewSourceAsync(key,
                        () => _variantManager.GetRevisionThumbnail(variantId, revision.Id), cancellation.Token) ||
                    !ReferenceEquals(row, _hoveredVariantRevisionRow) || row.panel == null) { return; }
                using (var image = new CachedImage(_imageCache))
                {
                    image.SetSource(key);
                    if (image.DisplayedTexture is Texture2D texture)
                    {
                        _variantRevisionTooltip = FileTreeImageTooltipWindow.Show(row,
                            _variantRevisionPointerPosition, texture, string.Empty, 240f);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                _variantPreviewLoads.Remove("variant-revision:" + revision.Id);
                if (!_disposed && !cancellation.IsCancellationRequested) { Debug.LogException(exception); }
            }
            finally
            {
                if (ReferenceEquals(_variantRevisionHoverCancellation, cancellation))
                {
                    _variantRevisionHoverCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private void HideVariantRevisionTooltip()
        {
            var cancellation = _variantRevisionHoverCancellation;
            _variantRevisionHoverCancellation = null;
            cancellation?.Cancel();
            if (_variantRevisionTooltip != null)
            {
                _variantRevisionTooltip.Close();
                _variantRevisionTooltip = null;
            }
            _hoveredVariantRevisionRow = null;
        }

        private IReadOnlyList<AssetVariantFileDependency> GetCurrentVariantDependencies(DerivedAssetInfo variant)
        {
            if (variant.Prefab == null) { return Array.Empty<AssetVariantFileDependency>(); }
            var folder = Path.GetDirectoryName(variant.AssetPath).Replace('\\', '/');
            var sourceGuid = Infrastructure.DerivedAssetCatalog.Read(variant.AssetPath)?.SourceGuid;
            var guids = new HashSet<string>(AssetDatabase.GetDependencies(variant.AssetPath, true)
                .Concat(AssetDatabase.FindAssets(string.Empty, new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
                .Append(AssetDatabase.GUIDToAssetPath(sourceGuid ?? string.Empty))
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && path != variant.AssetPath && path != folder)
                .Select(AssetDatabase.AssetPathToGUID), StringComparer.Ordinal);
            return _manager.GetImportedAssetAssociations(guids.ToArray())
                .GroupBy(association => association.AssetGuid)
                .Select(group => group.OrderByDescending(association => association.ImportedAt).First())
                .Select(association => association.FileId).Distinct(StringComparer.Ordinal)
                .Select(_manager.GetFile)
                .Select(file => new AssetVariantFileDependency
                {
                    FileId = file.Id, SourceType = file.SourceType, SourceId = file.SourceId,
                    TargetPaths = Array.Empty<string>()
                }).ToArray();
        }

        private void AddVariantDependencies(VisualElement detail,
            IReadOnlyList<AssetVariantFileDependency> dependencies, string error)
        {
            var section = new AssetDetailSection(I18N.Get("variant.dependencies"));
            section.AddToClassList("ee4v-asset-manager__variant-dependencies");
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("ee4v-asset-manager__variant-dependency-scroll");
            section.Add(scroll);
            if (!string.IsNullOrEmpty(error))
            {
                scroll.Add(UiTextFactory.CreateHelpBox(error, HelpBoxMessageType.Error));
            }
            else if (dependencies == null || dependencies.Count == 0)
            {
                scroll.Add(UiTextFactory.Create(I18N.Get("common.none"), UiClassNames.SecondaryText));
            }
            else
            {
                IReadOnlyList<AssetFile> fallbackFiles = null;
                var grid = new VisualElement();
                grid.AddToClassList("ee4v-asset-manager__variant-card-grid");
                var groups = dependencies.Select(dependency => new
                    {
                        Dependency = dependency,
                        File = FindVariantDependencyFile(dependency, ref fallbackFiles)
                    })
                    .GroupBy(entry => !string.IsNullOrEmpty(entry.File?.ItemId)
                        ? "item:" + entry.File.ItemId
                        : entry.File != null ? "file:" + entry.File.Id
                        : "source:" + entry.Dependency.SourceType + ":" + entry.Dependency.SourceId,
                        StringComparer.Ordinal);
                foreach (var group in groups)
                {
                    var entries = group.GroupBy(entry =>
                            (entry.Dependency.SourceType, entry.Dependency.SourceId))
                        .Select(files => files.First()).ToArray();
                    var file = entries[0].File;
                    var item = FindVariantItem(file?.ItemId);
                    var title = item?.Name ?? (file == null ? I18N.Get("variant.dependencyMissing")
                        : file.IsArchived ? I18N.Get("detail.item.archived")
                        : I18N.Get("navigation.unassignedFiles"));
                    var fileNames = entries.Select(entry => entry.File?.FileName ??
                        Path.GetFileName(entry.Dependency.SourceId)).ToArray();
                    var description = fileNames.Length == 1 ? fileNames[0]
                        : I18N.Get("variant.dependencyFiles", fileNames[0], fileNames.Length - 1);
                    var archived = item != null && entries.Any(entry => entry.File?.IsArchived == true)
                        ? I18N.Get("detail.item.archived") : null;
                    var card = new InfoCard(new InfoCardState(title, description, badgeText: archived));
                    card.TitleText.SetWhiteSpace(WhiteSpace.Normal);
                    card.DescriptionText.SetWhiteSpace(WhiteSpace.Normal);
                    card.DescriptionText.tooltip = string.Join("\n", fileNames);
                    card.AddToClassList("ee4v-asset-manager__variant-card");
                    card.AddToClassList("ee4v-asset-manager__variant-dependency-card");
                    var preview = CreateVariantPreview(item?.Id, item == null ? null :
                        (Func<Task<AssetThumbnail>>)(() => _manager.GetThumbnail(item.Id)));
                    preview.AddToClassList("ee4v-asset-manager__variant-card-preview");
                    card.Insert(0, preview);
                    if (item != null)
                    {
                        card.Body.Add(AssetManagerControls.CreateButton(I18N.Get("variant.openSource"),
                            () => _viewState.OpenItemDetail(item.Id)));
                    }
                    var content = new VisualElement();
                    content.AddToClassList("ee4v-asset-manager__variant-dependency-content");
                    content.Add(card.Q<VisualElement>(className: "ee4v-ui-info-card__header"));
                    content.Add(card.Body);
                    card.Add(content);
                    grid.Add(card);
                }
                scroll.Add(grid);
            }
            detail.Add(section);
        }

        private PreviewContainer CreateVariantRevisionPreview(string variantId, string revisionId)
        {
            var key = string.IsNullOrEmpty(revisionId) ? null : "variant-revision:" + revisionId;
            return CreateVariantPreview(key, key == null || _variantManager == null ? null :
                (Func<Task<AssetThumbnail>>)(() => _variantManager.GetRevisionThumbnail(variantId, revisionId)));
        }

        private PreviewContainer CreateVariantPreview(string key, Func<Task<AssetThumbnail>> load)
        {
            var preview = new PreviewContainer();
            var image = new CachedImage(_imageCache) { scaleMode = ScaleMode.ScaleToFit, userData = key };
            image.AddToClassList("ee4v-asset-manager__variant-preview-image");
            preview.Content.Add(image);
            preview.RegisterCallback<DetachFromPanelEvent>(_ => image.Dispose());
            if (key != null && load != null) { _ = LoadVariantPreviewAsync(preview, image, key, load); }
            return preview;
        }

        private void SetVariantRevisionPreview(PreviewContainer preview, string variantId, string revisionId)
        {
            var key = string.IsNullOrEmpty(revisionId) ? null : "variant-revision:" + revisionId;
            SetVariantPreview(preview, key, key == null || _variantManager == null ? null :
                (Func<Task<AssetThumbnail>>)(() => _variantManager.GetRevisionThumbnail(variantId, revisionId)));
        }

        private void SetVariantPreview(PreviewContainer preview, string key, Func<Task<AssetThumbnail>> load)
        {
            var image = preview.Q<CachedImage>();
            if (key != null && string.Equals(image.userData as string, key, StringComparison.Ordinal) &&
                image.DisplayedTexture != null) { return; }
            image.userData = key;
            image.ClearSource();
            preview.SetHasContent(false);
            if (key != null && load != null)
            {
                _ = LoadVariantPreviewAsync(preview, image, key, load);
            }
        }

        private async Task<bool> EnsureVariantPreviewSourceAsync(string key, Func<Task<AssetThumbnail>> load,
            CancellationToken cancellation)
        {
            if (!_imageCache.HasSource(key))
            {
                if (!_variantPreviewLoads.TryGetValue(key, out var pending))
                {
                    pending = load();
                    _variantPreviewLoads.Add(key, pending);
                }
                var thumbnail = await pending;
                if (_disposed || cancellation.IsCancellationRequested) { return false; }
                _imageCache.SetSource(key, thumbnail != null && thumbnail.Found ? thumbnail.Data : null);
            }
            return !_disposed && !cancellation.IsCancellationRequested;
        }

        private async Task LoadVariantPreviewAsync(PreviewContainer preview, CachedImage image,
            string key, Func<Task<AssetThumbnail>> load)
        {
            var cancellation = _variantPreviewCancellation?.Token ?? CancellationToken.None;
            try
            {
                if (!await EnsureVariantPreviewSourceAsync(key, load, cancellation) ||
                    !string.Equals(image.userData as string, key, StringComparison.Ordinal)) { return; }
                image.SetSource(key);
                preview.SetHasContent(image.DisplayedTexture != null);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!_disposed && !cancellation.IsCancellationRequested) { Debug.LogException(exception); }
                _variantPreviewLoads.Remove(key);
            }
        }

        private async Task LoadDerivedVariantThumbnailAsync(AssetItemGridCard card, DerivedAssetInfo variant)
        {
            var saved = _variantManager?.GetVariants().FirstOrDefault(candidate => candidate.Id == variant.VariantId);
            if (saved == null) { return; }
            var cancellation = _variantPreviewCancellation?.Token ?? CancellationToken.None;
            var key = "variant-revision:" + saved.HeadRevisionId;
            try
            {
                if (!_variantPreviewLoads.TryGetValue(key, out var pending))
                {
                    pending = _variantManager.GetRevisionThumbnail(variant.VariantId, saved.HeadRevisionId);
                    _variantPreviewLoads.Add(key, pending);
                }
                var thumbnail = await pending;
                if (_disposed || cancellation.IsCancellationRequested) { return; }
                _imageCache.SetSource(variant.AssetPath, thumbnail.Data);
                card.SetState(new AssetItemGridEntry(variant.AssetPath, variant.Name), selected: false);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!_disposed && !cancellation.IsCancellationRequested) { Debug.LogException(exception); }
                _variantPreviewLoads.Remove(key);
            }
        }

        private AssetFile FindVariantDependencyFile(AssetVariantFileDependency dependency,
            ref IReadOnlyList<AssetFile> fallbackFiles)
        {
            AssetFile file = null;
            if (!string.IsNullOrEmpty(dependency.FileId))
            {
                try { file = _manager.GetFile(dependency.FileId); }
                catch (AssetManagerException exception) when (exception.Code == AssetManagerErrorCode.NotFound) { }
            }
            if (file != null && file.SourceType == dependency.SourceType && file.SourceId == dependency.SourceId)
            {
                return file;
            }
            if (fallbackFiles == null)
            {
                fallbackFiles = _manager.SearchItems(new AssetItemQuery { IncludeArchived = true }).Items
                    .SelectMany(item => _manager.GetFiles(item.Id, true))
                    .Concat(_manager.GetUnassignedFiles(true)).ToArray();
            }
            return fallbackFiles.FirstOrDefault(candidate => candidate.SourceType == dependency.SourceType &&
                candidate.SourceId == dependency.SourceId);
        }

        private AssetItem FindVariantItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) { return null; }
            try { return _manager.GetItem(itemId); }
            catch (AssetManagerException exception) when (exception.Code == AssetManagerErrorCode.NotFound) { return null; }
        }

        private void ModifyVariant(string variantId)
        {
            if (_variantBusy || _modifyVariant == null) { return; }
            var variant = GetVariants().FirstOrDefault(candidate => candidate.VariantId == variantId);
            if (variant?.Prefab == null)
            {
                return;
            }
            _modifyVariant(variant);
        }

        private IReadOnlyList<DerivedAssetInfo> GetVariants()
        {
            var project = DerivedAssetCreator.FindAll().ToDictionary(
                variant => variant.VariantId, StringComparer.Ordinal);
            if (_variantManager != null)
            {
                foreach (var saved in _variantManager.GetVariants())
                {
                    if (project.TryGetValue(saved.Id, out var imported))
                    {
                        imported.Name = saved.Name;
                        imported.Description = saved.Description;
                    }
                    else
                    {
                        project.Add(saved.Id, new DerivedAssetInfo
                        {
                            VariantId = saved.Id, Name = saved.Name, Description = saved.Description,
                            ParentItemId = saved.ParentItemId, AssetPath = saved.RootAssetPath
                        });
                    }
                }
            }
            return project.Values.ToArray();
        }

        private async void RestoreVariant(string variantId, string revisionId, bool confirm = true)
        {
            await PendingVariantMetadataSave;
            if (_disposed) { return; }
            if (_variantBusy || _variantManager == null || (confirm && !EditorUtility.DisplayDialog(I18N.Get("variant.restore"),
                    I18N.Get("variant.restoreConfirm"), I18N.Get("variant.restore"), I18N.Get("action.cancel"))))
            {
                return;
            }
            _variantBusy = true;
            Refresh();
            try
            {
                await _variantManager.Restore(variantId, revisionId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!_disposed)
                {
                    EditorUtility.DisplayDialog(I18N.Get(confirm ? "variant.restore" : "action.import"), exception.Message, "OK");
                }
            }
            finally
            {
                _variantBusy = false;
                if (!_disposed) { Refresh(); }
            }
        }

        private void OnVariantsChanged()
        {
            if (_variantGalleryOperationId != null) { return; }
            _variantRevisionDetails.Clear();
            _itemGrid?.ClearThumbnails();
            if (_variantMetadataSaveCount > 0) { return; }
            if (!_disposed && (ShowsVariants || !string.IsNullOrEmpty(_viewState.DetailVariantId) ||
                !string.IsNullOrEmpty(_viewState.DetailItemId))) { Refresh(); }
        }

        private void ShowItemSelectionDetail(
            IReadOnlyList<AssetItem> items)
        {
            _detail.Clear();
            if (items == null || items.Count == 0)
            {
                ShowEmptyDetail(I18N.Get("notice.selectedItemMissing"));
                return;
            }

            var orderedItems = items
                .OrderBy(item => string.Equals(
                    item.Id,
                    _viewState.SelectedItemId,
                    StringComparison.Ordinal)
                    ? 1
                    : 0)
                .ToArray();
            var thumbnailStack = CreateDetailThumbnailStack(
                orderedItems.Select(item => item.Id).ToArray());
            _detail.Add(thumbnailStack);
            _detail.Add(UiTextFactory.Create(
                string.Format(
                    I18N.Get("detail.item.selectedCount"),
                    items.Count),
                UiClassNames.SelectionCount,
                "ee4v-asset-manager__selection-count"));
            _ = LoadDetailThumbnailsAsync(orderedItems, thumbnailStack);
        }

        private AssetThumbnailStack CreateDetailThumbnailStack(
            IReadOnlyList<string> itemIds)
        {
            var stack = new AssetThumbnailStack(_imageCache, itemIds);
            _detailThumbnailStack = stack;
            return stack;
        }

        private async Task LoadDetailThumbnailsAsync(
            IReadOnlyList<AssetItem> items,
            AssetThumbnailStack thumbnailStack)
        {
            var firstIndex = Math.Max(0, items.Count - 3);
            var cancellation = new CancellationTokenSource();
            _thumbnailCancellation = cancellation;
            try
            {
                for (var index = firstIndex; index < items.Count; index++)
                {
                    var itemId = items[index].Id;
                    if (!_imageCache.HasSource(itemId))
                    {
                        var thumbnail = await _manager.GetThumbnail(
                            itemId,
                            cancellation.Token);
                        if (!ReferenceEquals(
                                _thumbnailCancellation,
                                cancellation))
                        {
                            return;
                        }

                        _imageCache.SetSource(
                            itemId,
                            thumbnail != null && thumbnail.Found
                                ? thumbnail.Data
                                : Array.Empty<byte>());
                    }

                    thumbnailStack.Refresh(itemId);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (ReferenceEquals(_thumbnailCancellation, cancellation))
                {
                    _thumbnailCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private async Task LoadItemOverviewThumbnailAsync(
            AssetItem item,
            AssetThumbnailStack thumbnailStack)
        {
            var cancellation = new CancellationTokenSource();
            _itemOverviewThumbnailCancellation = cancellation;
            try
            {
                if (!_imageCache.HasSource(item.Id))
                {
                    var thumbnail = await _manager.GetThumbnail(
                        item.Id,
                        cancellation.Token);
                    if (!ReferenceEquals(
                            _itemOverviewThumbnailCancellation,
                            cancellation))
                    {
                        return;
                    }

                    _itemGrid.SetThumbnail(
                        item.Id,
                        thumbnail != null && thumbnail.Found
                            ? thumbnail.Data
                            : null);
                }

                thumbnailStack.Refresh(item.Id);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (ReferenceEquals(
                        _itemOverviewThumbnailCancellation,
                        cancellation))
                {
                    _itemOverviewThumbnailCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void ShowFileDetail(AssetFile file)
        {
            _detail.Clear();
            if (file == null)
            {
                ShowEmptyDetail(I18N.Get("notice.selectedFileMissing"));
                return;
            }

            BuildFileDetail(_detail, file);
        }

        private void BuildFileDetail(VisualElement host, AssetFile file)
        {
            if (host == null || file == null)
            {
                return;
            }

            var extension = GetFileExtension(file);
            var eyebrow = I18N.Get("detail.file.eyebrow") +
                          (string.IsNullOrEmpty(extension)
                              ? string.Empty
                              : " · " + extension.ToUpperInvariant()) +
                          " · " +
                          file.SourceType.ToString().ToUpperInvariant();
            var header = new AssetDetailHeader(
                file.FileName,
                eyebrow,
                file.SourcePath,
                CreateAssetStatusState(file.IsArchived));
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, null)))
            {
                header.AddAction(AssetManagerControls.CreateButton(
                    I18N.Get("action.import"),
                    () => ImportFileTreeEntry(
                        new FileTreeSelection(file, null)),
                    "ee4v-asset-manager__primary-action"));
            }
            header.AddAction(AssetManagerControls.CreateButton(
                I18N.Get(file.IsArchived
                    ? "action.restore"
                    : "action.archive"),
                () => ArchiveFile(file.Id, !file.IsArchived)));
            if (file.IsArchived)
            {
                header.AddAction(AssetManagerControls.CreateDangerButton(
                    I18N.Get("action.delete"),
                    () => DeleteFile(file.Id)));
            }
            host.Add(header);

            BuildFileSettings(
                host,
                new[] { file },
                SearchableFileTree.CanImport(
                    new FileTreeSelection(file, null))
                    ? new[] { new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = string.Empty
                    } }
                    : Array.Empty<AssetFileTarget>(),
                showFileId: true);
        }

        private void BuildFileSettings(
            VisualElement host,
            IReadOnlyList<AssetFile> files,
            IReadOnlyList<AssetFileTarget> dependencySources,
            bool showFileId)
        {
            var fileIds = files.Select(file => file.Id).ToArray();
            var assignedItemId = files[0].ItemId;
            var settings = new AssetDetailSection(
                I18N.Get("detail.settings"));
            var settingList = new AssetDetailSettingList();
            var itemId = AssetManagerControls.CreateTextField(
                string.Empty,
                "ee4v-asset-manager__setting-input");
            itemId.value = assignedItemId ?? string.Empty;
            settingList.Add(AssetDetailSettingRow.Editable(
                I18N.Get("field.assignedItem"),
                string.IsNullOrWhiteSpace(assignedItemId)
                    ? I18N.Get("common.none")
                    : assignedItemId,
                itemId,
                AssetManagerControls.CreateButton(
                    I18N.Get("action.moveFile"),
                    () => MoveFiles(fileIds, itemId.value)),
                I18N.Get("action.move")));

            AddDependencySetting(settingList, dependencySources);
            if (showFileId)
            {
                settingList.Add(new AssetDetailSettingRow(
                    I18N.Get("field.fileId"),
                    CreateMonoValue(fileIds[0])));
            }
            settings.Add(settingList);
            host.Add(settings);
        }

        private void AddDependencySetting(
            AssetDetailSettingList settingList,
            IReadOnlyList<AssetFileTarget> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return;
            }

            var dependencyTargets = GetCommonFileDependencies(sources);
            var dependencySummary = dependencyTargets
                .Select(target => FormatTargetName(
                    _manager.GetFile(target.FileId), target))
                .ToArray();
            UiButton editDependencies = null;
            editDependencies = AssetManagerControls.CreateButton(
                I18N.Get("action.edit"),
                () => ShowDependencyEditor(
                    editDependencies,
                    sources,
                    dependencyTargets));
            if (sources.Count > 1)
            {
                editDependencies.tooltip = I18N.Get(
                    "detail.batchDependenciesTooltip");
            }
            var summary = UiTextFactory.Create(
                dependencySummary.Length == 0
                    ? I18N.Get("common.none")
                    : string.Join(" · ", dependencySummary));
            summary.SetWhiteSpace(WhiteSpace.Normal);
            settingList.Add(new AssetDetailSettingRow(
                I18N.Get("field.dependencies"),
                summary,
                editDependencies));
        }

        private void SelectFile(AssetFile file)
        {
            _viewState.SelectFile(file?.Id);
        }

        private void BuildItemDetail()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.None;
            _sortButton.style.display = DisplayStyle.None;
            _gridControls.style.display = DisplayStyle.None;
            var item = _manager.GetItem(_viewState.DetailItemId);
            if (item == null)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.itemMissing")));
                return;
            }

            if (_viewState.IsDerivedAssetsPage)
            {
                BuildDerivedAssetsPage();
                return;
            }

            var files = GetFiles(item.Id);
            var layout = new VisualElement();
            layout.AddToClassList("ee4v-asset-manager__item-detail-layout");
            var treePane = new VisualElement();
            treePane.AddToClassList("ee4v-asset-manager__item-detail-tree-pane");
            if (_fileTree == null)
            {
                _fileTree = new SearchableFileTree(
                    _manager,
                    RegisterCurrentItemFile,
                    selections => _ = ImportFileTreeEntriesAsync(selections));
                _fileTree.SelectionChanged += OnFileTreeSelectionChanged;
            }
            treePane.Add(_fileTree);
            layout.Add(treePane);

            _itemDetailPane = new ScrollView(ScrollViewMode.Vertical);
            _itemDetailPane.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            _itemDetailPane.AddToClassList(
                "ee4v-asset-manager__item-detail-pane");
            _itemDetailPane.contentContainer.AddToClassList(
                "ee4v-asset-manager__item-detail-content");
            layout.Add(_itemDetailPane);
            _content.Add(layout);

            if (!string.Equals(
                    _fileTree.ItemId,
                    item.Id,
                    StringComparison.Ordinal))
            {
                _fileTreeSelections = Array.Empty<FileTreeSelection>();
            }
            _fileTree.SetItem(item.Id, files);
            _fileTreeSelections = _fileTree.SelectedSelections;
            RefreshItemDetailPane(item, files);
        }

        private void BuildDerivedAssetsPage()
        {
            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            _itemDetailPane = null;
            _fileTreeSelections = Array.Empty<FileTreeSelection>();
            var page = new VisualElement();
            page.AddToClassList(
                "ee4v-asset-manager__derived-assets-page");
            page.Add(AssetManagerControls.CreateIconButton(
                I18N.Get("toolbar.back"),
                "arrow_left.png",
                UiSizeTokens.Size18,
                UiButtonVariant.Ghost,
                _viewState.CloseDerivedAssetsPage,
                "ee4v-asset-manager__derived-assets-back"));

            var form = new VisualElement();
            form.AddToClassList(
                "ee4v-asset-manager__derived-assets-form");
            form.Add(UiTextFactory.Create(
                I18N.Get("detail.derivedAssetsCreate"),
                UiClassNames.SectionTitle,
                "ee4v-asset-manager__derived-assets-form-title"));

            var formLayout = new VisualElement();
            formLayout.AddToClassList(
                "ee4v-asset-manager__derived-assets-form-layout");
            var previewColumn = new VisualElement();
            previewColumn.AddToClassList(
                "ee4v-asset-manager__derived-assets-preview-column");
            var prefabPreview = new DerivedAssetPrefabScenePreview();
            previewColumn.Add(prefabPreview);

            var prefabContainer = new VisualElement();
            prefabContainer.AddToClassList(
                "ee4v-asset-manager-control-field");
            prefabContainer.Add(UiTextFactory.Create(
                I18N.Get("field.prefab"),
                UiClassNames.FormLabel,
                "ee4v-asset-manager-control-field__label"));
            var prefabCandidates = DerivedAssetCreator
                .FindPrefabCandidates(_manager.GetItemImportedAssetGuids(
                    _viewState.DetailItemId))
                .ToList();
            var prefab = new DerivedAssetPrefabSelector(
                prefabCandidates);
            prefab.AddToClassList(
                "ee4v-asset-manager__derived-assets-prefab-field");
            prefabContainer.Add(prefab);
            if (prefabCandidates.Count == 0)
            {
                prefabContainer.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.derivedAssetPrefabNotFound"),
                    "ee4v-asset-manager__derived-assets-prefab-empty"));
            }
            previewColumn.Add(prefabContainer);
            formLayout.Add(previewColumn);

            var fields = new VisualElement();
            fields.AddToClassList(
                "ee4v-asset-manager__derived-assets-fields");
            var name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"));

            var description = AssetManagerControls.CreateTextField(
                I18N.Get("field.description"),
                "ee4v-asset-manager__derived-assets-description-field");
            description.SetMultiline(true, 144f);
            var inputGroup = new InputGroup(
                I18N.Get("detail.information"),
                name,
                description);
            inputGroup.AddToClassList(
                "ee4v-asset-manager__derived-assets-input-group");
            fields.Add(inputGroup);

            var message = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText);
            message.SetColor(UiColorTokens.StatusFailedText);
            message.SetWhiteSpace(WhiteSpace.Normal);
            message.style.display = DisplayStyle.None;
            fields.Add(message);
            UiButton create = null;
            create = AssetManagerControls.CreateButton(
                I18N.Get("action.createDerivedAsset"),
                () => CreateDerivedAsset(
                    name.value,
                    prefab.Value,
                    description.value,
                    message,
                    create),
                "ee4v-asset-manager__primary-action",
                "ee4v-asset-manager__derived-assets-create");
            create.SetEnabled(false);
            prefab.ValueChanged += selectedPrefab =>
            {
                prefabPreview.SetPrefab(selectedPrefab);
                SetDerivedAssetFeedback(message, string.Empty);
                create.SetEnabled(selectedPrefab != null);
            };
            prefab.SelectionRejected += () =>
            {
                create.SetEnabled(prefab.Value != null);
                SetDerivedAssetFeedback(
                    message,
                    I18N.Get("notice.derivedAssetPrefabInvalid"));
            };
            fields.Add(create);
            formLayout.Add(fields);
            form.Add(formLayout);
            page.Add(form);
            _content.Add(page);
        }

        private void CreateDerivedAsset(
            string name,
            GameObject prefab,
            string description,
            UiTextElement message,
            UiButton createButton)
        {
            if (!DerivedAssetCreator.IsValidName(name))
            {
                SetDerivedAssetFeedback(
                    message,
                    I18N.Get("notice.derivedAssetNameInvalid"));
                return;
            }

            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath) ||
                !prefabPath.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase) ||
                !IsAllowedPrefab(
                    prefab,
                    new HashSet<string>(
                        _manager.GetItemImportedAssetGuids(
                            _viewState.DetailItemId) ??
                        Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase)))
            {
                SetDerivedAssetFeedback(
                    message,
                    I18N.Get("notice.derivedAssetPrefabInvalid"));
                return;
            }

            if (AssetDatabase.IsValidFolder(
                    DerivedAssetCreator.GetVariantFolder(name)))
            {
                SetDerivedAssetFeedback(
                    message,
                    I18N.Get("notice.derivedAssetAlreadyExists"));
                return;
            }

            createButton.SetEnabled(false);
            try
            {
                var result = DerivedAssetCreator.Create(
                    new DerivedAssetCreationRequest
                    {
                        ParentItemId = _viewState.DetailItemId,
                        Name = name,
                        Prefab = prefab,
                        Description = description
                    });
                Selection.activeObject = result.Prefab;
                EditorGUIUtility.PingObject(result.Prefab);
                _viewState.CloseDerivedAssetsPage();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetDerivedAssetFeedback(
                    message,
                    I18N.Get("notice.derivedAssetCreateFailed"));
            }
            finally
            {
                createButton.SetEnabled(true);
            }
        }

        private static void SetDerivedAssetFeedback(
            UiTextElement message,
            string text)
        {
            message.SetText(text);
            message.style.display = string.IsNullOrWhiteSpace(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private static bool IsAllowedPrefab(
            GameObject prefab,
            ISet<string> allowedGuids)
        {
            if (prefab == null || allowedGuids == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith(
                       ".prefab",
                       StringComparison.OrdinalIgnoreCase) &&
                   allowedGuids.Contains(
                       AssetDatabase.AssetPathToGUID(path));
        }

        private void RefreshItemDetailPane()
        {
            var item = _manager.GetItem(_viewState.DetailItemId);
            if (item == null)
            {
                return;
            }
            RefreshItemDetailPane(item, null);
        }

        private void RefreshItemDetailPane(
            AssetItem item,
            IReadOnlyList<AssetFile> files)
        {
            if (_itemDetailPane == null)
            {
                return;
            }

            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            _itemDetailPane.Clear();
            if (_fileTreeSelections.Count > 1)
            {
                BuildFileTreeSelectionDetail(
                    _itemDetailPane,
                    _fileTreeSelections);
                return;
            }

            var selection = _fileTreeSelections.Count == 1
                ? _fileTreeSelections[0]
                : null;
            if (selection?.Entry != null)
            {
                BuildFileEntryDetail(
                    _itemDetailPane,
                    selection.File,
                    selection.Entry);
                return;
            }

            var selectedFile = GetSelectedDetailFile(item.Id);
            if (selectedFile != null)
            {
                BuildFileDetail(_itemDetailPane, selectedFile);
                return;
            }

            BuildItemOverview(
                _itemDetailPane,
                item,
                files ?? GetFiles(item.Id));
        }

        private AssetFile GetSelectedDetailFile(string itemId)
        {
            if (string.IsNullOrEmpty(_viewState.SelectedFileId))
            {
                return null;
            }

            try
            {
                var file = _manager.GetFile(_viewState.SelectedFileId);
                return file != null && string.Equals(
                    file.ItemId,
                    itemId,
                    StringComparison.Ordinal)
                    ? file
                    : null;
            }
            catch (AssetManagerException exception) when (
                exception.Code == AssetManagerErrorCode.NotFound)
            {
                return null;
            }
        }

        private void BuildItemOverview(
            VisualElement detail,
            AssetItem item,
            IReadOnlyList<AssetFile> files)
        {
            var hero = new VisualElement();
            hero.AddToClassList("ee4v-asset-manager__overview-hero");
            var thumbnail = new AssetThumbnailStack(
                _imageCache,
                new[] { item.Id });
            thumbnail.AddToClassList(
                "ee4v-asset-manager__overview-thumbnail");
            _itemOverviewThumbnailStack = thumbnail;
            hero.Add(thumbnail);

            var summary = new InfoCard(new InfoCardState(
                item.Name,
                null,
                I18N.Get("detail.item.eyebrow"),
                CreateAssetStatusState(item.IsArchived)));
            summary.AddToClassList("ee4v-asset-manager__overview-summary");
            summary.TitleText.SetFontSize(UiTypographyTokens.TitleFontSize);

            var tagPaths = GetTagPaths(item);
            var tags = new VisualElement();
            tags.AddToClassList("ee4v-asset-manager__overview-tags");
            if (tagPaths.Count == 0)
            {
                tags.Add(UiTextFactory.Create(
                    I18N.Get("common.none"),
                    UiClassNames.SecondaryText,
                    "ee4v-asset-manager__overview-empty-tags"));
            }
            else
            {
                for (var index = 0; index < tagPaths.Count; index++)
                {
                    var tag = UiTextFactory.Create(
                        tagPaths[index],
                        UiClassNames.SecondaryText,
                        "ee4v-asset-manager__overview-tag");
                    tag.SetFontSize(UiTypographyTokens.BodyFontSize);
                    tags.Add(tag);
                }
            }
            summary.Body.Add(tags);
            var importButton = AssetManagerControls.CreateButton(
                I18N.Get("action.import"),
                null,
                "ee4v-asset-manager__primary-action",
                "ee4v-asset-manager__overview-import");
            importButton.clicked += () => ShowItemTargetImport(
                importButton,
                item.Id,
                files);
            importButton.SetEnabled(HasItemTargets(item.Id));
            hero.Add(summary);
            hero.Add(importButton);
            detail.Add(hero);
            _ = LoadItemOverviewThumbnailAsync(item, thumbnail);

            var facts = new VisualElement();
            facts.AddToClassList("ee4v-asset-manager__overview-facts");
            facts.Add(new AssetDetailFact(
                I18N.Get("field.fileCount"),
                files.Count.ToString()));
            facts.Add(new AssetDetailFact(
                I18N.Get("field.fileTypes"),
                GetFileTypes(files)));
            var sourceFact = new AssetDetailFact(
                I18N.Get("field.source"),
                GetItemSources(item, files));
            sourceFact.AddToClassList(
                "ee4v-asset-manager__overview-fact--last");
            facts.Add(sourceFact);
            detail.Add(facts);

            var importSettings = new AssetDetailSection(
                I18N.Get("detail.importSettings"));
            var targetTree = CreateTargetList(item.Id, files);
            UiButton editTargets = null;
            editTargets = AssetManagerControls.CreateButton(
                I18N.Get("action.edit"),
                () => ShowItemTargetEditor(
                    editTargets,
                    item.Id,
                    files),
                "ee4v-asset-manager__inline-action");
            var targetSetting = new AssetDetailSettingRow(
                I18N.Get("field.target"),
                targetTree,
                editTargets);
            targetSetting.AddToClassList(
                "ee4v-asset-manager__target-setting");
            RegisterTargetDrop(
                targetSetting,
                payload => SetTargetGroups(
                    item.Id,
                    payload.Targets,
                    null));
            var targetList = new AssetDetailSettingList();
            targetList.Add(targetSetting);
            importSettings.Add(targetList);
            detail.Add(importSettings);

            AddDerivedAssets(detail, item.Id);
            AddItemInformation(detail, item, files);
        }

        private void AddDerivedAssets(
            VisualElement detail,
            string itemId)
        {
            var section = new AssetDetailSection(
                I18N.Get("detail.derivedAssets"));
            var grid = new VisualElement();
            grid.AddToClassList(
                "ee4v-asset-manager__derived-assets-grid");
            foreach (var derivedAsset in
                     GetVariants()
                         .Where(variant => string.Equals(
                             variant.ParentItemId, itemId, StringComparison.Ordinal))
                         .OrderBy(variant => variant.Name, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(variant => variant.VariantId, StringComparer.Ordinal))
            {
                var assetCard = new AssetItemGridCard(_imageCache);
                assetCard.SetWidth(DerivedAssetCardWidth);
                assetCard.SetPlaceholderIcon(
                    AssetManagerControls.LoadFluentIconState(
                        "cube.png",
                        UiSizeTokens.Size24,
                        derivedAsset.Name,
                        UiColorTokens.TextMuted));
                assetCard.SetState(
                    new AssetItemGridEntry(
                        derivedAsset.VariantId,
                        derivedAsset.Name),
                    selected: false);
                assetCard.tooltip = derivedAsset.Description;
                assetCard.Clicked += (_, __, ___) =>
                {
                    if (derivedAsset.Prefab == null) { return; }
                    Selection.activeObject = derivedAsset.Prefab;
                    EditorGUIUtility.PingObject(derivedAsset.Prefab);
                };
                assetCard.DoubleClicked += _ =>
                {
                    _viewState.OpenVariantDetail(derivedAsset.VariantId);
                };
                assetCard.RegisterCallback<DetachFromPanelEvent>(_ =>
                    assetCard.Dispose());
                grid.Add(assetCard);
                _ = LoadDerivedVariantThumbnailAsync(assetCard, derivedAsset);
            }
            var addCard = new AssetItemGridCard(_imageCache);
            addCard.AddToClassList(
                "ee4v-asset-manager__derived-assets-add-card");
            addCard.SetWidth(DerivedAssetCardWidth);
            addCard.SetPlaceholderIcon(
                AssetManagerControls.LoadFluentIconState(
                    "add.png",
                    UiSizeTokens.Size24,
                    I18N.Get("detail.derivedAssetsAdd"),
                    UiColorTokens.TextMuted));
            addCard.SetState(
                new AssetItemGridEntry(
                    "derived-assets-add:" + itemId,
                    I18N.Get("detail.derivedAssetsAdd")),
                selected: false);
            addCard.Clicked += (_, __, ___) =>
            {
                if (_createDerivedAsset != null)
                {
                    _createDerivedAsset(itemId);
                }
                else
                {
                    _viewState.OpenDerivedAssetsPage(itemId);
                }
            };
            addCard.RegisterCallback<DetachFromPanelEvent>(_ =>
                addCard.Dispose());
            grid.Add(addCard);
            section.Add(grid);
            detail.Add(section);
        }

        private static void AddItemInformation(
            VisualElement detail,
            AssetItem item,
            IReadOnlyList<AssetFile> files)
        {
            var information = new AssetDetailSection(
                I18N.Get("detail.information"));
            AddBoothInformation(information, item.Booth);
            information.Add(new AssetDetailKeyValueRow(
                I18N.Get("field.totalFileSize"),
                FormatTotalFileSize(files)));
            information.Add(new AssetDetailKeyValueRow(
                I18N.Get("field.createdAt"),
                FormatTimestamp(item.CreatedAt)));
            information.Add(new AssetDetailKeyValueRow(
                I18N.Get("field.updatedAt"),
                FormatTimestamp(item.UpdatedAt)));
            detail.Add(information);
        }

        private static void AddBoothInformation(
            VisualElement information,
            AssetBoothMetadata booth)
        {
            if (booth == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(booth.ShopName) &&
                TryGetWebUrl(booth.ShopUrl, out var shopUrl))
            {
                information.Add(new AssetDetailKeyValueRow(
                    I18N.Get("field.boothStore"),
                    CreateDetailLink(booth.ShopName, shopUrl)));
            }
            if (TryGetWebUrl(booth.ItemUrl, out var itemUrl))
            {
                information.Add(new AssetDetailKeyValueRow(
                    I18N.Get("field.boothLink"),
                    CreateDetailLink(itemUrl, itemUrl)));
            }
        }

        private static UiButton CreateDetailLink(
            string label,
            string url)
        {
            var link = AssetManagerControls.CreateButton(
                label,
                () => Application.OpenURL(url),
                "ee4v-asset-manager__detail-link");
            link.SetLabelColor(UiColorTokens.Focus);
            link.SetLabelTextAlign(TextAnchor.MiddleLeft);
            link.tooltip = url;
            return link;
        }

        private static bool TryGetWebUrl(
            string value,
            out string url)
        {
            url = null;
            if (!Uri.TryCreate(
                    value?.Trim(),
                    UriKind.Absolute,
                    out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            url = uri.AbsoluteUri;
            return true;
        }

        private static string FormatTotalFileSize(
            IReadOnlyList<AssetFile> files)
        {
            long total = 0;
            var source = files ?? Array.Empty<AssetFile>();
            for (var index = 0; index < source.Count; index++)
            {
                var path = source[index]?.SourcePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return I18N.Get("common.unknown");
                }

                try
                {
                    total = checked(total + new FileInfo(path).Length);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is NotSupportedException ||
                    exception is ArgumentException ||
                    exception is OverflowException)
                {
                    return I18N.Get("common.unknown");
                }
            }

            return AssetFileTreeBuilder.FormatSize(total);
        }

        private VisualElement CreateTargetList(
            string itemId,
            IReadOnlyList<AssetFile> files)
        {
            var list = new VisualElement();
            list.AddToClassList("ee4v-asset-manager__target-list");
            var filesById = (files ?? Array.Empty<AssetFile>())
                .ToDictionary(file => file.Id, StringComparer.Ordinal);
            var entries = _manager.GetItemTargets(itemId)
                .Where(target => filesById.ContainsKey(target.FileId))
                .Select(target => new ItemTargetEntry
                {
                    File = filesById[target.FileId],
                    Target = target
                })
                .ToArray();
            if (entries.Length == 0)
            {
                list.Add(UiTextFactory.Create(I18N.Get("common.none")));
                return list;
            }

            var grouped = entries
                .Where(entry => !string.IsNullOrWhiteSpace(
                    entry.Target.GroupName))
                .GroupBy(
                    entry => entry.Target.GroupName,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var ungrouped = entries
                .Where(entry => string.IsNullOrWhiteSpace(
                    entry.Target.GroupName))
                .ToArray();
            for (var index = 0; index < ungrouped.Length; index++)
            {
                list.Add(CreateTargetRow(
                    itemId,
                    ungrouped[index],
                    entries,
                    false));
            }

            for (var groupIndex = 0;
                 groupIndex < grouped.Length;
                 groupIndex++)
            {
                var groupEntries = grouped[groupIndex].ToArray();
                list.Add(CreateTargetGroupRow(
                    itemId,
                    grouped[groupIndex].Key,
                    grouped[groupIndex].Key,
                    groupEntries.Select(entry => entry.Target).ToArray()));
                for (var entryIndex = 0;
                     entryIndex < groupEntries.Length;
                     entryIndex++)
                {
                    list.Add(CreateTargetRow(
                        itemId,
                        groupEntries[entryIndex],
                        entries,
                        true));
                }
            }

            RegisterTargetDrop(
                list,
                payload => SetTargetGroups(
                    itemId,
                    payload.Targets,
                    null));
            return list;
        }

        private VisualElement CreateTargetGroupRow(
            string itemId,
            string label,
            string groupName,
            IReadOnlyList<AssetFileTarget> targets)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-asset-manager__target-group-row");
            var icon = new Image
            {
                image = AssetManagerControls.LoadFluentIconTexture(
                    "folder.png"),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("ee4v-asset-manager__target-icon");
            row.Add(icon);
            row.Add(UiTextFactory.Create(
                label,
                UiClassNames.NavigationItemLabel,
                "ee4v-asset-manager__target-group-name"));
            row.Add(UiTextFactory.Create(
                targets.Count.ToString(),
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__target-group-count"));
            RegisterTargetDrag(
                row,
                new TargetDragPayload
                {
                    GroupName = groupName,
                    Targets = targets
                },
                label);
            RegisterTargetDrop(
                row,
                payload => SetTargetGroups(
                    itemId,
                    payload.Targets,
                    groupName));
            return row;
        }

        private VisualElement CreateTargetRow(
            string itemId,
            ItemTargetEntry entry,
            IReadOnlyList<ItemTargetEntry> entries,
            bool grouped)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-asset-manager__target-item");
            row.EnableInClassList(
                "ee4v-asset-manager__target-tree-child",
                grouped);

            var icon = new Image
            {
                image = AssetManagerControls.LoadFluentIconTexture(
                    string.IsNullOrEmpty(entry.Target.TargetPath)
                        ? "folder_zip.png"
                        : "cube.png"),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("ee4v-asset-manager__target-icon");
            row.Add(icon);
            var value = UiTextFactory.Create(
                FormatTargetName(entry.File, entry.Target),
                UiClassNames.NavigationItemLabel,
                "ee4v-asset-manager__target-value");
            value.tooltip = (entry.File.FileName ?? entry.File.Id) +
                            " / " +
                            (string.IsNullOrEmpty(entry.Target.TargetPath)
                                ? entry.File.FileName
                                : entry.Target.TargetPath);
            row.Add(value);

            RegisterTargetDrag(
                row,
                new TargetDragPayload
                {
                    GroupName = entry.Target.GroupName,
                    Targets = new[] { entry.Target }
                },
                FormatTargetName(entry.File, entry.Target));
            RegisterTargetDrop(
                row,
                payload => GroupDroppedTargets(
                    itemId,
                    payload,
                    entry.Target,
                    entries));
            return row;
        }

        private void GroupDroppedTargets(
            string itemId,
            TargetDragPayload payload,
            AssetFileTarget target,
            IReadOnlyList<ItemTargetEntry> entries)
        {
            if (payload?.Targets == null ||
                payload.Targets.Any(source =>
                    AssetFileTarget.HasSameIdentity(source, target)))
            {
                return;
            }

            var groupName = target.GroupName;
            var targets = payload.Targets.ToList();
            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = string.IsNullOrWhiteSpace(payload.GroupName)
                    ? CreateTargetGroupName(entries)
                    : payload.GroupName;
                targets.Add(target);
            }
            SetTargetGroups(itemId, targets, groupName);
        }

        private static string CreateTargetGroupName(
            IReadOnlyList<ItemTargetEntry> entries)
        {
            var names = new HashSet<string>(
                (entries ?? Array.Empty<ItemTargetEntry>())
                    .Select(entry => entry.Target.GroupName)
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            for (var index = 1; ; index++)
            {
                var candidate = "Group " + index;
                if (!names.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        private void SetTargetGroups(
            string itemId,
            IReadOnlyList<AssetFileTarget> targets,
            string groupName)
        {
            var changes = (targets ?? Array.Empty<AssetFileTarget>())
                .Where(target => target != null && !string.Equals(
                    target.GroupName,
                    groupName,
                    StringComparison.OrdinalIgnoreCase))
                .GroupBy(
                    target => target.FileId + "\n" + target.TargetPath,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            if (changes.Length == 0)
            {
                return;
            }

            Run(() =>
            {
                for (var index = 0; index < changes.Length; index++)
                {
                    _manager.SetItemTargetGroup(
                        itemId,
                        changes[index].FileId,
                        changes[index].TargetPath,
                        groupName);
                }
            });
        }

        private static void RegisterTargetDrag(
            VisualElement element,
            TargetDragPayload payload,
            string label)
        {
            UiDragAndDrop.RegisterStart(
                element,
                TargetDragDataKey,
                () => payload,
                _ => label);
        }

        private static void RegisterTargetDrop(
            VisualElement element,
            Action<TargetDragPayload> onDrop)
        {
            UiDragAndDrop.RegisterMoveTarget(
                element,
                TargetDragDataKey,
                null,
                onDrop,
                active => element.EnableInClassList(
                    "ee4v-asset-manager__target-drop",
                    active));
        }

        private static string FormatTargetName(
            AssetFile file,
            AssetFileTarget target)
        {
            var fileName = string.IsNullOrWhiteSpace(file?.FileName)
                ? file?.Id ?? "file"
                : file.FileName;
            if (string.IsNullOrEmpty(target?.TargetPath))
            {
                return fileName;
            }

            var targetName = Path.GetFileName(target.TargetPath);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = target.TargetPath;
            }
            return string.Equals(
                    targetName,
                    fileName,
                    StringComparison.OrdinalIgnoreCase)
                ? targetName
                : targetName + "(" + fileName + ")";
        }

        private void BuildFileEntryDetail(
            VisualElement detail,
            AssetFile file,
            AssetFileContentEntry entry)
        {
            var header = new AssetDetailHeader(
                Path.GetFileName(entry.Path),
                I18N.Get("detail.file.entryEyebrow") + " · " +
                I18N.Get(entry.Kind == AssetFileContentEntryKind.Directory
                    ? "detail.file.directory"
                    : "detail.file.file"),
                (file?.FileName ?? I18N.Get("common.none")) + " / " +
                entry.Path);
            detail.Add(header);

            var information = new AssetDetailSection(
                I18N.Get("detail.entryInformation"));
            var settingList = new AssetDetailSettingList();
            settingList.Add(new AssetDetailSettingRow(
                I18N.Get("field.file"),
                UiTextFactory.Create(
                    file?.FileName ?? I18N.Get("common.none"))));
            settingList.Add(new AssetDetailSettingRow(
                I18N.Get("field.kind"),
                UiTextFactory.Create(I18N.Get(
                    entry.Kind == AssetFileContentEntryKind.Directory
                        ? "detail.file.directory"
                        : "detail.file.file"))));
            settingList.Add(new AssetDetailSettingRow(
                I18N.Get("field.size"),
                UiTextFactory.Create(
                    entry.SizeBytes.ToString("N0") + " B")));
            settingList.Add(new AssetDetailSettingRow(
                I18N.Get("field.imported"),
                UiTextFactory.Create(I18N.Get(
                    IsEntryImported(file, entry)
                        ? "common.yes"
                        : "common.no"))));
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, entry)))
            {
                AddDependencySetting(settingList, new[]
                {
                    new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = entry.Path
                    }
                });
            }
            information.Add(settingList);
            if (SearchableFileTree.CanImport(
                    new FileTreeSelection(file, entry)))
            {
                information.Add(AssetManagerControls.CreateButton(
                    I18N.Get("action.importEntry"),
                    () => ImportEntries(file.Id, entry.Path),
                    "ee4v-asset-manager__primary-action"));
            }
            detail.Add(information);
        }

        private void BuildFileTreeSelectionDetail(
            VisualElement detail,
            IReadOnlyList<FileTreeSelection> selections)
        {
            var importable = selections
                .Where(SearchableFileTree.CanImport)
                .ToArray();
            var files = selections
                .Where(selection => selection.File != null)
                .Select(selection => selection.File)
                .GroupBy(file => file.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            if (files.Length == 0)
            {
                detail.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.selectFile")));
                return;
            }

            var firstFile = files[0];
            var extension = GetFileExtension(firstFile);
            var commonExtension = files.All(file => string.Equals(
                GetFileExtension(file),
                extension,
                StringComparison.OrdinalIgnoreCase));
            var commonSource = files.All(file =>
                file.SourceType == firstFile.SourceType);
            var eyebrow = I18N.Get("detail.file.eyebrow") +
                          (commonExtension && !string.IsNullOrEmpty(extension)
                              ? " · " + extension.ToUpperInvariant()
                              : string.Empty) +
                          (commonSource
                              ? " · " + firstFile.SourceType.ToString()
                                  .ToUpperInvariant()
                              : string.Empty);
            var allArchived = files.All(file => file.IsArchived);
            var fileIds = files.Select(file => file.Id).ToArray();
            var header = new AssetDetailHeader(
                files.Length == 1
                    ? firstFile.FileName
                    : string.Format(
                        I18N.Get("detail.fileTreeSelectedCount"),
                        selections.Count),
                eyebrow,
                files.Length == 1 ? firstFile.SourcePath : null,
                CreateAssetStatusState(allArchived));
            if (importable.Length > 0)
            {
                header.AddAction(AssetManagerControls.CreateButton(
                    I18N.Get("action.import"),
                    () => _ = ImportFileTreeEntriesAsync(importable),
                    "ee4v-asset-manager__primary-action"));
            }
            header.AddAction(AssetManagerControls.CreateButton(
                I18N.Get(allArchived
                    ? "action.restore"
                    : "action.archive"),
                () => ArchiveFiles(fileIds, !allArchived)));
            if (allArchived)
            {
                header.AddAction(AssetManagerControls.CreateDangerButton(
                    I18N.Get("action.delete"),
                    () => DeleteFiles(fileIds)));
            }
            detail.Add(header);
            BuildFileSettings(
                detail,
                files,
                importable.Select(selection => new AssetFileTarget
                {
                    FileId = selection.File.Id,
                    TargetPath = selection.Entry?.Path ?? string.Empty
                }).GroupBy(target =>
                        target.FileId + "\n" + target.TargetPath,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToArray(),
                showFileId: false);
        }

        private void OnFileTreeSelectionChanged(
            IReadOnlyList<FileTreeSelection> selection)
        {
            _fileTreeSelections = selection ??
                Array.Empty<FileTreeSelection>();
            _viewState.SelectFile(_fileTreeSelections.Count == 1
                ? _fileTreeSelections[0].File?.Id
                : null);
        }

        private void BuildUnassignedFiles()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.None;
            var files = AssetManagerItemSort.Apply(
                GetUnassignedFiles()
                    .Where(file => AssetManagerSearch.MatchesFile(
                        file,
                        _search.Value,
                        _viewState.SearchTargets)),
                _viewState.ItemSortField,
                _viewState.IsItemSortReversed);
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("ee4v-asset-manager__file-list");
            if (files.Count == 0)
            {
                list.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.noUnassignedFiles")));
            }
            else
            {
                for (var index = 0; index < files.Count; index++)
                {
                    list.Add(CreateFileButton(files[index]));
                }
            }
            _content.Add(list);
        }

        private UiButton CreateFileButton(AssetFile file)
        {
            var selected = string.Equals(
                file.Id,
                _viewState.SelectedFileId,
                StringComparison.Ordinal);
            var button = AssetManagerControls.CreateButton(
                file.FileName,
                () => SelectFile(file),
                "ee4v-asset-manager__file-button");
            button.EnableInClassList(
                "ee4v-asset-manager__file-button--selected",
                selected);
            button.SetLabelColor(selected
                ? UiColorTokens.TextOnState
                : UiColorTokens.TextPrimary);
            var meta = UiTextFactory.Create(
                GetFileMeta(file),
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__file-meta");
            meta.SetWhiteSpace(WhiteSpace.NoWrap);
            button.Add(meta);
            button.tooltip = file.FileName;
            return button;
        }

        private static UiTextElement CreateMonoValue(string value)
        {
            var text = UiTextFactory.Create(
                value,
                "ee4v-asset-manager__mono");
            text.SetWhiteSpace(WhiteSpace.Normal);
            return text;
        }

        private bool IsEntryImported(
            AssetFile file,
            AssetFileContentEntry entry)
        {
            if (file == null || entry == null)
            {
                return false;
            }

            var importedGuids = _manager.GetFileImportedAssetGuids(file.Id);
            if (string.Equals(
                    Path.GetExtension(entry.Path),
                    ".unitypackage",
                    StringComparison.OrdinalIgnoreCase))
            {
                return importedGuids.Count > 0;
            }

            if (!string.IsNullOrWhiteSpace(entry.AssetGuid))
            {
                return importedGuids.Contains(
                    entry.AssetGuid,
                    StringComparer.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                return false;
            }

            var targetPath = entry.Path
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
            if (targetPath.EndsWith(
                    ".meta",
                    StringComparison.OrdinalIgnoreCase))
            {
                targetPath = targetPath.Substring(
                    0,
                    targetPath.Length - ".meta".Length);
            }

            return importedGuids.Any(guid => IsEntryAssetPath(
                    AssetDatabase.GUIDToAssetPath(guid),
                    targetPath));
        }

        private static bool IsEntryAssetPath(
            string assetPath,
            string targetPath)
        {
            var normalizedAssetPath = (assetPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
            return targetPath.Length > 0 &&
                   (string.Equals(
                        normalizedAssetPath,
                        targetPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    normalizedAssetPath.EndsWith(
                        "/" + targetPath,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string GetFileMeta(AssetFile file)
        {
            var extension = GetFileExtension(file);
            return string.IsNullOrEmpty(extension)
                ? file.SourceType.ToString()
                : extension.ToUpperInvariant();
        }

        private static string GetFileTypes(IReadOnlyList<AssetFile> files)
        {
            var extensions = (files ?? Array.Empty<AssetFile>())
                .Where(file => file != null)
                .Select(GetFileExtension)
                .Where(extension => extension.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
                .Select(extension => extension.ToUpperInvariant())
                .ToArray();
            return extensions.Length == 0
                ? I18N.Get("common.none")
                : string.Join(", ", extensions);
        }

        private static string GetItemSources(
            AssetItem item,
            IReadOnlyList<AssetFile> files)
        {
            var sources = (files ?? Array.Empty<AssetFile>())
                .Where(file => file != null)
                .Select(file => file.SourceType.ToString())
                .Concat(item != null && item.SourceType.HasValue
                    ? new[] { item.SourceType.Value.ToString() }
                    : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return sources.Length == 0
                ? I18N.Get("common.none")
                : string.Join(", ", sources);
        }

        private static string GetFileExtension(AssetFile file)
        {
            if (file == null)
            {
                return string.Empty;
            }

            var extension = file.Extension;
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(file.FileName);
            }
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(file.SourcePath);
            }
            return (extension ?? string.Empty).Trim().TrimStart('.');
        }

        private static string FormatTimestamp(DateTime value)
        {
            return value == default
                ? I18N.Get("common.none")
                : value.ToString("g");
        }

        private void ShowCollectionContextMenu(
            VisualElement anchor,
            AssetCollection collection)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get("action.edit")),
                false,
                () => ShowCollectionEditor(anchor, collection));
            var ids = _manager.GetCollections()
                .Select(entry => entry.Id).ToList();
            var index = ids.IndexOf(collection.Id);
            AddCollectionMoveMenuItem(
                menu, collection.Id, "navigation.moveCollectionUp",
                -1, index > 0);
            AddCollectionMoveMenuItem(
                menu, collection.Id, "navigation.moveCollectionDown",
                1, index >= 0 && index < ids.Count - 1);
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get("action.delete")),
                false,
                () => DeleteCollection(collection?.Id));
            menu.ShowAsContext();
        }

        private void RegisterCollectionReordering(
            NavigationItem row,
            AssetCollection collection)
        {
            row.tooltip = collection.Name + "\n" +
                I18N.Get("navigation.reorderCollection");
            row.AddManipulator(new CollectionDragManipulator(
                () => new CollectionDragPayload
                {
                    Manager = _manager,
                    CollectionId = collection.Id,
                    Name = collection.Name
                }));

            var insertAfter = false;
            row.RegisterCallback<DragUpdatedEvent>(evt =>
                insertAfter = evt.mousePosition.y >= row.worldBound.center.y);
            UiDragAndDrop.RegisterMoveTarget<CollectionDragPayload>(
                row,
                CollectionDragDataKey,
                payload => ReferenceEquals(payload.Manager, _manager) &&
                    !string.Equals(payload.CollectionId, collection.Id,
                        StringComparison.Ordinal),
                payload => MoveCollection(
                    payload.CollectionId, collection.Id, insertAfter),
                active =>
                {
                    row.EnableInClassList(
                        "ee4v-asset-manager__collection-drop-before",
                        active && !insertAfter);
                    row.EnableInClassList(
                        "ee4v-asset-manager__collection-drop-after",
                        active && insertAfter);
                });
        }

        private void AddCollectionMoveMenuItem(
            GenericMenu menu,
            string collectionId,
            string labelKey,
            int offset,
            bool enabled)
        {
            var label = UiTextFactory.CreateGuiContent(I18N.Get(labelKey));
            if (!enabled)
            {
                menu.AddDisabledItem(label);
                return;
            }
            menu.AddItem(label, false, () =>
            {
                var ids = _manager.GetCollections()
                    .Select(collection => collection.Id).ToList();
                var index = ids.IndexOf(collectionId);
                var destination = index + offset;
                if (index < 0 || destination < 0 || destination >= ids.Count)
                {
                    return;
                }
                MoveCollection(collectionId, ids[destination], offset > 0);
            });
        }

        private void MoveCollection(
            string collectionId,
            string targetId,
            bool insertAfter)
        {
            Run(() =>
            {
                var ids = _manager.GetCollections()
                    .Select(collection => collection.Id).ToList();
                var originalIndex = ids.IndexOf(collectionId);
                if (originalIndex < 0 || !ids.Contains(targetId) ||
                    string.Equals(collectionId, targetId, StringComparison.Ordinal))
                {
                    return;
                }
                ids.RemoveAt(originalIndex);
                var destination = ids.IndexOf(targetId) + (insertAfter ? 1 : 0);
                ids.Insert(destination, collectionId);
                if (destination != originalIndex)
                {
                    _manager.ReorderCollections(ids);
                }
            });
        }

        private void ShowNewCollection(VisualElement anchor)
        {
            AssetCollectionCreationPopup.Show(
                anchor,
                null,
                CreateCollection);
        }

        private void ShowCollectionEditor(
            VisualElement anchor,
            AssetCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            AssetCollectionCreationPopup.Show(
                anchor,
                collection,
                (name, icon, root) => UpdateCollection(
                    collection.Id,
                    name,
                    icon,
                    root));
        }

        private void SaveItemMetadataAutomatically(
            string id,
            string name,
            string description)
        {
            var item = _manager.GetItem(id);
            if (item == null)
            {
                return;
            }

            var normalizedName = (name ?? string.Empty).Trim();
            var normalizedDescription = description ?? string.Empty;
            var metadataChanged =
                !string.Equals(
                    normalizedName,
                    item.Name,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    normalizedDescription,
                    item.Description ?? string.Empty,
                    StringComparison.Ordinal);
            if (!metadataChanged)
            {
                return;
            }

            var succeeded = RunWithoutDetailRefresh(() =>
            {
                _manager.UpdateItem(id, new UpdateAssetItemRequest
                {
                    Name = normalizedName,
                    Description = normalizedDescription
                });
            });
            RefreshAfterItemAutoSave(succeeded);
        }

        private void SaveVariantMetadataAutomatically(DerivedAssetInfo variant, string name,
            string description, VisualElement error)
        {
            if (_variantManager == null || _variantBusy) { return; }
            if (_variantMetadataSaveCount == 0 && variant.Name == name &&
                (variant.Description ?? string.Empty) == description) { return; }
            var queue = VariantMetadataSaveQueues.GetValue(_variantManager, _ => new VariantMetadataSaveQueue());
            var previousSave = queue.Pending;
            _variantMetadataSaveCount++;
            _variantMetadataSaveTask = SaveVariantMetadataAsync(previousSave, variant,
                new UpdateAssetVariantRequest { Name = name, Description = description }, error);
            queue.Pending = _variantMetadataSaveTask;
        }

        private Task PendingVariantMetadataSave => _variantManager == null ? Task.CompletedTask :
            VariantMetadataSaveQueues.GetValue(_variantManager, _ => new VariantMetadataSaveQueue()).Pending;

        private async Task SaveVariantMetadataAsync(Task previousSave, DerivedAssetInfo variant,
            UpdateAssetVariantRequest request, VisualElement error)
        {
            try
            {
                await previousSave;
                var current = GetVariants().FirstOrDefault(candidate => candidate.VariantId == variant.VariantId);
                if (current == null || (current.Name == request.Name &&
                    (current.Description ?? string.Empty) == request.Description)) { return; }
                await _variantManager.UpdateMetadata(variant.VariantId, request);
                variant.Name = request.Name;
                variant.Description = request.Description;
                if (variant.Prefab != null) { variant.AssetPath = AssetDatabase.GetAssetPath(variant.Prefab); }
                error.Clear();
            }
            catch (Exception exception)
            {
                error.Clear();
                error.Add(UiTextFactory.CreateHelpBox(exception.Message, HelpBoxMessageType.Error));
                Debug.LogException(exception);
            }
            finally
            {
                _variantMetadataSaveCount--;
                if (!_disposed && _variantMetadataSaveCount == 0)
                {
                    if (ShowsMain)
                    {
                        if (string.IsNullOrEmpty(_viewState.DetailVariantId)) { RefreshMain(); }
                        else
                        {
                            if (_viewState.DetailVariantId == variant.VariantId)
                            {
                                _content.Q<UiTextElement>(className: "ee4v-asset-manager__variant-overview-title")
                                    ?.SetText(variant.Name);
                                var description = _content.Q<UiTextElement>(
                                    className: "ee4v-asset-manager__variant-overview-description");
                                if (description != null)
                                {
                                    description.SetText(variant.Description);
                                    description.tooltip = variant.Description;
                                    description.parent.style.display = string.IsNullOrWhiteSpace(variant.Description)
                                        ? DisplayStyle.None : DisplayStyle.Flex;
                                }
                            }
                            RefreshHistoryNavigation();
                        }
                    }
                    if (ShowsInformation &&
                        (_viewState.DetailVariantId ?? _viewState.SelectedVariantId) != variant.VariantId)
                    {
                        RefreshDetail();
                    }
                }
            }
        }

        private void SaveItemTagsAutomatically(
            string id,
            IReadOnlyList<string> tags)
        {
            var item = _manager.GetItem(id);
            if (item == null)
            {
                return;
            }

            var normalizedTags = (tags ?? Array.Empty<string>())
                .Select(tag => (tag ?? string.Empty).Trim())
                .Where(tag => tag.Length > 0)
                .Where(tag => !GetSourceTagPaths(item).Contains(
                    tag, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedTags.SequenceEqual(
                    GetEditableTagPaths(item),
                    StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            var succeeded = RunWithoutDetailRefresh(() =>
                _manager.SetItemTags(new[] { id }, normalizedTags));
            RefreshAfterItemAutoSave(succeeded);
        }

        private void RefreshAfterItemAutoSave(bool succeeded)
        {
            if (!succeeded)
            {
                return;
            }

            if (ShowsNavigation)
            {
                RebuildNavigation();
            }
            if (ShowsMain)
            {
                RefreshMain();
            }
        }

        private IReadOnlyList<AssetTagOption> GetAvailableTagOptions()
        {
            var usageCounts = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            var items = _manager.SearchItems(new AssetItemQuery
            {
                IncludeArchived = true
            }).Items;
            foreach (var item in items)
            {
                foreach (var path in GetTagPaths(item).Distinct(
                             StringComparer.OrdinalIgnoreCase))
                {
                    usageCounts.TryGetValue(path, out var count);
                    usageCounts[path] = count + 1;
                }
            }

            return (_manager.GetTags() ?? Array.Empty<AssetTag>())
                .Where(tag =>
                    tag != null &&
                    !string.IsNullOrWhiteSpace(tag.Path))
                .Select(tag => tag.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new AssetTagOption(
                    path,
                    usageCounts.TryGetValue(path, out var count)
                        ? count
                        : 0))
                .OrderByDescending(option => option.UsageCount)
                .ThenBy(
                    option => option.Path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<string> GetTagPaths(AssetItem item)
        {
            return (item?.Tags ?? Array.Empty<AssetTag>())
                .Where(tag =>
                    tag != null &&
                    !string.IsNullOrWhiteSpace(tag.Path))
                .Select(tag => tag.Path)
                .ToArray();
        }

        private static IReadOnlyList<string> GetSourceTagPaths(
            AssetItem item)
        {
            return (item?.Tags ?? Array.Empty<AssetTag>())
                .Where(tag => tag != null && tag.IsSourceOwned)
                .Select(tag => tag.Path)
                .ToArray();
        }

        private static IReadOnlyList<string> GetEditableTagPaths(
            AssetItem item)
        {
            return (item?.Tags ?? Array.Empty<AssetTag>())
                .Where(tag => tag != null && !tag.IsSourceOwned)
                .Select(tag => tag.Path)
                .ToArray();
        }

        private void SetItemsArchived(
            IReadOnlyList<string> itemIds,
            bool archived)
        {
            Run(() =>
                _manager.SetItemArchived(itemIds, archived));
        }

        private void DeleteItems(IReadOnlyList<string> itemIds)
        {
            var multiple = itemIds != null && itemIds.Count > 1;
            if (!Confirm(
                    I18N.Get(multiple
                        ? "confirm.deleteItems.title"
                        : "confirm.deleteItem.title"),
                    I18N.Get(
                        multiple
                            ? "confirm.deleteItems.message"
                            : "confirm.deleteItem.message",
                        itemIds?.Count ?? 0)))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteItem(itemIds);
                _viewState.SelectItem(null);
            });
        }

        private void RegisterFile(
            string itemId,
            string filePath,
            string fileName)
        {
            Run(() => _manager.RegisterFile(
                itemId,
                new RegisterFileRequest
                {
                    LibraryPath = AssetManagerSettings.Ee4vLibraryPath,
                    FilePath = filePath,
                    FileName = fileName
                }));
        }

        private void RegisterCurrentItemFile()
        {
            var itemId = _viewState.DetailItemId;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            var filePath = EditorUtility.OpenFilePanel(
                I18N.Get("detail.file.register"),
                string.Empty,
                string.Empty);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            RegisterFile(
                itemId,
                filePath,
                Path.GetFileName(filePath));
        }

        private void MoveFiles(
            IReadOnlyList<string> fileIds,
            string itemId)
        {
            Run(() =>
                _manager.SetFileItem(
                    fileIds,
                    string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim()));
        }

        private void ArchiveFile(string id, bool archived)
        {
            ArchiveFiles(new[] { id }, archived);
        }

        private void ArchiveFiles(
            IReadOnlyList<string> fileIds,
            bool archived)
        {
            Run(() =>
                _manager.SetFileArchived(fileIds, archived));
        }

        private void DeleteFile(string id)
        {
            DeleteFiles(new[] { id });
        }

        private void DeleteFiles(IReadOnlyList<string> fileIds)
        {
            if (!Confirm(
                    I18N.Get(fileIds.Count == 1
                        ? "confirm.deleteFile.title"
                        : "confirm.deleteFiles.title"),
                    fileIds.Count == 1
                        ? I18N.Get("confirm.deleteFile.message")
                        : string.Format(
                            I18N.Get("confirm.deleteFiles.message"),
                            fileIds.Count)))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteFile(fileIds);
                _fileTreeSelections = Array.Empty<FileTreeSelection>();
                _viewState.SelectFile(null);
            });
        }

        private void SaveDependencies(
            IReadOnlyList<AssetFileTarget> sources,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            Run(() =>
                _manager.SetFileDependencies(
                    sources,
                    dependencyTargets));
        }

        private void ImportEntries(string fileId, string paths)
        {
            _ = RunImport(
                () => _manager.ImportFileEntries(fileId, SplitLines(paths)));
        }

        private void ImportFileTreeEntry(FileTreeSelection selection)
        {
            if (!SearchableFileTree.CanImport(selection))
            {
                return;
            }

            _ = ImportFileTreeEntriesAsync(new[] { selection });
        }

        private async Task ImportFileTreeEntriesAsync(
            IReadOnlyList<FileTreeSelection> selections)
        {
            var groups = selections
                .Where(SearchableFileTree.CanImport)
                .GroupBy(selection => selection.File.Id, StringComparer.Ordinal)
                .ToArray();
            foreach (var group in groups)
            {
                var paths = group
                    .Select(selection => selection.Entry?.Path ?? string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (paths.Contains(string.Empty))
                {
                    paths = new[] { string.Empty };
                }
                try
                {
                    var result = await _manager.ImportFileEntries(
                        group.Key,
                        paths);
                    if (!result.Succeeded)
                    {
                        Debug.LogError(
                            "Asset import failed: " + result.State + " · " +
                            result.ErrorMessage);
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return;
                }
            }
        }

        private void ShowItemTargetEditor(
            VisualElement anchor,
            string itemId,
            IReadOnlyList<AssetFile> files)
        {
            TargetEditorPopup.Show(
                anchor,
                _manager,
                itemId,
                files,
                _manager.GetItemTargets(itemId),
                I18N.Get("detail.itemTargetPickerTitle"),
                I18N.Get("action.saveTargets"),
                targets => Run(() =>
                    _manager.SetItemTargets(itemId, targets)),
                _fileTree?.GetCachedAnalyses());
        }

        private IReadOnlyList<AssetFileTarget> GetCommonFileDependencies(
            IReadOnlyList<AssetFileTarget> sources)
        {
            var common = _manager.GetFileDependencies(sources[0].FileId)
                .Where(dependency => string.Equals(
                    dependency.DependentTargetPath,
                    sources[0].TargetPath,
                    StringComparison.OrdinalIgnoreCase))
                .Select(dependency => new AssetFileTarget
                {
                    FileId = dependency.DependencyFileId,
                    TargetPath = dependency.TargetPath
                })
                .ToArray();
            for (var index = 1; index < sources.Count; index++)
            {
                var source = sources[index];
                var dependencies = _manager.GetFileDependencies(source.FileId)
                    .Where(dependency => string.Equals(
                        dependency.DependentTargetPath,
                        source.TargetPath,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                common = common.Where(target => dependencies.Any(dependency =>
                    string.Equals(
                        dependency.DependencyFileId,
                        target.FileId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        dependency.TargetPath,
                        target.TargetPath,
                        StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
            }
            return common;
        }

        private void ShowDependencyEditor(
            VisualElement anchor,
            IReadOnlyList<AssetFileTarget> sources,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            var sourceFile = _manager.GetFile(sources[0].FileId);
            var sourceItemId = sourceFile?.ItemId ?? string.Empty;
            var excludedFileIds = new HashSet<string>(
                sources.Where(source => string.IsNullOrEmpty(
                        source.TargetPath))
                    .Select(source => source.FileId),
                StringComparer.Ordinal);
            var groupedFiles = (_manager.SearchItems(new AssetItemQuery
                {
                    IncludeArchived = true
                }).Items ?? Array.Empty<AssetItem>())
                .Where(item => item != null)
                .Select(item => new
                {
                    Item = item,
                    Files = (item.Files ?? Array.Empty<AssetFile>())
                        .Where(file => file != null &&
                                       !excludedFileIds.Contains(file.Id))
                        .OrderBy(
                            file => file.FileName,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(file => file.Id, StringComparer.Ordinal)
                        .ToArray()
                })
                .Where(group => group.Files.Length > 0)
                .OrderBy(group => !string.Equals(
                    group.Item.Id,
                    sourceItemId,
                    StringComparison.Ordinal))
                .ThenBy(
                    group => group.Item.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Item.Id, StringComparer.Ordinal)
                .ToArray();
            var hasSourceGroup = groupedFiles.Any(group => string.Equals(
                group.Item.Id,
                sourceItemId,
                StringComparison.Ordinal));
            var startedOtherItems = false;
            var groups = new List<FileTreeGroup>(groupedFiles.Length);
            foreach (var group in groupedFiles)
            {
                var isSourceGroup = string.Equals(
                    group.Item.Id,
                    sourceItemId,
                    StringComparison.Ordinal);
                var startsNewSection = hasSourceGroup &&
                                       !isSourceGroup &&
                                       !startedOtherItems;
                startedOtherItems |= !isSourceGroup;
                groups.Add(new FileTreeGroup(
                    group.Item.Id,
                    string.IsNullOrWhiteSpace(group.Item.Name)
                        ? group.Item.Id
                        : group.Item.Name,
                    group.Files,
                    startsNewSection));
            }
            var files = groups
                .SelectMany(group => group.Files)
                .ToArray();
            TargetEditorPopup.Show(
                anchor,
                _manager,
                null,
                files,
                dependencyTargets,
                I18N.Get("detail.dependencyTargetPickerTitle"),
                I18N.Get("action.saveDependencies"),
                targets => SaveDependencies(sources, targets),
                _fileTree?.GetCachedAnalyses(),
                groups,
                sources);
        }

        private void ShowItemTargetImport(
            VisualElement anchor,
            string itemId,
            IReadOnlyList<AssetFile> files)
        {
            var filesById = (files ?? Array.Empty<AssetFile>())
                .ToDictionary(file => file.Id, StringComparer.Ordinal);
            var groups = _manager.GetItemTargets(itemId)
                .Where(target =>
                    filesById.ContainsKey(target.FileId) &&
                    !string.IsNullOrWhiteSpace(target.GroupName))
                .Select(target => new ItemTargetEntry
                {
                    File = filesById[target.FileId],
                    Target = target
                })
                .GroupBy(
                    entry => entry.Target.GroupName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new TargetImportGroup
                {
                    Name = group.Key,
                    Choices = group.Select(entry => new TargetImportChoice
                    {
                        Label = FormatTargetName(entry.File, entry.Target),
                        Target = entry.Target
                    }).ToArray()
                })
                .ToArray();
            if (groups.Length == 0)
            {
                ImportItemTargets(
                    itemId,
                    Array.Empty<AssetFileTarget>());
                return;
            }

            TargetImportPopup.Show(
                anchor,
                groups,
                selections => ImportItemTargets(itemId, selections));
        }

        private bool HasItemTargets(string itemId)
        {
            return _manager.GetItemTargets(itemId).Count > 0;
        }

        private void ImportItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> selections)
        {
            _ = RunImport(() => _manager.ImportItemTargets(
                itemId,
                selections));
        }

        private bool UpdateCollection(
            string id,
            string name,
            AssetCollectionIcon icon,
            AssetFilterNode root)
        {
            return Run(() => _manager.UpdateCollection(
                id,
                new UpdateAssetCollectionRequest
                {
                    Name = name,
                    Icon = icon,
                    Root = root
                }));
        }

        private bool CreateCollection(
            string name,
            AssetCollectionIcon icon,
            AssetFilterNode root)
        {
            return Run(() =>
            {
                var created = _manager.CreateCollection(
                    new CreateAssetCollectionRequest
                    {
                        Name = name,
                        Icon = icon,
                        Root = root
                    });
                _viewState.SelectCollection(created.Id);
            });
        }

        private void DeleteCollection(string id)
        {
            if (string.IsNullOrEmpty(id) ||
                !Confirm(
                    I18N.Get("confirm.deleteCollection.title"),
                    I18N.Get("confirm.deleteCollection.message")))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteCollection(id);
                _viewState.SelectPage(AssetManagerPage.Library);
            });
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

        private IReadOnlyList<AssetItem> GetVisibleItems()
        {
            IReadOnlyList<AssetItem> items;
            if (_viewState.Page == AssetManagerPage.Collection)
            {
                items = _manager.SearchCollection(
                    _viewState.CollectionId).Items;
            }
            else
            {
                var query = new AssetItemQuery
                {
                    IncludeArchived =
                        _viewState.Page == AssetManagerPage.Archived,
                    Filter = AssetManagerSearch.BuildBackendFilter(
                        _search.Value,
                        _viewState.SearchTargets)
                };
                items = _manager.SearchItems(query).Items;
            }

            var importedItemIds =
                _viewState.Page == AssetManagerPage.Imported
                    ? GetImportedItemIdsInProject()
                    : null;
            var visibleItems = items
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Archived ||
                    item.IsArchived)
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Imported ||
                    importedItemIds.Contains(item.Id))
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Tags ||
                    MatchesTag(item, _viewState.TagPath))
                .Where(item => AssetManagerSearch.MatchesItem(
                    item,
                    _search.Value,
                    _viewState.SearchTargets));
            return AssetManagerItemSort.Apply(
                visibleItems,
                _viewState.ItemSortField,
                _viewState.IsItemSortReversed);
        }

        private ISet<string> GetImportedItemIdsInProject()
        {
            if (_importedItemIdsInProject != null)
            {
                return _importedItemIdsInProject;
            }

            var existingGuids = new HashSet<string>(StringComparer.Ordinal);
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var associations = _manager.GetImportedAssetAssociations();
            for (var i = 0; i < associations.Count; i++)
            {
                var association = associations[i];
                if (association == null ||
                    string.IsNullOrEmpty(association.ItemId) ||
                    string.IsNullOrEmpty(association.AssetGuid))
                {
                    continue;
                }

                if (existingGuids.Contains(association.AssetGuid) ||
                    IsImportedAssetInProject(association.AssetGuid))
                {
                    existingGuids.Add(association.AssetGuid);
                    itemIds.Add(association.ItemId);
                }
            }

            _importedItemIdsInProject = itemIds;
            return _importedItemIdsInProject;
        }

        private static bool IsImportedAssetInProject(string assetGuid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            return !string.IsNullOrEmpty(assetPath) &&
                   !AssetDatabase.IsValidFolder(assetPath) &&
                   AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null;
        }

        private static bool MatchesTag(AssetItem item, string tagPath)
        {
            if (item == null || string.IsNullOrWhiteSpace(tagPath))
            {
                return false;
            }

            return (item.Tags ?? Array.Empty<AssetTag>()).Any(tag =>
                tag != null &&
                !string.IsNullOrEmpty(tag.Path) &&
                (string.Equals(
                    tag.Path,
                    tagPath,
                    StringComparison.Ordinal) ||
                 tag.Path.StartsWith(
                    tagPath + "/",
                    StringComparison.Ordinal)));
        }

        private IReadOnlyList<AssetFile> GetFiles(string itemId)
        {
            return _manager.GetFiles(itemId, includeArchived: true);
        }

        private IReadOnlyList<AssetFile> GetUnassignedFiles()
        {
            return _manager.GetUnassignedFiles(includeArchived: true);
        }

        private string GetPageTitle()
        {
            switch (_viewState.Page)
            {
                case AssetManagerPage.Variants:
                    return I18N.Get("navigation.variants");
                case AssetManagerPage.Imported:
                    return I18N.Get("navigation.imported");
                case AssetManagerPage.Archived:
                    return I18N.Get("navigation.archived");
                case AssetManagerPage.Tags:
                    return string.IsNullOrEmpty(_viewState.TagPath)
                        ? I18N.Get("navigation.tags")
                        : _viewState.TagPath;
                case AssetManagerPage.Collection:
                    return _manager.GetCollections()
                        .FirstOrDefault(collection =>
                            collection.Id == _viewState.CollectionId)
                        ?.Name ?? I18N.Get("common.collection");
                case AssetManagerPage.UnassignedFiles:
                    return I18N.Get("navigation.unassignedFiles");
                default:
                    return I18N.Get("navigation.library");
            }
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
            if (_variantMetadataSaveCount > 0) { return; }
            if (ShowsVariants || !string.IsNullOrEmpty(_viewState.DetailVariantId))
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
