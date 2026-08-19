using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;
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
        private readonly VisualElement _navigation;
        private readonly VisualElement _content;
        private readonly VisualElement _detail;
        private readonly AssetItemGridView _itemGrid;
        private UiTextElement _title;
        private UiTextElement _resultCount;
        private AssetManagerGridSizeSlider _gridSizeSlider;
        private VisualElement _gridControls;
        private readonly UiTextElement _status;
        private AssetManagerSearchField _search;

        private Texture2D _thumbnailTexture;
        private CancellationTokenSource _thumbnailCancellation;
        private CancellationTokenSource _gridThumbnailCancellation;

        public AssetManagerView(
            IAssetManager manager,
            AssetManagerViewState viewState = null,
            AssetManagerViewMode mode = AssetManagerViewMode.Combined)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _viewState = viewState ?? new AssetManagerViewState();
            _mode = mode;
            _itemGrid = new AssetItemGridView();
            _itemGrid.ItemSelected += SelectItem;
            _itemGrid.RecommendedMinimumItemsPerRowChanged +=
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
            _status = UiTextFactory.Create(
                "Ready",
                "ee4v-asset-manager__status");

            var layout = new AssetManagerThreePaneLayout(mode);
            layout.LeftToolbarContent.Add(UiTextFactory.Create(
                "ASSET MANAGER",
                "ee4v-asset-manager__brand"));
            layout.MainToolbarContent.Add(BuildToolbar());
            layout.RightToolbarContent.Add(UiTextFactory.Create(
                "INFORMATION",
                "ee4v-asset-manager__pane-title"));
            layout.LeftContent.Add(_navigation);
            layout.MainContent.Add(_content);
            layout.MainContent.Add(_status);
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
            _itemGrid.ItemSelected -= SelectItem;
            _itemGrid.RecommendedMinimumItemsPerRowChanged -=
                SetMinimumGridSize;

            CancelGridThumbnails();
            CancelThumbnail();
            DestroyThumbnail();
            _itemGrid.Dispose();
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

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-asset-manager__toolbar");

            var heading = new VisualElement();
            heading.AddToClassList("ee4v-asset-manager__heading");
            _title = UiTextFactory.Create(
                "Library",
                "ee4v-asset-manager__title");
            _resultCount = UiTextFactory.Create(
                string.Empty,
                "ee4v-asset-manager__count");
            heading.Add(_title);
            heading.Add(_resultCount);

            _search = AssetManagerControls.CreateSearchField(
                "Search items",
                "ee4v-asset-manager__search");
            _search.tooltip = "Search items";
            _search.RegisterValueChangedCallback(_ => Refresh());

            toolbar.Add(heading);
            _gridControls = new VisualElement();
            _gridControls.AddToClassList(
                "ee4v-asset-manager__grid-controls");
            _gridSizeSlider = AssetManagerControls.CreateGridSizeSlider(
                _itemGrid.ItemsPerRow,
                _itemGrid.RecommendedMinimumItemsPerRow,
                AssetItemGridView.MaximumItemsPerRow,
                "ee4v-asset-manager__grid-size-slider");
            _gridSizeSlider.tooltip = "Grid columns";
            _gridSizeSlider.ValueChanged += SetGridSize;
            _gridControls.Add(_gridSizeSlider);
            toolbar.Add(_gridControls);

            var actions = new VisualElement();
            actions.AddToClassList("ee4v-asset-manager__toolbar-actions");
            actions.Add(_search);
            actions.Add(AssetManagerControls.CreateReloadButton(
                Refresh,
                "ee4v-asset-manager__reload"));
            toolbar.Add(actions);
            return toolbar;
        }

        private void RebuildNavigation()
        {
            _navigation.Clear();
            _navigation.Add(CreateNavigationButton(
                "Library",
                AssetManagerPage.Library));
            _navigation.Add(CreateNavigationButton(
                "Archived",
                AssetManagerPage.Archived));
            _navigation.Add(CreateNavigationButton(
                "Unassigned files",
                AssetManagerPage.UnassignedFiles));
            _navigation.Add(CreateNavigationButton(
                "Sources & import",
                AssetManagerPage.Sources));

            _navigation.Add(UiTextFactory.Create(
                "COLLECTIONS",
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

            _navigation.Add(AssetManagerControls.CreateButton(
                "+ New collection",
                ShowNewCollection,
                "ee4v-asset-manager__nav-button",
                "ee4v-asset-manager__nav-create"));

            var spacer = new VisualElement();
            spacer.AddToClassList("ee4v-asset-manager__nav-spacer");
            _navigation.Add(spacer);
            _navigation.Add(AssetManagerControls.CreateButton(
                "New item",
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

                if (ShowsInformation)
                {
                    RefreshDetail();
                }
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private void BuildItems()
        {
            var items = GetVisibleItems();
            CancelGridThumbnails();
            _content.Clear();
            _title.SetText(GetPageTitle());
            _resultCount.SetText(items.Count + " items");
            _search.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.Flex;

            if (_viewState.Page == AssetManagerPage.Collection)
            {
                _content.Add(BuildCollectionHeader());
            }

            if (items.Count == 0)
            {
                _content.Add(AssetManagerControls.CreateNotice(
                    "No items match this view."));
            }

            _itemGrid.SetItems(items.Select(item =>
                    new AssetItemGridEntry(
                        item.Id,
                        item.Name))
                .ToArray());
            _itemGrid.SetSelectedItemId(_viewState.SelectedItemId);
            _content.Add(_itemGrid);
            LoadGridThumbnails(items);
        }

        private void SelectItem(string itemId)
        {
            _viewState.SelectItem(itemId);
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
                var thumbnails = await _manager.GetThumbnails(
                    items.Select(item => item.Id).ToArray(),
                    cancellation.Token);
                if (!ReferenceEquals(_gridThumbnailCancellation, cancellation))
                {
                    return;
                }

                _itemGrid.SetThumbnails(thumbnails
                    .Where(pair =>
                        pair.Value != null &&
                        pair.Value.Found &&
                        pair.Value.Data != null &&
                        pair.Value.Data.Length > 0)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.Data));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetError(exception);
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
            CancelThumbnail();
            DestroyThumbnail();
            if (item == null)
            {
                ShowEmptyDetail("The selected item no longer exists.");
                return;
            }

            _detail.Add(CreateDetailTitle(item.Name, "ITEM"));
            var thumbnailHost = new VisualElement();
            thumbnailHost.AddToClassList("ee4v-asset-manager__thumbnail");
            thumbnailHost.Add(UiTextFactory.Create("No thumbnail"));
            _detail.Add(thumbnailHost);

            var name = AssetManagerControls.CreateTextField("Name");
            name.value = item.Name;
            var description = AssetManagerControls.CreateTextField("Description");
            description.multiline = true;
            description.value = item.Description ?? string.Empty;
            var tags = AssetManagerControls.CreateTextField("Tags");
            tags.value = string.Join(", ", (item.Tags ?? Array.Empty<AssetTag>()).Select(tag => tag.Path));
            _detail.Add(name);
            _detail.Add(description);
            _detail.Add(tags);

            var actions = CreateActionRow();
            actions.Add(AssetManagerControls.CreateButton(
                "Save",
                () => SaveItem(item.Id, name.value, description.value, tags.value),
                "ee4v-asset-manager__primary-action"));
            actions.Add(AssetManagerControls.CreateButton(
                item.IsArchived ? "Restore" : "Archive",
                () => ArchiveItem(item.Id, !item.IsArchived)));
            actions.Add(AssetManagerControls.CreateButton(
                "Delete",
                () => DeleteItem(item.Id),
                "ee4v-asset-manager__danger-action"));
            _detail.Add(actions);

            _detail.Add(CreateSectionTitle("FILES"));
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
            _detail.Add(CreateSectionTitle("IMPORTED ASSET GUIDS"));
            _detail.Add(UiTextFactory.Create(
                string.Join("\n", GetItemGuids(item.Id).DefaultIfEmpty("None")),
                "ee4v-asset-manager__mono"));

            _thumbnailCancellation = new CancellationTokenSource();
            try
            {
                var thumbnail = await _manager.GetThumbnail(
                    item.Id,
                    _thumbnailCancellation.Token);
                if (thumbnail == null || !thumbnail.Found ||
                    thumbnail.Data == null || thumbnail.Data.Length == 0)
                {
                    return;
                }

                _thumbnailTexture = new Texture2D(2, 2);
                if (!_thumbnailTexture.LoadImage(thumbnail.Data))
                {
                    DestroyThumbnail();
                    return;
                }

                thumbnailHost.Clear();
                thumbnailHost.Add(new Image
                {
                    image = _thumbnailTexture,
                    scaleMode = ScaleMode.ScaleToFit
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private void RefreshDetail()
        {
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
                    ? "Select a file to assign it or edit import settings."
                    : "Select an item to inspect its files and metadata.");
        }

        private VisualElement BuildRegisterFile(string itemId)
        {
            var foldout = AssetManagerControls.CreateFoldout("Register file");
            var filePath = AssetManagerControls.CreateTextField("File path");
            var fileName = AssetManagerControls.CreateTextField("Display name");
            foldout.Add(filePath);
            foldout.Add(fileName);
            foldout.Add(AssetManagerControls.CreateButton(
                "Register",
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
                ShowEmptyDetail("The selected file no longer exists.");
                return;
            }

            _detail.Add(CreateDetailTitle(file.FileName, "FILE"));
            _detail.Add(CreateKeyValue("Source", file.SourceType + " · " + file.SourcePath));
            _detail.Add(CreateKeyValue("File ID", file.Id));

            var itemId = AssetManagerControls.CreateTextField("Assigned item ID");
            itemId.value = file.ItemId ?? string.Empty;
            _detail.Add(itemId);
            _detail.Add(AssetManagerControls.CreateButton(
                "Move file",
                () => MoveFile(file.Id, itemId.value)));

            var targets = AssetManagerControls.CreateTextField("Target paths (one per line)");
            targets.multiline = true;
            targets.value = string.Join("\n", GetTargets(file.Id).Select(target => target.TargetPath));
            _detail.Add(targets);
            _detail.Add(AssetManagerControls.CreateButton(
                "Save targets",
                () => SaveTargets(file.Id, targets.value)));

            var dependencies = AssetManagerControls.CreateTextField("Dependency file IDs (one per line)");
            dependencies.multiline = true;
            dependencies.value = string.Join(
                "\n",
                GetDependencies(file.Id).Select(dependency => dependency.DependencyFileId));
            _detail.Add(dependencies);
            _detail.Add(AssetManagerControls.CreateButton(
                "Save dependencies",
                () => SaveDependencies(file.Id, dependencies.value)));

            var entries = AssetManagerControls.CreateTextField("Archive entries (one per line)");
            entries.multiline = true;
            _detail.Add(entries);
            var importActions = CreateActionRow();
            importActions.Add(AssetManagerControls.CreateButton(
                "Analyze",
                () => AnalyzeFile(file.Id)));
            importActions.Add(AssetManagerControls.CreateButton(
                "Import entries",
                () => ImportEntries(file.Id, entries.value)));
            importActions.Add(AssetManagerControls.CreateButton(
                "Import targets",
                () => ImportTargets(file.Id),
                "ee4v-asset-manager__primary-action"));
            _detail.Add(importActions);

            _detail.Add(CreateSectionTitle("IMPORTED ASSET GUIDS"));
            _detail.Add(UiTextFactory.Create(
                string.Join("\n", GetFileGuids(file.Id).DefaultIfEmpty("None")),
                "ee4v-asset-manager__mono"));

            var actions = CreateActionRow();
            actions.Add(AssetManagerControls.CreateButton(
                file.IsArchived ? "Restore" : "Archive",
                () => ArchiveFile(file.Id, !file.IsArchived)));
            actions.Add(AssetManagerControls.CreateButton(
                "Delete",
                () => DeleteFile(file.Id),
                "ee4v-asset-manager__danger-action"));
            _detail.Add(actions);
        }

        private void SelectFile(AssetFile file)
        {
            _viewState.SelectFile(file?.Id);
        }

        private void BuildUnassignedFiles()
        {
            CancelGridThumbnails();
            _content.Clear();
            _title.SetText("Unassigned files");
            _search.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.None;
            var scroll = CreateContentScroll();
            _content.Add(scroll);
            var files = GetUnassignedFiles()
                .Where(file => MatchesSearch(file.FileName))
                .ToArray();
            _resultCount.SetText(files.Length + " files");
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                scroll.Add(AssetManagerControls.CreateButton(
                    file.FileName,
                    () => SelectFile(file),
                    "ee4v-asset-manager__list-row"));
            }

            if (files.Length == 0)
            {
                scroll.Add(AssetManagerControls.CreateNotice(
                    "There are no unassigned files."));
            }

        }

        private void BuildSources()
        {
            CancelGridThumbnails();
            _content.Clear();
            _title.SetText("Sources & import");
            _resultCount.SetText(string.Empty);
            _search.style.display = DisplayStyle.None;
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
                "Eagle sync",
                "Synchronize the Eagle library configured in Preferences/4OF/ee4v.");
            card.Add(AssetManagerControls.CreateButton(
                "Sync Eagle",
                SyncEagle,
                "ee4v-asset-manager__primary-action"));
            return card;
        }

        private VisualElement BuildEe4vSource()
        {
            var card = CreateSourceCard(
                "ee4v sync",
                "Synchronize the ee4v library configured in Preferences/4OF/ee4v.");
            card.Add(AssetManagerControls.CreateButton(
                "Sync ee4v",
                SyncEe4v,
                "ee4v-asset-manager__primary-action"));
            return card;
        }

        private VisualElement BuildEe4vImport()
        {
            var card = CreateSourceCard(
                "Import into ee4v",
                "Copy a file into the configured ee4v library and create its catalog item.");
            var file = AssetManagerControls.CreateTextField("File path");
            var name = AssetManagerControls.CreateTextField("Item name (optional)");
            var description = AssetManagerControls.CreateTextField("Description (optional)");
            var tags = AssetManagerControls.CreateTextField("Tags (comma separated)");
            card.Add(file);
            card.Add(name);
            card.Add(description);
            card.Add(tags);
            card.Add(AssetManagerControls.CreateButton(
                "Import file",
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
                "Imported asset lookup",
                "Find catalog files associated with Unity Asset GUIDs.");
            var guids = AssetManagerControls.CreateTextField("GUIDs (one per line, empty for all)");
            guids.multiline = true;
            var results = UiTextFactory.Create(
                "No lookup has been run.",
                "ee4v-asset-manager__mono");
            card.Add(guids);
            card.Add(AssetManagerControls.CreateButton(
                "Find associations",
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
                "Edit",
                () => _viewState.ShowCollectionEditor(
                    collection?.Id)));
            bar.Add(AssetManagerControls.CreateButton(
                "Delete",
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
            _detail.Add(CreateDetailTitle("New item", "CREATE"));
            var name = AssetManagerControls.CreateTextField("Name");
            var description = AssetManagerControls.CreateTextField("Description");
            description.multiline = true;
            _detail.Add(name);
            _detail.Add(description);
            _detail.Add(AssetManagerControls.CreateButton(
                "Create item",
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
                collection == null ? "New collection" : collection.Name,
                collection == null ? "CREATE" : "COLLECTION"));
            var name = AssetManagerControls.CreateTextField("Name");
            name.value = collection?.Name ?? string.Empty;
            var condition = AssetManagerControls.CreateEnumField(
                "Condition",
                GetConditionType(collection?.Root));
            var value = AssetManagerControls.CreateTextField("Value");
            value.value = GetConditionValue(collection?.Root);
            _detail.Add(name);
            _detail.Add(condition);
            _detail.Add(value);
            _detail.Add(AssetManagerControls.CreateNotice(
                "This editor creates one condition. Nested AND, OR and NOT filters remain supported by the backend API."));
            _detail.Add(AssetManagerControls.CreateButton(
                collection == null ? "Create collection" : "Save collection",
                () => SaveCollection(
                    collection?.Id,
                    name.value,
                    (AssetFilterConditionType)condition.value,
                    value.value),
                "ee4v-asset-manager__primary-action"));
        }

        private void CreateItem(string name, string description)
        {
            Run("Item created", () =>
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
            Run("Item saved", () =>
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
            Run(archived ? "Item archived" : "Item restored", () =>
                _manager.SetItemArchived(new[] { id }, archived));
        }

        private void DeleteItem(string id)
        {
            if (!Confirm("Delete item", "Delete this item and its files?"))
            {
                return;
            }

            Run("Item deleted", () =>
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
            Run("File registered", () => _manager.RegisterFile(
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
            Run("File moved", () =>
                _manager.SetFileItem(
                    new[] { fileId },
                    string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim()));
        }

        private void ArchiveFile(string id, bool archived)
        {
            Run(archived ? "File archived" : "File restored", () =>
                _manager.SetFileArchived(new[] { id }, archived));
        }

        private void DeleteFile(string id)
        {
            if (!Confirm("Delete file", "Delete this file record?"))
            {
                return;
            }

            Run("File deleted", () =>
            {
                _manager.DeleteFile(new[] { id });
                _viewState.SelectFile(null);
            });
        }

        private void SaveTargets(string fileId, string paths)
        {
            Run("Targets saved", () =>
                _manager.SetFileTargets(fileId, SplitLines(paths)));
        }

        private void SaveDependencies(string fileId, string dependencyIds)
        {
            Run("Dependencies saved", () =>
                _manager.SetFileDependencies(
                    new[] { fileId },
                    SplitLines(dependencyIds)));
        }

        private void AnalyzeFile(string fileId)
        {
            Run("Archive analyzed", () =>
            {
                var analysis = _manager.AnalyzeFile(fileId);
                SetStatus(
                    analysis.Kind + ": " + analysis.Entries.Count + " entries");
            }, refresh: false);
        }

        private async void ImportEntries(string fileId, string paths)
        {
            await RunImport(
                "Entries imported",
                () => _manager.ImportFileEntries(fileId, SplitLines(paths)));
        }

        private async void ImportTargets(string fileId)
        {
            await RunImport(
                "Targets imported",
                () => _manager.ImportFileTargets(fileId));
        }

        private void SaveCollection(
            string id,
            string name,
            AssetFilterConditionType condition,
            string value)
        {
            Run(string.IsNullOrEmpty(id) ? "Collection created" : "Collection saved", () =>
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
                !Confirm("Delete collection", "Delete this collection?"))
            {
                return;
            }

            Run("Collection deleted", () =>
            {
                _manager.DeleteCollection(id);
                _viewState.SelectPage(AssetManagerPage.Library);
            });
        }

        private void SyncEagle()
        {
            RunSync("Eagle", () =>
                _manager.SyncEagle(new EagleSyncRequest(
                    EmptyToNull(AssetManagerSettings.EagleLibraryPath),
                    EmptyToNull(AssetManagerSettings.EagleTargetRoot))));
        }

        private void SyncEe4v()
        {
            RunSync("ee4v", () =>
                _manager.SyncEe4v(new Ee4vSyncRequest(
                    AssetManagerSettings.Ee4vLibraryPath)));
        }

        private void ImportEe4vFile(
            string filePath,
            string name,
            string description,
            string tags)
        {
            Run("File imported into ee4v", () =>
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
                SetStatus(associations.Count + " associations found");
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private void RunSync(string source, Func<AssetSyncResult> operation)
        {
            Run(source + " synchronized", () =>
            {
                var result = operation();
                SetStatus(
                    source + " " + result.State + " · " +
                    result.CreatedCount + " created · " +
                    result.UpdatedCount + " updated · " +
                    result.DeletedCount + " deleted · " +
                    result.ErrorCount + " errors");
            }, refresh: false);
        }

        private async System.Threading.Tasks.Task RunImport(
            string successMessage,
            Func<System.Threading.Tasks.Task<AssetImportResult>> operation)
        {
            try
            {
                SetStatus("Importing…");
                var result = await operation();
                SetStatus(result.Succeeded
                    ? successMessage + " · " + result.AssetGuids.Count + " assets"
                    : result.State + " · " + result.ErrorMessage);
                Refresh();
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private void Run(string successMessage, Action operation, bool refresh = true)
        {
            try
            {
                operation();
                SetStatus(successMessage);
                if (refresh)
                {
                    if (ShowsNavigation)
                    {
                        RebuildNavigation();
                    }
                    Refresh();
                }
            }
            catch (Exception exception)
            {
                SetError(exception);
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
                    Filter = BuildSearchFilter(_search.value)
                };
                items = _manager.SearchItems(query).Items;
            }

            return items
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Archived ||
                    item.IsArchived)
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Collection ||
                    MatchesSearch(item.Name) ||
                    MatchesSearch(item.Description))
                .ToArray();
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
                    return "Archived";
                case AssetManagerPage.Collection:
                    return GetCollections()
                        .FirstOrDefault(collection =>
                            collection.Id == _viewState.CollectionId)
                        ?.Name ?? "Collection";
                default:
                    return "Library";
            }
        }

        private bool MatchesSearch(string value)
        {
            return string.IsNullOrWhiteSpace(_search.value) ||
                (!string.IsNullOrEmpty(value) &&
                 value.IndexOf(_search.value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static AssetFilterNode BuildSearchFilter(string search)
        {
            return string.IsNullOrWhiteSpace(search)
                ? null
                : AssetFilterNode.Or(
                    AssetFilterNode.Condition(
                        AssetFilterConditionType.NameContains,
                        search.Trim()),
                    AssetFilterNode.Condition(
                        AssetFilterConditionType.DescriptionContains,
                        search.Trim()));
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
            SetStatus(change.Kind + " · catalog refreshed");
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
                        _itemGrid.SetSelectedItemId(
                            _viewState.SelectedItemId);
                    }
                    if (ShowsInformation)
                    {
                        RefreshDetail();
                    }
                    break;
                case AssetManagerViewStateChange.FileSelection:
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
            }
        }

        private void SetStatus(string text)
        {
            _status.SetText(text);
            _status.EnableInClassList(
                "ee4v-asset-manager__status--error",
                false);
        }

        private void SetError(Exception exception)
        {
            _status.SetText(exception.Message);
            _status.EnableInClassList(
                "ee4v-asset-manager__status--error",
                true);
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

        private void DestroyThumbnail()
        {
            if (_thumbnailTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_thumbnailTexture);
                _thumbnailTexture = null;
            }
        }

        private static bool Confirm(string title, string message)
        {
            return EditorUtility.DisplayDialog(title, message, "Delete", "Cancel");
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
