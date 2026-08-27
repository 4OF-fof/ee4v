using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        private const float DerivedAssetCardWidth = 144f;

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
                IReadOnlyList<FileTreeGroup> groups = null)
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
                    showTargetToggles: true);
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

        private readonly IAssetManager _manager;
        private readonly AssetManagerViewState _viewState;
        private readonly AssetManagerViewMode _mode;
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
        private UiButton _sortButton;
        private UiButton _backButton;
        private UiButton _forwardButton;
        private AssetManagerBreadcrumb _breadcrumbs;

        private AssetThumbnailStack _detailThumbnailStack;
        private AssetThumbnailStack _itemOverviewThumbnailStack;
        private SearchableFileTree _fileTree;
        private ScrollView _itemDetailPane;
        private FileTreeSelection _fileTreeSelection;
        private CancellationTokenSource _thumbnailCancellation;
        private CancellationTokenSource _itemOverviewThumbnailCancellation;
        private CancellationTokenSource _gridThumbnailCancellation;
        private CancellationTokenSource _fileAnalysisCancellation;
        private bool _defersManagerRefresh;
        private bool _managerRefreshPending;

        public AssetManagerView(
            IAssetManager manager,
            AssetManagerViewState viewState = null,
            AssetManagerViewMode mode = AssetManagerViewMode.Main,
            CachedImageCache imageCache = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _viewState = viewState ?? new AssetManagerViewState();
            _mode = mode;
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
            _viewState.Changed += OnViewStateChanged;

            if (ShowsNavigation)
            {
                RebuildNavigation();
            }
            Refresh();
        }

        public void Dispose()
        {
            _manager.Changed -= OnManagerChanged;
            _viewState.Changed -= OnViewStateChanged;
            if (_itemGrid != null)
            {
                _itemGrid.SelectionChanged -= SelectItems;
                _itemGrid.ItemDoubleClicked -= OpenItemDetail;
                _itemGrid.ContextMenuRequested -= ShowItemContextMenu;
                _itemGrid.RecommendedMinimumItemsPerRowChanged -=
                    SetMinimumGridSize;
            }
            if (_search != null)
            {
                _search.SearchActionRequested -= ShowSearchTargetsMenu;
            }

            CancelGridThumbnails();
            CancelThumbnail();
            CancelItemOverviewThumbnail();
            CancelFileAnalysis();
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
            toolbar.Actions.Add(AssetManagerControls.CreateReloadButton(
                Reload,
                "ee4v-asset-manager__reload"));
            return toolbar;
        }

        private void ShowSortMenu()
        {
            var menu = new GenericMenu();
            AddSortMenuItem(
                menu,
                GetSortLabel(AssetManagerItemSortField.Name),
                AssetManagerItemSortField.Name);
            AddSortMenuItem(
                menu,
                GetSortLabel(AssetManagerItemSortField.CreatedAt),
                AssetManagerItemSortField.CreatedAt);
            AddSortMenuItem(
                menu,
                GetSortLabel(AssetManagerItemSortField.UpdatedAt),
                AssetManagerItemSortField.UpdatedAt);
            if (!ShowsFileList)
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
            AddSearchTargetMenuItem(
                menu,
                "toolbar.search.tags",
                AssetManagerSearchTarget.Tags);
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
                I18N.Get("navigation.library"),
                AssetManagerPage.Library,
                "library.png"));
            primary.Add(CreateNavigationButton(
                I18N.Get("navigation.unassignedFiles"),
                AssetManagerPage.UnassignedFiles,
                "folder.png"));
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
            var section = new SectionHeader(string.Format(
                I18N.Get("navigation.collectionsWithCount"),
                collections.Count));
            section.AddToClassList(
                "ee4v-asset-manager__nav-section-header");
            section.TitleText.AddToClassList(
                "ee4v-asset-manager__nav-section");
            UiButton createCollectionButton = null;
            createCollectionButton = AssetManagerControls.CreateIconButton(
                I18N.Get("navigation.newCollection"),
                "add.png",
                () => ShowNewCollection(createCollectionButton),
                "ee4v-asset-manager__nav-section-action");
            section.Actions.Add(createCollectionButton);
            _navigation.Add(section);

            for (var i = 0; i < collections.Count; i++)
            {
                var collection = collections[i];
                var button = new NavigationItem(
                    new NavigationItemState(
                        collection.Name,
                        icon: AssetManagerControls.LoadFluentIconState(
                            "folder.png",
                            UiSizeTokens.Size12)),
                    () => SelectCollection(collection.Id));
                AssetManagerControls.SetNavigationSelected(
                    button,
                    _viewState.Page == AssetManagerPage.Collection &&
                    string.Equals(
                        _viewState.CollectionId,
                        collection.Id,
                        StringComparison.Ordinal));
                button.AddToClassList("ee4v-asset-manager__nav-button");
                button.RegisterCallback<ContextClickEvent>(evt =>
                {
                    ShowCollectionContextMenu(button, collection);
                    evt.StopPropagation();
                });
                var count = new Badge(
                    _manager.SearchCollection(collection.Id, limit: 1)
                        .TotalCount.ToString());
                count.AddToClassList("ee4v-asset-manager__nav-count");
                button.Trailing.Add(count);
                _navigation.Add(button);
            }

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
            _toolbar.style.display = _viewState.IsDerivedAssetsPage
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            RefreshHistoryNavigation();
            RefreshSortButton();
            var showsItemDetail =
                !string.IsNullOrEmpty(_viewState.DetailItemId);
            _content.EnableInClassList(
                "ee4v-asset-manager__content--item-detail",
                showsItemDetail);
            if (showsItemDetail)
            {
                BuildItemDetail();
                return;
            }

            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            CancelFileAnalysis();
            switch (_viewState.Page)
            {
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
            _itemGrid.ClearThumbnails();
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
            LoadGridThumbnails(items);
        }

        private void BuildTags()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.None;
            _sortButton.style.display = DisplayStyle.None;
            _gridControls.style.display = DisplayStyle.None;

            var items = _manager.SearchItems(new AssetItemQuery()).Items;
            var tags = (_manager.GetTags() ?? Array.Empty<AssetTag>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag.Path))
                .GroupBy(tag => tag.Path, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (tags.Length == 0)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.noTags")));
                return;
            }

            var list = new ScrollView();
            list.AddToClassList("ee4v-asset-manager__tag-list");
            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                var button = new NavigationItem(
                    new NavigationItemState(
                        tag.Path,
                        icon: AssetManagerControls.LoadFluentIconState(
                            "tag.png",
                            UiSizeTokens.Size12)),
                    () => _viewState.SelectTag(tag.Path));
                button.AddToClassList("ee4v-asset-manager__tag-row");
                var count = new Badge(
                    items.Count(item => MatchesTag(item, tag.Path))
                        .ToString());
                count.AddToClassList("ee4v-asset-manager__nav-count");
                button.Trailing.Add(count);
                list.Add(button);
            }
            _content.Add(list);
        }

        private void SelectItems(
            IReadOnlyList<string> itemIds,
            string primaryItemId)
        {
            _viewState.SelectItems(itemIds, primaryItemId);
        }

        private void OpenItemDetail(string itemId)
        {
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

        private async void LoadGridThumbnails(IReadOnlyList<AssetItem> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _gridThumbnailCancellation = cancellation;
            try
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (_itemGrid.HasThumbnailResult(items[i].Id))
                    {
                        continue;
                    }

                    var thumbnail = await _manager.GetThumbnail(
                        items[i].Id,
                        cancellation.Token);
                    if (!ReferenceEquals(
                            _gridThumbnailCancellation,
                            cancellation))
                    {
                        return;
                    }

                    _itemGrid.SetThumbnail(
                        items[i].Id,
                        thumbnail != null && thumbnail.Found
                            ? thumbnail.Data
                            : null);
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
                    cancellation.Dispose();
                    _gridThumbnailCancellation = null;
                }
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
                I18N.Get("field.name"));
            name.value = item.Name;
            var descriptionContainer = new VisualElement();
            descriptionContainer.AddToClassList(
                "ee4v-asset-manager-control-field");
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
            tagsContainer.Add(UiTextFactory.Create(
                I18N.Get("field.tags"),
                UiClassNames.FormLabel,
                "ee4v-asset-manager-control-field__label"));
            var tags = new AssetTagField();
            tags.SetValues(
                GetAvailableTagOptions(),
                GetTagPaths(item));
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
            tags.SetEnabled(!item.IsArchived);
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
                            : GetTagPaths(current));
                };
            }
            _detail.Add(name);
            _detail.Add(descriptionContainer);
            _detail.Add(tagsContainer);
            AddItemInformation(
                _detail,
                item,
                GetFiles(item.Id));
            LoadDetailThumbnails(new[] { item }, thumbnailStack);
        }

        private void RefreshDetail()
        {
            CancelThumbnail();
            ClearDetailThumbnail();
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
                UiClassNames.InfomationPanelSelectionCount,
                "ee4v-asset-manager__selection-count"));
            LoadDetailThumbnails(orderedItems, thumbnailStack);
        }

        private AssetThumbnailStack CreateDetailThumbnailStack(
            IReadOnlyList<string> itemIds)
        {
            var stack = new AssetThumbnailStack(_imageCache, itemIds);
            _detailThumbnailStack = stack;
            return stack;
        }

        private async void LoadDetailThumbnails(
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
                    cancellation.Dispose();
                    _thumbnailCancellation = null;
                }
            }
        }

        private async void LoadItemOverviewThumbnail(
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
                    cancellation.Dispose();
                    _itemOverviewThumbnailCancellation = null;
                }
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

            var entries = AssetManagerControls.CreateTextField(
                string.Empty,
                "ee4v-asset-manager__setting-input",
                "ee4v-asset-manager__contents-input");
            entries.multiline = true;
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
            header.AddAction(AssetManagerControls.CreateButton(
                I18N.Get("action.analyze"),
                () => AnalyzeFile(file.Id, entries)));
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

            var settings = new AssetDetailSection(
                I18N.Get("detail.settings"));
            var settingList = new AssetDetailSettingList();
            var itemId = AssetManagerControls.CreateTextField(
                string.Empty,
                "ee4v-asset-manager__setting-input");
            itemId.value = file.ItemId ?? string.Empty;
            settingList.Add(AssetDetailSettingRow.Editable(
                I18N.Get("field.assignedItem"),
                string.IsNullOrWhiteSpace(file.ItemId)
                    ? I18N.Get("common.none")
                    : file.ItemId,
                itemId,
                AssetManagerControls.CreateButton(
                    I18N.Get("action.moveFile"),
                    () => MoveFile(file.Id, itemId.value)),
                I18N.Get("action.move")));

            var dependencyTargets = _manager
                .GetFileDependencies(file.Id)
                .Select(dependency => new AssetFileTarget
                {
                    FileId = dependency.DependencyFileId,
                    TargetPath = dependency.TargetPath
                })
                .ToArray();
            var dependencySummary = dependencyTargets
                .Select(target => FormatTargetName(
                    _manager.GetFile(target.FileId),
                    target))
                .ToArray();
            UiButton editDependencies = null;
            editDependencies = AssetManagerControls.CreateButton(
                I18N.Get("action.edit"),
                () => ShowDependencyEditor(
                    editDependencies,
                    file.Id,
                    dependencyTargets));
            var dependencySummaryText = UiTextFactory.Create(
                dependencySummary.Length == 0
                    ? I18N.Get("common.none")
                    : string.Join(" · ", dependencySummary));
            dependencySummaryText.SetWhiteSpace(WhiteSpace.Normal);
            settingList.Add(new AssetDetailSettingRow(
                I18N.Get("field.dependencies"),
                dependencySummaryText,
                editDependencies));
            settingList.Add(new AssetDetailSettingRow(
                I18N.Get("field.fileId"),
                CreateMonoValue(file.Id)));
            settings.Add(settingList);
            host.Add(settings);

            var contents = new AssetDetailSection(
                I18N.Get("detail.contents"));
            contents.Add(UiTextFactory.Create(
                I18N.Get("detail.contentsNote"),
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__section-note"));
            contents.Add(entries);
            contents.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.importEntries"),
                () => ImportEntries(file.Id, entries.value),
                "ee4v-asset-manager__primary-action"));
            host.Add(contents);
        }

        private void SelectFile(AssetFile file)
        {
            _viewState.SelectFile(file?.Id);
        }

        private void BuildItemDetail()
        {
            CancelGridThumbnails();
            CancelFileAnalysis();
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
                    ImportFileTreeEntry);
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

            _fileTreeSelection = null;
            _fileTree.SetItem(item.Id, files);
            RefreshItemDetailPane(item, files);
        }

        private void BuildDerivedAssetsPage()
        {
            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            _itemDetailPane = null;
            _fileTreeSelection = null;
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
            fields.Add(name);

            var description = AssetManagerControls.CreateTextField(
                I18N.Get("field.description"),
                "ee4v-asset-manager__derived-assets-description-field");
            description.SetMultiline(true, 144f);
            fields.Add(description);

            var message = new InlineMessage();
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
                message.SetState(new InlineMessageState(string.Empty));
                create.SetEnabled(selectedPrefab != null);
            };
            prefab.SelectionRejected += () =>
            {
                create.SetEnabled(prefab.Value != null);
                message.SetState(new InlineMessageState(
                    I18N.Get("notice.derivedAssetPrefabInvalid"),
                    UiStatusTone.Failed));
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
            InlineMessage message,
            UiButton createButton)
        {
            if (!DerivedAssetCreator.IsValidName(name))
            {
                message.SetState(new InlineMessageState(
                    I18N.Get("notice.derivedAssetNameInvalid"),
                    UiStatusTone.Failed));
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
                message.SetState(new InlineMessageState(
                    I18N.Get("notice.derivedAssetPrefabInvalid"),
                    UiStatusTone.Failed));
                return;
            }

            if (AssetDatabase.IsValidFolder(
                    DerivedAssetCreator.GetVariantFolder(name)))
            {
                message.SetState(new InlineMessageState(
                    I18N.Get("notice.derivedAssetAlreadyExists"),
                    UiStatusTone.Failed));
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
                message.SetState(new InlineMessageState(
                    I18N.Get("notice.derivedAssetCreateFailed"),
                    UiStatusTone.Failed));
            }
            finally
            {
                createButton.SetEnabled(true);
            }
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
            if (_fileTreeSelection?.Entry != null)
            {
                BuildFileEntryDetail(
                    _itemDetailPane,
                    _fileTreeSelection.File,
                    _fileTreeSelection.Entry);
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
            LoadItemOverviewThumbnail(item, thumbnail);

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
                     DerivedAssetCreator.FindByParentItem(itemId))
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
                        derivedAsset.AssetPath,
                        derivedAsset.Name),
                    selected: false);
                assetCard.tooltip = derivedAsset.Description;
                assetCard.Clicked += (_, __, ___) =>
                {
                    Selection.activeObject = derivedAsset.Prefab;
                    EditorGUIUtility.PingObject(derivedAsset.Prefab);
                };
                assetCard.RegisterCallback<DetachFromPanelEvent>(_ =>
                    assetCard.Dispose());
                grid.Add(assetCard);
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
                _viewState.OpenDerivedAssetsPage(itemId);
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
            information.Add(settingList);
            if (entry.Kind != AssetFileContentEntryKind.Directory &&
                file != null)
            {
                information.Add(AssetManagerControls.CreateButton(
                    I18N.Get("action.importEntry"),
                    () => ImportEntries(file.Id, entry.Path),
                    "ee4v-asset-manager__primary-action"));
            }
            detail.Add(information);
        }

        private void OnFileTreeSelectionChanged(FileTreeSelection selection)
        {
            _fileTreeSelection = selection;
            _viewState.SelectFile(selection?.File?.Id);
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
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get("action.delete")),
                false,
                () => DeleteCollection(collection?.Id));
            menu.ShowAsContext();
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
                (name, root) => UpdateCollection(
                    collection.Id,
                    name,
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
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedTags.SequenceEqual(
                    GetTagPaths(item),
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

        private void MoveFile(string fileId, string itemId)
        {
            Run(() =>
                _manager.SetFileItem(
                    new[] { fileId },
                    string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim()));
        }

        private void ArchiveFile(string id, bool archived)
        {
            Run(() =>
                _manager.SetFileArchived(new[] { id }, archived));
        }

        private void DeleteFile(string id)
        {
            if (!Confirm(
                    I18N.Get("confirm.deleteFile.title"),
                    I18N.Get("confirm.deleteFile.message")))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteFile(new[] { id });
                _viewState.SelectFile(null);
            });
        }

        private void SaveDependencies(
            string fileId,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            Run(() =>
                _manager.SetFileDependencies(
                    new[] { fileId },
                    dependencyTargets));
        }

        private async void AnalyzeFile(
            string fileId,
            AssetManagerTextField output)
        {
            CancelFileAnalysis();
            var cancellation = new CancellationTokenSource();
            _fileAnalysisCancellation = cancellation;
            try
            {
                var analysis = await _manager.AnalyzeFileAsync(
                    fileId,
                    cancellation.Token);
                if (!ReferenceEquals(
                        _fileAnalysisCancellation,
                        cancellation) ||
                    output.panel == null)
                {
                    return;
                }

                output.value = string.Join(
                    "\n",
                    (analysis?.Entries ??
                     Array.Empty<AssetFileContentEntry>())
                    .Where(entry => entry != null)
                    .Select(entry => entry.Path));
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
                        _fileAnalysisCancellation,
                        cancellation))
                {
                    cancellation.Dispose();
                    _fileAnalysisCancellation = null;
                }
            }
        }

        private async void ImportEntries(string fileId, string paths)
        {
            await RunImport(
                () => _manager.ImportFileEntries(fileId, SplitLines(paths)));
        }

        private async void ImportFileTreeEntry(FileTreeSelection selection)
        {
            if (selection?.File == null)
            {
                return;
            }

            await RunImport(() => _manager.ImportFileEntries(
                selection.File.Id,
                new[] { selection.Entry?.Path ?? string.Empty }));
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

        private void ShowDependencyEditor(
            VisualElement anchor,
            string fileId,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            var sourceFile = _manager.GetFile(fileId);
            var sourceItemId = sourceFile?.ItemId ?? string.Empty;
            var groupedFiles = (_manager.SearchItems(new AssetItemQuery
                {
                    IncludeArchived = true
                }).Items ?? Array.Empty<AssetItem>())
                .Where(item => item != null)
                .Select(item => new
                {
                    Item = item,
                    Files = (item.Files ?? Array.Empty<AssetFile>())
                        .Where(file =>
                            file != null &&
                            !string.Equals(
                                file.Id,
                                fileId,
                                StringComparison.Ordinal))
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
                targets => SaveDependencies(fileId, targets),
                _fileTree?.GetCachedAnalyses(),
                groups);
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

        private async void ImportItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> selections)
        {
            await RunImport(() => _manager.ImportItemTargets(
                itemId,
                selections));
        }

        private bool UpdateCollection(
            string id,
            string name,
            AssetFilterNode root)
        {
            return Run(() => _manager.UpdateCollection(
                id,
                new UpdateAssetCollectionRequest
                {
                    Name = name,
                    Root = root
                }));
        }

        private bool CreateCollection(
            string name,
            AssetFilterNode root)
        {
            return Run(() =>
            {
                var created = _manager.CreateCollection(
                    new CreateAssetCollectionRequest
                    {
                        Name = name,
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

        private async System.Threading.Tasks.Task RunImport(
            Func<System.Threading.Tasks.Task<AssetImportResult>> operation)
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

            var visibleItems = items
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Archived ||
                    item.IsArchived)
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
                if (ShowsMain &&
                    !string.IsNullOrEmpty(_viewState.DetailItemId))
                {
                    RefreshItemDetailPane();
                }
                if (ShowsInformation)
                {
                    RefreshDetail();
                }
                return;
            }

            RefreshAfterManagerChange();
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
            _thumbnailCancellation?.Cancel();
            _thumbnailCancellation?.Dispose();
            _thumbnailCancellation = null;
        }

        private void CancelItemOverviewThumbnail()
        {
            _itemOverviewThumbnailCancellation?.Cancel();
            _itemOverviewThumbnailCancellation?.Dispose();
            _itemOverviewThumbnailCancellation = null;
        }

        private void CancelGridThumbnails()
        {
            _gridThumbnailCancellation?.Cancel();
            _gridThumbnailCancellation?.Dispose();
            _gridThumbnailCancellation = null;
        }

        private void CancelFileAnalysis()
        {
            _fileAnalysisCancellation?.Cancel();
            _fileAnalysisCancellation?.Dispose();
            _fileAnalysisCancellation = null;
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
