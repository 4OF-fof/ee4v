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
        private readonly IAssetManager _manager;
        private readonly AssetManagerViewState _viewState;
        private readonly AssetManagerViewMode _mode;
        private readonly CachedImageCache _imageCache;
        private readonly bool _ownsImageCache;
        private readonly VisualElement _navigation;
        private readonly VisualElement _content;
        private readonly VisualElement _detail;
        private readonly AssetItemGridView _itemGrid;
        private AssetManagerGridSizeSlider _gridSizeSlider;
        private VisualElement _gridControls;
        private SearchField _search;
        private UiButton _sortButton;
        private UiButton _backButton;
        private UiButton _forwardButton;
        private AssetManagerBreadcrumb _breadcrumbs;

        private AssetThumbnailStack _detailThumbnailStack;
        private SearchableFileTree _fileTree;
        private ScrollView _itemDetailPane;
        private FileTreeSelection _fileTreeSelection;
        private CancellationTokenSource _thumbnailCancellation;
        private CancellationTokenSource _gridThumbnailCancellation;
        private CancellationTokenSource _fileAnalysisCancellation;
        private bool _defersManagerRefresh;
        private bool _managerRefreshPending;

        public AssetManagerView(
            IAssetManager manager,
            AssetManagerViewState viewState = null,
            AssetManagerViewMode mode = AssetManagerViewMode.Combined,
            CachedImageCache imageCache = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _viewState = viewState ?? new AssetManagerViewState();
            _mode = mode;
            _ownsImageCache = imageCache == null;
            _imageCache = imageCache ?? new CachedImageCache();
            _itemGrid = new AssetItemGridView(_imageCache);
            _itemGrid.SelectionChanged += SelectItems;
            _itemGrid.ItemDoubleClicked += OpenItemDetail;
            _itemGrid.ContextMenuRequested += ShowItemContextMenu;
            _itemGrid.RecommendedMinimumItemsPerRowChanged +=
                SetMinimumGridSize;

            AddToClassList("ee4v-asset-manager");

            _navigation = new ScrollView();
            _navigation.AddToClassList("ee4v-asset-manager__navigation");
            _navigation.contentContainer.AddToClassList(
                "ee4v-asset-manager__navigation-content");
            _content = new VisualElement();
            _content.AddToClassList("ee4v-asset-manager__content");
            _detail = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _detail.AddToClassList("ee4v-asset-manager__detail");
            var layout = new AssetManagerThreePaneLayout(mode);
            layout.MainToolbarContent.Add(BuildToolbar());
            layout.LeftContent.Add(_navigation);
            layout.MainContent.Add(_content);
            layout.RightContent.Add(_detail);
            Add(layout);

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
            _itemGrid.SelectionChanged -= SelectItems;
            _itemGrid.ItemDoubleClicked -= OpenItemDetail;
            _itemGrid.ContextMenuRequested -= ShowItemContextMenu;
            _itemGrid.RecommendedMinimumItemsPerRowChanged -=
                SetMinimumGridSize;
            _search.SearchActionRequested -= ShowSearchTargetsMenu;

            CancelGridThumbnails();
            CancelThumbnail();
            CancelFileAnalysis();
            ClearDetailThumbnail();
            _breadcrumbs?.Dispose();
            _itemGrid.Dispose();
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
            _mode == AssetManagerViewMode.Combined ||
            _mode == AssetManagerViewMode.Navigation;

        private bool ShowsMain =>
            _mode == AssetManagerViewMode.Combined ||
            _mode == AssetManagerViewMode.Main;

        private bool ShowsInformation =>
            _mode == AssetManagerViewMode.Combined ||
            _mode == AssetManagerViewMode.Information;

        private bool ShowsFileList =>
            _viewState.Page == AssetManagerPage.UnassignedFiles;

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
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
            toolbar.Add(history);

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
            toolbar.Add(_gridControls);

            var actions = new VisualElement();
            actions.AddToClassList("ee4v-asset-manager__toolbar-actions");
            _sortButton = AssetManagerControls.CreateSortButton(
                ShowSortMenu,
                "ee4v-asset-manager__sort");
            RefreshSortButton();
            actions.Add(_sortButton);
            actions.Add(_search);
            actions.Add(AssetManagerControls.CreateReloadButton(
                Reload,
                "ee4v-asset-manager__reload"));
            toolbar.Add(actions);
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

            _breadcrumbs.SetItems(new[]
            {
                new AssetManagerBreadcrumbItem(
                    pageTitle,
                    _viewState.ShowPageRoot),
                new AssetManagerBreadcrumbItem(
                    _manager.GetItem(_viewState.DetailItemId)?.Name ??
                    I18N.Get("common.item"))
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
            var section = new VisualElement();
            section.AddToClassList(
                "ee4v-asset-manager__nav-section-header");
            section.Add(UiTextFactory.Create(
                string.Format(
                    I18N.Get("navigation.collectionsWithCount"),
                    collections.Count),
                "ee4v-asset-manager__nav-section"));
            UiButton createCollectionButton = null;
            createCollectionButton = AssetManagerControls.CreateIconButton(
                I18N.Get("navigation.newCollection"),
                "add.png",
                () => ShowNewCollection(createCollectionButton),
                "ee4v-asset-manager__nav-section-action");
            section.Add(createCollectionButton);
            _navigation.Add(section);

            for (var i = 0; i < collections.Count; i++)
            {
                var collection = collections[i];
                var button = AssetManagerControls.CreateIconTextButton(
                    collection.Name,
                    "folder.png",
                    () => SelectCollection(collection.Id));
                AssetManagerControls.SetNavigationSelected(
                    button,
                    _viewState.Page == AssetManagerPage.Collection &&
                    string.Equals(
                        _viewState.CollectionId,
                        collection.Id,
                        StringComparison.Ordinal));
                button.AddToClassList("ee4v-asset-manager__nav-button");
                button.Add(UiTextFactory.Create(
                    _manager.SearchCollection(collection.Id, limit: 1)
                        .TotalCount.ToString(),
                    "ee4v-asset-manager__nav-count"));
                _navigation.Add(button);
            }

        }

        private UiButton CreateNavigationButton(
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
            RefreshHistoryNavigation();
            RefreshSortButton();
            if (!string.IsNullOrEmpty(_viewState.DetailItemId))
            {
                BuildItemDetail();
                return;
            }

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
                Run(() => _manager.SyncEagle(new EagleSyncRequest(
                    AssetManagerSettings.EagleLibraryPath,
                    AssetManagerSettings.EagleTargetRoot)),
                    refresh: false);
                Run(() => _manager.SyncEe4v(new Ee4vSyncRequest(
                    AssetManagerSettings.Ee4vLibraryPath)),
                    refresh: false);
            }
            finally
            {
                _defersManagerRefresh = false;
                _managerRefreshPending = false;
                RefreshAfterManagerChange();
            }
        }

        private void BuildItems()
        {
            var items = GetVisibleItems();
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.Flex;

            if (_viewState.Page == AssetManagerPage.Collection)
            {
                _content.Add(BuildCollectionHeader());
            }

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
                var button = AssetManagerControls.CreateIconTextButton(
                    tag.Path,
                    "tag.png",
                    () => _viewState.SelectTag(tag.Path),
                    "ee4v-asset-manager__tag-row");
                button.Add(UiTextFactory.Create(
                    items.Count(item => MatchesTag(item, tag.Path))
                        .ToString(),
                    "ee4v-asset-manager__nav-count"));
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
            _viewState.OpenItemDetail(itemId);
        }

        private void ShowItemContextMenu(
            IReadOnlyList<string> itemIds)
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
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                UiTextFactory.CreateGuiContent(I18N.Get(
                    ids.Length == 1
                        ? "action.delete"
                        : "action.deleteItems",
                    ids.Length)),
                false,
                () => DeleteItems(ids));
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
            var canEditMetadata = !item.SourceType.HasValue ||
                                  item.SourceType == AssetSourceType.Ee4v;
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
            _detail.Add(name);
            _detail.Add(descriptionContainer);
            _detail.Add(tagsContainer);
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

                        _itemGrid.SetThumbnail(
                            itemId,
                            thumbnail != null && thumbnail.Found
                                ? thumbnail.Data
                                : null);
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

        private VisualElement BuildRegisterFile(string itemId)
        {
            var foldout = AssetManagerControls.CreateFoldout(
                I18N.Get("detail.file.register"));
            var filePath = AssetManagerControls.CreateTextField(
                I18N.Get("field.filePath"));
            var fileName = AssetManagerControls.CreateTextField(
                I18N.Get("field.displayName"));
            foldout.Add(filePath);
            foldout.Add(fileName);
            foldout.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.register"),
                () => RegisterFile(
                    itemId,
                    filePath.value,
                    fileName.value),
                "ee4v-asset-manager__primary-action"));
            return foldout;
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

            host.Add(CreateDetailTitle(
                file.FileName,
                I18N.Get("detail.file.eyebrow")));
            host.Add(CreateKeyValue(
                I18N.Get("field.source"),
                file.SourceType + " · " + file.SourcePath));
            host.Add(CreateKeyValue(
                I18N.Get("field.fileId"),
                file.Id));

            var itemId = AssetManagerControls.CreateTextField(
                I18N.Get("field.assignedItemId"));
            itemId.value = file.ItemId ?? string.Empty;
            host.Add(itemId);
            host.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.moveFile"),
                () => MoveFile(file.Id, itemId.value)));

            var targets = AssetManagerControls.CreateTextField(
                I18N.Get("field.targetPaths"));
            targets.multiline = true;
            targets.value = string.Join(
                "\n",
                _manager.GetFileTargets(file.Id)
                    .Select(target => target.TargetPath));
            host.Add(targets);
            host.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.saveTargets"),
                () => SaveTargets(file.Id, targets.value)));

            var dependencies = AssetManagerControls.CreateTextField(
                I18N.Get("field.dependencyFileIds"));
            dependencies.multiline = true;
            dependencies.value = string.Join(
                "\n",
                _manager.GetFileDependencies(file.Id)
                    .Select(dependency => dependency.DependencyFileId));
            host.Add(dependencies);
            host.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.saveDependencies"),
                () => SaveDependencies(file.Id, dependencies.value)));

            var entries = AssetManagerControls.CreateTextField(
                I18N.Get("field.archiveEntries"));
            entries.multiline = true;
            host.Add(entries);
            var importActions = new VisualElement();
            importActions.AddToClassList(
                "ee4v-asset-manager__actions");
            importActions.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.analyze"),
                () => AnalyzeFile(file.Id, entries)));
            importActions.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.importEntries"),
                () => ImportEntries(file.Id, entries.value)));
            importActions.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.importTargets"),
                () => ImportTargets(file.Id),
                "ee4v-asset-manager__primary-action"));
            host.Add(importActions);

            host.Add(CreateSectionTitle(
                I18N.Get("detail.importedAssetGuids")));
            host.Add(UiTextFactory.Create(
                string.Join(
                    "\n",
                    _manager.GetFileImportedAssetGuids(file.Id).DefaultIfEmpty(
                        I18N.Get("common.none"))),
                "ee4v-asset-manager__mono"));

            var actions = new VisualElement();
            actions.AddToClassList("ee4v-asset-manager__actions");
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get(
                    file.IsArchived
                        ? "action.restore"
                        : "action.archive"),
                () => ArchiveFile(file.Id, !file.IsArchived)));
            actions.Add(AssetManagerControls.CreateDangerButton(
                I18N.Get("action.delete"),
                () => DeleteFile(file.Id)));
            host.Add(actions);
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

            var files = GetFiles(item.Id);
            var layout = new VisualElement();
            layout.AddToClassList("ee4v-asset-manager__item-detail-layout");
            var treePane = new VisualElement();
            treePane.AddToClassList("ee4v-asset-manager__item-detail-tree-pane");
            if (_fileTree == null)
            {
                _fileTree = new SearchableFileTree(_manager);
                _fileTree.SelectionChanged += OnFileTreeSelectionChanged;
            }
            treePane.Add(_fileTree);
            layout.Add(treePane);

            _itemDetailPane = new ScrollView(ScrollViewMode.Vertical);
            _itemDetailPane.AddToClassList(
                "ee4v-asset-manager__item-detail-pane");
            layout.Add(_itemDetailPane);
            _content.Add(layout);

            _fileTreeSelection = null;
            _fileTree.SetItem(item.Id, files);
            RefreshItemDetailPane(item, files);
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
            var information = CreateItemDetailSection(
                I18N.Get("detail.information"));
            information.Add(CreateKeyValue(
                I18N.Get("field.name"),
                item.Name));
            information.Add(CreateKeyValue(
                I18N.Get("field.description"),
                string.IsNullOrWhiteSpace(item.Description)
                    ? I18N.Get("common.none")
                    : item.Description));
            var tagPaths = GetTagPaths(item);
            information.Add(CreateKeyValue(
                I18N.Get("field.tags"),
                tagPaths.Count == 0
                    ? I18N.Get("common.none")
                    : string.Join(", ", tagPaths)));
            information.Add(CreateKeyValue(
                I18N.Get("field.fileCount"),
                files.Count.ToString()));
            information.Add(CreateKeyValue(
                I18N.Get("field.fileTypes"),
                GetFileTypes(files)));
            information.Add(CreateKeyValue(
                I18N.Get("field.source"),
                GetItemSources(item, files)));
            information.Add(CreateKeyValue(
                I18N.Get("field.createdAt"),
                FormatTimestamp(item.CreatedAt)));
            information.Add(CreateKeyValue(
                I18N.Get("field.updatedAt"),
                FormatTimestamp(item.UpdatedAt)));
            information.Add(CreateKeyValue(
                I18N.Get("field.status"),
                I18N.Get(item.IsArchived
                    ? "detail.item.archived"
                    : "detail.item.active")));
            information.Add(CreateKeyValue(
                I18N.Get("field.itemId"),
                item.Id));
            information.Add(CreateKeyValue(
                I18N.Get("field.sourceId"),
                string.IsNullOrWhiteSpace(item.SourceId)
                    ? I18N.Get("common.none")
                    : item.SourceId));
            detail.Add(information);
            detail.Add(BuildRegisterFile(item.Id));

            var guidSection = CreateItemDetailSection(
                I18N.Get("detail.importedAssetGuids"));
            guidSection.Add(UiTextFactory.Create(
                string.Join(
                    "\n",
                    _manager.GetItemImportedAssetGuids(item.Id).DefaultIfEmpty(
                        I18N.Get("common.none"))),
                "ee4v-asset-manager__mono"));
            detail.Add(guidSection);
        }

        private static void BuildFileEntryDetail(
            VisualElement detail,
            AssetFile file,
            AssetFileContentEntry entry)
        {
            detail.Add(CreateDetailTitle(
                Path.GetFileName(entry.Path),
                I18N.Get("detail.file.entryEyebrow")));
            detail.Add(CreateKeyValue(
                I18N.Get("field.file"),
                file?.FileName ?? I18N.Get("common.none")));
            detail.Add(CreateKeyValue(
                I18N.Get("field.path"),
                entry.Path));
            detail.Add(CreateKeyValue(
                I18N.Get("field.kind"),
                I18N.Get(entry.Kind == AssetFileContentEntryKind.Directory
                    ? "detail.file.directory"
                    : "detail.file.file")));
            detail.Add(CreateKeyValue(
                I18N.Get("field.size"),
                entry.SizeBytes.ToString("N0") + " B"));
            detail.Add(CreateKeyValue(
                I18N.Get("field.assetGuid"),
                string.IsNullOrWhiteSpace(entry.AssetGuid)
                    ? I18N.Get("common.none")
                    : entry.AssetGuid));
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
                "ee4v-asset-manager__file-meta");
            meta.SetWhiteSpace(WhiteSpace.NoWrap);
            button.Add(meta);
            button.tooltip = file.FileName;
            return button;
        }

        private static VisualElement CreateItemDetailSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList(
                "ee4v-asset-manager__item-detail-section");
            section.Add(CreateSectionTitle(title));
            return section;
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

        private VisualElement BuildCollectionHeader()
        {
            var collection = _manager.GetCollections().FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    _viewState.CollectionId,
                    StringComparison.Ordinal));
            var bar = new VisualElement();
            bar.AddToClassList("ee4v-asset-manager__collection-bar");
            bar.Add(UiTextFactory.Create(
                DescribeFilter(collection?.Root),
                "ee4v-asset-manager__collection-filter"));
            UiButton editButton = null;
            editButton = AssetManagerControls.CreateButton(
                I18N.Get("action.edit"),
                () => ShowCollectionEditor(editButton, collection));
            bar.Add(editButton);
            bar.Add(AssetManagerControls.CreateDangerButton(
                I18N.Get("action.delete"),
                () => DeleteCollection(collection?.Id)));
            return bar;
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

        private void SaveTargets(string fileId, string paths)
        {
            Run(() =>
                _manager.SetFileTargets(fileId, SplitLines(paths)));
        }

        private void SaveDependencies(string fileId, string dependencyIds)
        {
            Run(() =>
                _manager.SetFileDependencies(
                    new[] { fileId },
                    SplitLines(dependencyIds)));
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

        private async void ImportTargets(string fileId)
        {
            await RunImport(
                () => _manager.ImportFileTargets(fileId));
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

        private static string DescribeFilter(AssetFilterNode root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            if (root.Type == AssetFilterNodeType.Condition)
            {
                return AssetManagerControls.FormatFilterCondition(
                           root.ConditionType.HasValue
                               ? (Enum)root.ConditionType.Value
                               : null) +
                       ": " + root.Value;
            }

            var children = root.Children ?? Array.Empty<AssetFilterNode>();
            if (root.Type == AssetFilterNodeType.Not)
            {
                return string.Format(
                    I18N.Get("filterSummary.not"),
                    children.Count > 0
                        ? DescribeFilter(children[0])
                        : string.Empty);
            }

            var separator = " " + I18N.Get(
                root.Type == AssetFilterNodeType.Or
                    ? "filterSummary.or"
                    : "filterSummary.and") + " ";
            return string.Join(
                separator,
                children.Select(DescribeFilter));
        }

        private static VisualElement CreateDetailTitle(string title, string eyebrow)
        {
            var header = new VisualElement();
            header.AddToClassList("ee4v-asset-manager__detail-header");
            header.Add(UiTextFactory.Create(
                eyebrow,
                "ee4v-asset-manager__eyebrow"));
            header.Add(UiTextFactory.Create(
                title,
                "ee4v-asset-manager__detail-title"));
            return header;
        }

        private static UiTextElement CreateSectionTitle(string text)
        {
            return UiTextFactory.Create(
                text,
                "ee4v-asset-manager__section-title");
        }

        private static VisualElement CreateKeyValue(string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-asset-manager__key-value");
            row.Add(UiTextFactory.Create(
                key,
                "ee4v-asset-manager__key"));
            var valueText = UiTextFactory.Create(
                value,
                "ee4v-asset-manager__value");
            valueText.SetWhiteSpace(WhiteSpace.Normal);
            row.Add(valueText);
            return row;
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
                _itemGrid.ClearThumbnails();
            }

            if (_defersManagerRefresh)
            {
                _managerRefreshPending = true;
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
                case AssetManagerViewStateChange.ItemSort:
                    if (ShowsMain)
                    {
                        Refresh();
                    }
                    else
                    {
                        RefreshSortButton();
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

    internal sealed class AssetThumbnailStack : VisualElement, IDisposable
    {
        private const int MaximumThumbnailCount = 3;
        private const float MinimumSize = 48f;
        private const float MaximumSize = 288f;
        private const float MultiImageInsetMultiplier = 2f;
        private static readonly float[] SlotLeftOffsetMultipliers =
            { -0.85f, 0f, 0.85f };
        private static readonly float[] SlotTopOffsetMultipliers =
            { -0.55f, 0f, 0.85f };
        private static readonly float[] SlotRotations =
            { -4.5f, 0.8f, 4.2f };
        private readonly List<ThumbnailSlot> _slots =
            new List<ThumbnailSlot>();

        public AssetThumbnailStack(
            CachedImageCache imageCache,
            IReadOnlyList<string> itemIds)
        {
            if (imageCache == null)
            {
                throw new ArgumentNullException(nameof(imageCache));
            }

            AddToClassList("ee4v-asset-manager__thumbnail-stack");
            var safeIds = itemIds ?? Array.Empty<string>();
            var firstIndex = Math.Max(
                0,
                safeIds.Count - MaximumThumbnailCount);
            for (var index = firstIndex; index < safeIds.Count; index++)
            {
                var slot = new ThumbnailSlot(
                    imageCache,
                    safeIds[index]);
                _slots.Add(slot);
                Add(slot.Root);
            }

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public void Refresh(string itemId)
        {
            for (var index = 0; index < _slots.Count; index++)
            {
                if (string.Equals(
                        _slots[index].ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    _slots[index].Refresh();
                }
            }
        }

        public void Dispose()
        {
            UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            for (var index = 0; index < _slots.Count; index++)
            {
                _slots[index].Dispose();
            }
            _slots.Clear();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var size = Mathf.Clamp(
                evt.newRect.width,
                MinimumSize,
                MaximumSize);
            if (float.IsNaN(size) || size <= 0f)
            {
                return;
            }

            style.height = size;
            style.minHeight = size;
            style.maxHeight = size;
            if (_slots.Count == 1)
            {
                ApplySlotLayout(0, size, 0f, 0f, 0f);
                return;
            }

            var offset = Mathf.Clamp(size * 0.065f, 6f, 18f);
            var imageSize = Mathf.Max(
                MinimumSize,
                size - (offset * MultiImageInsetMultiplier));
            var centerOffset = (size - imageSize) * 0.5f;
            for (var index = 0; index < _slots.Count; index++)
            {
                ApplySlotLayout(
                    index,
                    imageSize,
                    centerOffset +
                    (offset * SlotLeftOffsetMultipliers[index]),
                    centerOffset +
                    (offset * SlotTopOffsetMultipliers[index]),
                    SlotRotations[index]);
            }
        }

        private void ApplySlotLayout(
            int index,
            float size,
            float left,
            float top,
            float rotation)
        {
            var slot = _slots[index].Root;
            slot.style.width = size;
            slot.style.height = size;
            slot.style.left = left;
            slot.style.top = top;
            slot.style.rotate = new Rotate(new Angle(
                rotation,
                AngleUnit.Degree));
        }

        private sealed class ThumbnailSlot : IDisposable
        {
            private readonly CachedImage _image;
            private readonly VisualElement _placeholder;

            public ThumbnailSlot(
                CachedImageCache imageCache,
                string itemId)
            {
                ItemId = itemId ?? string.Empty;
                Root = new VisualElement();
                Root.AddToClassList(
                    "ee4v-asset-manager__thumbnail-stack-image");
                _placeholder = new VisualElement();
                _placeholder.AddToClassList(
                    "ee4v-asset-manager__thumbnail-placeholder");
                _image = new CachedImage(imageCache)
                {
                    scaleMode = ScaleMode.ScaleAndCrop
                };
                _image.AddToClassList(
                    "ee4v-asset-manager__thumbnail-image");
                Root.Add(_placeholder);
                Root.Add(_image);
                Refresh();
            }

            public string ItemId { get; }
            public VisualElement Root { get; }

            public void Refresh()
            {
                _image.SetSource(ItemId);
                var hasImage = _image.DisplayedTexture != null;
                _image.style.display = hasImage
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _placeholder.style.display = hasImage
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            public void Dispose()
            {
                _image.Dispose();
            }
        }
    }
}
