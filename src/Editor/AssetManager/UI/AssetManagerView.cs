using System;
using System.Collections.Generic;
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
        private readonly AssetItemGridView _fileGrid;
        private AssetManagerGridSizeSlider _gridSizeSlider;
        private VisualElement _gridControls;
        private AssetManagerSearchField _search;
        private AssetManagerButton _sortButton;
        private AssetManagerButton _backButton;
        private AssetManagerButton _forwardButton;
        private AssetManagerBreadcrumb _breadcrumbs;

        private CachedImage _detailThumbnail;
        private CancellationTokenSource _thumbnailCancellation;
        private CancellationTokenSource _gridThumbnailCancellation;
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
            _fileGrid = new AssetItemGridView();
            _itemGrid.SelectionChanged += SelectItems;
            _itemGrid.ItemDoubleClicked += BrowseItemFiles;
            _itemGrid.RecommendedMinimumItemsPerRowChanged +=
                SetMinimumGridSize;
            _fileGrid.SelectionChanged += SelectFile;
            _fileGrid.RecommendedMinimumItemsPerRowChanged +=
                SetMinimumGridSize;

            AddToClassList("ee4v-asset-manager");

            _navigation = new ScrollView();
            _navigation.AddToClassList("ee4v-asset-manager__navigation");
            _navigation.contentContainer.AddToClassList(
                "ee4v-asset-manager__navigation-content");
            _content = new VisualElement();
            _content.AddToClassList("ee4v-asset-manager__content");
            _detail = new ScrollView();
            _detail.AddToClassList("ee4v-asset-manager__detail");
            var layout = new AssetManagerThreePaneLayout(mode);
            layout.LeftToolbarContent.Add(UiTextFactory.Create(
                I18N.Get("pane.brand"),
                "ee4v-asset-manager__brand"));
            layout.MainToolbarContent.Add(BuildToolbar());
            layout.RightToolbarContent.Add(UiTextFactory.Create(
                I18N.Get("pane.information"),
                "ee4v-asset-manager__pane-title"));
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
            _itemGrid.ItemDoubleClicked -= BrowseItemFiles;
            _itemGrid.RecommendedMinimumItemsPerRowChanged -=
                SetMinimumGridSize;
            _fileGrid.SelectionChanged -= SelectFile;
            _fileGrid.RecommendedMinimumItemsPerRowChanged -=
                SetMinimumGridSize;
            _search.SearchOptionsClicked -= ShowSearchTargetsMenu;

            CancelGridThumbnails();
            CancelThumbnail();
            ClearDetailThumbnail();
            _breadcrumbs?.Dispose();
            _itemGrid.Dispose();
            _fileGrid.Dispose();
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

        private bool ShowsFileGrid =>
            !string.IsNullOrEmpty(_viewState.BrowsingItemId) ||
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
                "ee4v-asset-manager__search");
            _search.tooltip = I18N.Get("toolbar.search.tooltip");
            _search.RegisterValueChangedCallback(_ => Refresh());
            _search.SearchOptionsClicked += ShowSearchTargetsMenu;

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
            if (!ShowsFileGrid)
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
            menu.DropDown(_search.SearchOptionsAnchor.worldBound);
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
            return ShowsFileGrid
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
            if (string.IsNullOrEmpty(_viewState.BrowsingItemId))
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
                    FindItem(_viewState.BrowsingItemId)?.Name ??
                    I18N.Get("common.item"))
            });
        }

        private void RebuildNavigation()
        {
            _navigation.Clear();
            _navigation.Add(CreateNavigationButton(
                I18N.Get("navigation.library"),
                AssetManagerPage.Library));
            _navigation.Add(CreateNavigationButton(
                I18N.Get("navigation.archived"),
                AssetManagerPage.Archived));
            _navigation.Add(CreateNavigationButton(
                I18N.Get("navigation.unassignedFiles"),
                AssetManagerPage.UnassignedFiles));
            _navigation.Add(CreateNavigationButton(
                I18N.Get("navigation.sources"),
                AssetManagerPage.Sources));

            _navigation.Add(UiTextFactory.Create(
                I18N.Get("navigation.collections"),
                "ee4v-asset-manager__nav-section"));
            var collections = GetCollections();
            for (var i = 0; i < collections.Count; i++)
            {
                var collection = collections[i];
                var button = AssetManagerControls.CreateButton(
                    collection.Name,
                    () => SelectCollection(collection.Id));
                button.EnableInClassList(
                    "ee4v-asset-manager__nav-button--selected",
                    _viewState.Page == AssetManagerPage.Collection &&
                    string.Equals(
                        _viewState.CollectionId,
                        collection.Id,
                        StringComparison.Ordinal));
                button.AddToClassList("ee4v-asset-manager__nav-button");
                _navigation.Add(button);
            }

            _navigation.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("navigation.newCollection"),
                "add.png",
                ShowNewCollection,
                "ee4v-asset-manager__nav-button",
                "ee4v-asset-manager__nav-create"));

            var spacer = new VisualElement();
            spacer.AddToClassList("ee4v-asset-manager__nav-spacer");
            _navigation.Add(spacer);
            _navigation.Add(AssetManagerControls.CreateButton(
                I18N.Get("navigation.newItem"),
                ShowNewItem,
                "ee4v-asset-manager__nav-button",
                "ee4v-asset-manager__nav-new-item",
                "ee4v-asset-manager__primary-action"));
        }

        private AssetManagerButton CreateNavigationButton(
            string label,
            AssetManagerPage page)
        {
            var button = AssetManagerControls.CreateButton(
                label,
                () => SelectPage(page),
                "ee4v-asset-manager__nav-button");
            button.EnableInClassList(
                "ee4v-asset-manager__nav-button--selected",
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
                    RefreshHistoryNavigation();
                    RefreshSortButton();
                    if (!string.IsNullOrEmpty(
                            _viewState.BrowsingItemId))
                    {
                        BuildItemFiles();
                    }
                    else
                    {
                        switch (_viewState.Page)
                        {
                            case AssetManagerPage.UnassignedFiles:
                                BuildUnassignedFiles();
                                break;
                            case AssetManagerPage.Sources:
                                BuildSources();
                                break;
                            default:
                                BuildItems();
                                break;
                        }
                    }
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

        private void Reload()
        {
            _itemGrid.ClearThumbnails();
            Refresh();
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

        private void SelectItems(
            IReadOnlyList<string> itemIds,
            string primaryItemId)
        {
            _viewState.SelectItems(itemIds, primaryItemId);
        }

        private void BrowseItemFiles(string itemId)
        {
            _viewState.BrowseItemFiles(itemId);
        }

        private void SetGridSize(int value)
        {
            _itemGrid.SetItemsPerRow(value);
            _fileGrid.SetItemsPerRow(value);
            _gridSizeSlider.SetValueWithoutNotify(
                GetActiveGrid().ItemsPerRow);
        }

        private void SetMinimumGridSize(int value)
        {
            _gridSizeSlider.SetRangeWithoutNotify(
                value,
                AssetItemGridView.MaximumItemsPerRow);
            _gridSizeSlider.SetValueWithoutNotify(
                GetActiveGrid().ItemsPerRow);
        }

        private AssetItemGridView GetActiveGrid()
        {
            return !string.IsNullOrEmpty(_viewState.BrowsingItemId) ||
                   _viewState.Page == AssetManagerPage.UnassignedFiles
                ? _fileGrid
                : _itemGrid;
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

        private async void ShowItemDetail(AssetItem item)
        {
            _detail.Clear();
            if (item == null)
            {
                ShowEmptyDetail(I18N.Get("notice.selectedItemMissing"));
                return;
            }

            _detail.Add(CreateDetailTitle(
                item.Name,
                I18N.Get("detail.item.eyebrow")));
            var thumbnailHost = new VisualElement();
            thumbnailHost.AddToClassList("ee4v-asset-manager__thumbnail");
            thumbnailHost.Add(UiTextFactory.Create(
                I18N.Get("detail.item.noThumbnail")));
            _detail.Add(thumbnailHost);

            var name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"));
            name.value = item.Name;
            var description = AssetManagerControls.CreateTextField(
                I18N.Get("field.description"));
            description.multiline = true;
            description.value = item.Description ?? string.Empty;
            var tags = AssetManagerControls.CreateTextField(
                I18N.Get("field.tags"));
            tags.value = string.Join(", ", (item.Tags ?? Array.Empty<AssetTag>()).Select(tag => tag.Path));
            _detail.Add(name);
            _detail.Add(description);
            _detail.Add(tags);

            var actions = CreateActionRow();
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.save"),
                () => SaveItem(item.Id, name.value, description.value, tags.value),
                "ee4v-asset-manager__primary-action"));
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get(
                    item.IsArchived
                        ? "action.restore"
                        : "action.archive"),
                () => ArchiveItem(item.Id, !item.IsArchived)));
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.delete"),
                () => DeleteItem(item.Id),
                "ee4v-asset-manager__danger-action"));
            _detail.Add(actions);

            _detail.Add(CreateSectionTitle(
                I18N.Get("detail.files")));
            var files = GetFiles(item.Id);
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                _detail.Add(AssetManagerControls.CreateButton(
                    file.FileName,
                    () => SelectFile(file),
                    "ee4v-asset-manager__file-button"));
            }

            _detail.Add(BuildRegisterFile(item.Id));
            _detail.Add(CreateSectionTitle(
                I18N.Get("detail.importedAssetGuids")));
            _detail.Add(UiTextFactory.Create(
                string.Join(
                    "\n",
                    GetItemGuids(item.Id).DefaultIfEmpty(
                        I18N.Get("common.none"))),
                "ee4v-asset-manager__mono"));

            if (_imageCache.HasSource(item.Id))
            {
                ShowCachedThumbnail(item.Id, thumbnailHost);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _thumbnailCancellation = cancellation;
            try
            {
                var thumbnail = await _manager.GetThumbnail(
                    item.Id,
                    cancellation.Token);
                if (!ReferenceEquals(
                        _thumbnailCancellation,
                        cancellation))
                {
                    return;
                }

                _itemGrid.SetThumbnail(
                    item.Id,
                    thumbnail != null && thumbnail.Found
                        ? thumbnail.Data
                        : null);
                ShowCachedThumbnail(item.Id, thumbnailHost);
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
                        _thumbnailCancellation,
                        cancellation))
                {
                    cancellation.Dispose();
                    _thumbnailCancellation = null;
                }
            }
        }

        private void RefreshDetail()
        {
            CancelThumbnail();
            ClearDetailThumbnail();
            switch (_viewState.InformationContent)
            {
                case AssetManagerInformationContent.NewItem:
                    RenderNewItem();
                    return;
                case AssetManagerInformationContent.CollectionEditor:
                    RenderCollectionEditor(
                        string.IsNullOrEmpty(
                            _viewState.EditingCollectionId)
                            ? null
                            : _manager.GetCollection(
                                _viewState.EditingCollectionId));
                    return;
            }

            if (_viewState.Page == AssetManagerPage.Sources)
            {
                _detail.Clear();
                _detail.Add(BuildAssociationLookup());
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
                ShowItemDetail(FindItem(
                    _viewState.SelectedItemId));
                return;
            }

            ShowEmptyDetail(
                _viewState.Page == AssetManagerPage.UnassignedFiles
                    ? I18N.Get("notice.selectFile")
                    : I18N.Get("notice.selectItem"));
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

            _detail.Add(CreateDetailTitle(
                file.FileName,
                I18N.Get("detail.file.eyebrow")));
            _detail.Add(CreateKeyValue(
                I18N.Get("field.source"),
                file.SourceType + " · " + file.SourcePath));
            _detail.Add(CreateKeyValue(
                I18N.Get("field.fileId"),
                file.Id));

            var itemId = AssetManagerControls.CreateTextField(
                I18N.Get("field.assignedItemId"));
            itemId.value = file.ItemId ?? string.Empty;
            _detail.Add(itemId);
            _detail.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.moveFile"),
                () => MoveFile(file.Id, itemId.value)));

            var targets = AssetManagerControls.CreateTextField(
                I18N.Get("field.targetPaths"));
            targets.multiline = true;
            targets.value = string.Join("\n", GetTargets(file.Id).Select(target => target.TargetPath));
            _detail.Add(targets);
            _detail.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.saveTargets"),
                () => SaveTargets(file.Id, targets.value)));

            var dependencies = AssetManagerControls.CreateTextField(
                I18N.Get("field.dependencyFileIds"));
            dependencies.multiline = true;
            dependencies.value = string.Join(
                "\n",
                GetDependencies(file.Id).Select(dependency => dependency.DependencyFileId));
            _detail.Add(dependencies);
            _detail.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.saveDependencies"),
                () => SaveDependencies(file.Id, dependencies.value)));

            var entries = AssetManagerControls.CreateTextField(
                I18N.Get("field.archiveEntries"));
            entries.multiline = true;
            _detail.Add(entries);
            var importActions = CreateActionRow();
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
            _detail.Add(importActions);

            _detail.Add(CreateSectionTitle(
                I18N.Get("detail.importedAssetGuids")));
            _detail.Add(UiTextFactory.Create(
                string.Join(
                    "\n",
                    GetFileGuids(file.Id).DefaultIfEmpty(
                        I18N.Get("common.none"))),
                "ee4v-asset-manager__mono"));

            var actions = CreateActionRow();
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get(
                    file.IsArchived
                        ? "action.restore"
                        : "action.archive"),
                () => ArchiveFile(file.Id, !file.IsArchived)));
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.delete"),
                () => DeleteFile(file.Id),
                "ee4v-asset-manager__danger-action"));
            _detail.Add(actions);
        }

        private void SelectFile(AssetFile file)
        {
            _viewState.SelectFile(file?.Id);
        }

        private void SelectFile(
            IReadOnlyList<string> fileIds,
            string primaryFileId)
        {
            _fileGrid.SetSelectedItemId(primaryFileId);
            _viewState.SelectFile(primaryFileId);
        }

        private void BuildItemFiles()
        {
            CancelGridThumbnails();
            _content.Clear();
            var item = FindItem(_viewState.BrowsingItemId);
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.Flex;
            var files = item == null
                ? Array.Empty<AssetFile>()
                : AssetManagerItemSort.Apply(
                    GetFiles(item.Id)
                        .Where(file => AssetManagerSearch.MatchesFile(
                            file,
                            _search.value,
                            _viewState.SearchTargets)),
                    _viewState.ItemSortField,
                    _viewState.IsItemSortReversed);
            if (item == null)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.itemMissing")));
            }
            else if (files.Count == 0)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.noAssignedFiles")));
            }

            SetFileGridItems(files);
            _content.Add(_fileGrid);
        }

        private void BuildUnassignedFiles()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.Flex;
            var files = AssetManagerItemSort.Apply(
                GetUnassignedFiles()
                    .Where(file => AssetManagerSearch.MatchesFile(
                        file,
                        _search.value,
                        _viewState.SearchTargets)),
                _viewState.ItemSortField,
                _viewState.IsItemSortReversed);
            if (files.Count == 0)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    I18N.Get("notice.noUnassignedFiles")));
            }

            SetFileGridItems(files);
            _content.Add(_fileGrid);
        }

        private void SetFileGridItems(IReadOnlyList<AssetFile> files)
        {
            _fileGrid.SetItems(files.Select(file =>
                    new AssetItemGridEntry(
                        file.Id,
                        file.FileName,
                        icon: AssetFileIconResolver.Resolve(file)))
                .ToArray());
            _fileGrid.SetSelectedItemId(_viewState.SelectedFileId);
        }

        private void BuildSources()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.None;
            _sortButton.style.display = DisplayStyle.None;
            _gridControls.style.display = DisplayStyle.None;
            var scroll = CreateContentScroll();
            _content.Add(scroll);

            scroll.Add(BuildEagleSource());
            scroll.Add(BuildEe4vSource());
            scroll.Add(BuildEe4vImport());
        }

        private static ScrollView CreateContentScroll()
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("ee4v-asset-manager__content-scroll");
            return scroll;
        }

        private VisualElement BuildEagleSource()
        {
            var card = CreateSourceCard(
                I18N.Get("source.eagle.title"),
                I18N.Get("source.eagle.description"));
            card.Add(AssetManagerControls.CreateButton(
                I18N.Get("source.eagle.action"),
                SyncEagle,
                "ee4v-asset-manager__primary-action"));
            return card;
        }

        private VisualElement BuildEe4vSource()
        {
            var card = CreateSourceCard(
                I18N.Get("source.ee4v.title"),
                I18N.Get("source.ee4v.description"));
            card.Add(AssetManagerControls.CreateButton(
                I18N.Get("source.ee4v.action"),
                SyncEe4v,
                "ee4v-asset-manager__primary-action"));
            return card;
        }

        private VisualElement BuildEe4vImport()
        {
            var card = CreateSourceCard(
                I18N.Get("source.import.title"),
                I18N.Get("source.import.description"));
            var file = AssetManagerControls.CreateTextField(
                I18N.Get("field.filePath"));
            var name = AssetManagerControls.CreateTextField(
                I18N.Get("field.itemNameOptional"));
            var description = AssetManagerControls.CreateTextField(
                I18N.Get("field.descriptionOptional"));
            var tags = AssetManagerControls.CreateTextField(
                I18N.Get("field.tagsCommaSeparated"));
            card.Add(file);
            card.Add(name);
            card.Add(description);
            card.Add(tags);
            card.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.importFile"),
                () => ImportEe4vFile(
                    file.value,
                    name.value,
                    description.value,
                    tags.value),
                "ee4v-asset-manager__primary-action"));
            return card;
        }

        private VisualElement BuildAssociationLookup()
        {
            var card = CreateSourceCard(
                I18N.Get("source.lookup.title"),
                I18N.Get("source.lookup.description"));
            var guids = AssetManagerControls.CreateTextField(
                I18N.Get("field.guids"));
            guids.multiline = true;
            var results = UiTextFactory.Create(
                I18N.Get("source.lookup.empty"),
                "ee4v-asset-manager__mono");
            card.Add(guids);
            card.Add(AssetManagerControls.CreateButton(
                I18N.Get("source.lookup.action"),
                () => ShowAssociations(guids.value, results)));
            card.Add(results);
            return card;
        }

        private VisualElement BuildCollectionHeader()
        {
            var collection = GetCollections().FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    _viewState.CollectionId,
                    StringComparison.Ordinal));
            var bar = new VisualElement();
            bar.AddToClassList("ee4v-asset-manager__collection-bar");
            bar.Add(UiTextFactory.Create(
                DescribeFilter(collection?.Root),
                "ee4v-asset-manager__collection-filter"));
            bar.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.edit"),
                () => _viewState.ShowCollectionEditor(
                    collection?.Id)));
            bar.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.delete"),
                () => DeleteCollection(collection?.Id),
                "ee4v-asset-manager__danger-action"));
            return bar;
        }

        private void ShowNewItem()
        {
            _viewState.ShowNewItem();
        }

        private void RenderNewItem()
        {
            _detail.Clear();
            _detail.Add(CreateDetailTitle(
                I18N.Get("navigation.newItem"),
                I18N.Get("detail.createEyebrow")));
            var name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"));
            var description = AssetManagerControls.CreateTextField(
                I18N.Get("field.description"));
            description.multiline = true;
            _detail.Add(name);
            _detail.Add(description);
            _detail.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.createItem"),
                () => CreateItem(name.value, description.value),
                "ee4v-asset-manager__primary-action"));
        }

        private void ShowNewCollection()
        {
            _viewState.ShowCollectionEditor(null);
        }

        private void RenderCollectionEditor(AssetCollection collection)
        {
            _detail.Clear();
            _detail.Add(CreateDetailTitle(
                collection == null
                    ? I18N.Get("common.newCollection")
                    : collection.Name,
                I18N.Get(
                    collection == null
                        ? "detail.createEyebrow"
                        : "detail.collectionEyebrow")));
            var name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"));
            name.value = collection?.Name ?? string.Empty;
            var condition = AssetManagerControls.CreateEnumField(
                I18N.Get("field.condition"),
                GetConditionType(collection?.Root));
            var value = AssetManagerControls.CreateTextField(
                I18N.Get("field.value"));
            value.value = GetConditionValue(collection?.Root);
            _detail.Add(name);
            _detail.Add(condition);
            _detail.Add(value);
            _detail.Add(AssetManagerControls.CreateNotice(
                I18N.Get("notice.collectionCondition")));
            _detail.Add(AssetManagerControls.CreateButton(
                I18N.Get(
                    collection == null
                        ? "action.createCollection"
                        : "action.saveCollection"),
                () => SaveCollection(
                    collection?.Id,
                    name.value,
                    (AssetFilterConditionType)condition.value,
                    value.value),
                "ee4v-asset-manager__primary-action"));
        }

        private void CreateItem(string name, string description)
        {
            Run(() =>
            {
                var item = _manager.CreateItem(new CreateAssetItemRequest
                {
                    Name = name,
                    Description = description
                });
                _viewState.SelectItem(item.Id);
            });
        }

        private void SaveItem(string id, string name, string description, string tags)
        {
            Run(() =>
            {
                _manager.UpdateItem(id, new UpdateAssetItemRequest
                {
                    Name = name,
                    Description = description
                });
                _manager.SetItemTags(new[] { id }, SplitCommaSeparated(tags));
            });
        }

        private void ArchiveItem(string id, bool archived)
        {
            Run(() =>
                _manager.SetItemArchived(new[] { id }, archived));
        }

        private void DeleteItem(string id)
        {
            if (!Confirm(
                    I18N.Get("confirm.deleteItem.title"),
                    I18N.Get("confirm.deleteItem.message")))
            {
                return;
            }

            Run(() =>
            {
                _manager.DeleteItem(new[] { id });
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

        private void AnalyzeFile(
            string fileId,
            AssetManagerTextField output)
        {
            Run(() =>
            {
                var analysis = _manager.AnalyzeFile(fileId);
                output.value = string.Join(
                    "\n",
                    (analysis?.Entries ??
                     Array.Empty<AssetFileContentEntry>())
                    .Where(entry => entry != null)
                    .Select(entry => entry.Path));
            }, refresh: false);
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

        private void SaveCollection(
            string id,
            string name,
            AssetFilterConditionType condition,
            string value)
        {
            Run(() =>
            {
                var root = AssetFilterNode.Condition(condition, value);
                if (string.IsNullOrEmpty(id))
                {
                    var created = _manager.CreateCollection(
                        new CreateAssetCollectionRequest
                        {
                            Name = name,
                            Root = root
                        });
                    _viewState.SelectCollection(created.Id);
                }
                else
                {
                    _manager.UpdateCollection(
                        id,
                        new UpdateAssetCollectionRequest
                        {
                            Name = name,
                            Root = root
                        });
                }
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

        private void SyncEagle()
        {
            Run(() =>
                _manager.SyncEagle(new EagleSyncRequest(
                    EmptyToNull(AssetManagerSettings.EagleLibraryPath),
                    EmptyToNull(AssetManagerSettings.EagleTargetRoot))),
                refresh: false);
        }

        private void SyncEe4v()
        {
            Run(() =>
                _manager.SyncEe4v(new Ee4vSyncRequest(
                    AssetManagerSettings.Ee4vLibraryPath)),
                refresh: false);
        }

        private void ImportEe4vFile(
            string filePath,
            string name,
            string description,
            string tags)
        {
            Run(() =>
                _manager.ImportEe4vFile(new ImportEe4vFileRequest(
                    AssetManagerSettings.Ee4vLibraryPath,
                    filePath,
                    EmptyToNull(name),
                    EmptyToNull(description),
                    SplitCommaSeparated(tags))));
        }

        private void ShowAssociations(string guidText, UiTextElement output)
        {
            try
            {
                var guids = SplitLines(guidText);
                var associations = _manager.GetImportedAssetAssociations(
                    guids.Count == 0 ? null : guids);
                output.SetText(string.Join(
                    "\n",
                    associations.Select(association =>
                        association.AssetGuid + "  →  " + association.FileId)));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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

        private void Run(Action operation, bool refresh = true)
        {
            var succeeded = false;
            _defersManagerRefresh = refresh;
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
                _defersManagerRefresh = false;
                if (refresh &&
                    (succeeded || _managerRefreshPending))
                {
                    _managerRefreshPending = false;
                    RefreshAfterManagerChange();
                }
            }
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
                        _search.value,
                        _viewState.SearchTargets)
                };
                items = _manager.SearchItems(query).Items;
            }

            var visibleItems = items
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Archived ||
                    item.IsArchived)
                .Where(item => AssetManagerSearch.MatchesItem(
                    item,
                    _search.value,
                    _viewState.SearchTargets));
            return AssetManagerItemSort.Apply(
                visibleItems,
                _viewState.ItemSortField,
                _viewState.IsItemSortReversed);
        }

        private IReadOnlyList<AssetCollection> GetCollections()
        {
            return _manager.GetCollections();
        }

        private IReadOnlyList<AssetFile> GetFiles(string itemId)
        {
            return _manager.GetFiles(itemId, includeArchived: true);
        }

        private IReadOnlyList<AssetFile> GetUnassignedFiles()
        {
            return _manager.GetUnassignedFiles(includeArchived: true);
        }

        private IReadOnlyList<AssetFileTarget> GetTargets(string fileId)
        {
            return _manager.GetFileTargets(fileId);
        }

        private IReadOnlyList<AssetFileDependency> GetDependencies(string fileId)
        {
            return _manager.GetFileDependencies(fileId);
        }

        private IReadOnlyList<string> GetFileGuids(string fileId)
        {
            return _manager.GetFileImportedAssetGuids(fileId);
        }

        private IReadOnlyList<string> GetItemGuids(string itemId)
        {
            return _manager.GetItemImportedAssetGuids(itemId);
        }

        private AssetItem FindItem(string itemId)
        {
            return _manager.GetItem(itemId);
        }

        private string GetPageTitle()
        {
            switch (_viewState.Page)
            {
                case AssetManagerPage.Archived:
                    return I18N.Get("navigation.archived");
                case AssetManagerPage.Collection:
                    return GetCollections()
                        .FirstOrDefault(collection =>
                            collection.Id == _viewState.CollectionId)
                        ?.Name ?? I18N.Get("common.collection");
                case AssetManagerPage.UnassignedFiles:
                    return I18N.Get("navigation.unassignedFiles");
                case AssetManagerPage.Sources:
                    return I18N.Get("navigation.sources");
                default:
                    return I18N.Get("navigation.library");
            }
        }

        private static AssetFilterConditionType GetConditionType(AssetFilterNode root)
        {
            return root?.ConditionType ?? AssetFilterConditionType.NameContains;
        }

        private static string GetConditionValue(AssetFilterNode root)
        {
            return root?.Value ?? string.Empty;
        }

        private static string DescribeFilter(AssetFilterNode root)
        {
            return root?.ConditionType + ": " + root?.Value;
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
            row.Add(UiTextFactory.Create(
                value,
                "ee4v-asset-manager__value"));
            return row;
        }

        private static VisualElement CreateActionRow()
        {
            return new AssetManagerActionRow();
        }

        private static VisualElement CreateSourceCard(string title, string description)
        {
            return new AssetManagerCard(title, description);
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
                        _fileGrid.SetSelectedItemId(
                            _viewState.SelectedFileId);
                    }
                    if (ShowsInformation)
                    {
                        RefreshDetail();
                    }
                    break;
                case AssetManagerViewStateChange.Information:
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

        private void ShowCachedThumbnail(
            string itemId,
            VisualElement thumbnailHost)
        {
            var image = new CachedImage(_imageCache)
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            image.SetSource(itemId);
            if (image.DisplayedTexture == null)
            {
                image.Dispose();
                return;
            }

            thumbnailHost.Clear();
            thumbnailHost.Add(image);
            _detailThumbnail = image;
        }

        private void ClearDetailThumbnail()
        {
            _detailThumbnail?.Dispose();
            _detailThumbnail = null;
        }

        private static bool Confirm(string title, string message)
        {
            return EditorUtility.DisplayDialog(
                title,
                message,
                I18N.Get("action.delete"),
                I18N.Get("action.cancel"));
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

        private static IReadOnlyList<string> SplitCommaSeparated(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

    }
}
