using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
        private void ShowItemDetail(AssetItem item)
        {
            _detail.Clear();
            if (item == null)
            {
                ShowEmptyDetail(I18N.Get("notice.selectedItemMissing"));
                return;
            }

            var standalone = FindStandaloneAsset(item.Id);
            if (standalone != null)
            {
                ShowVariantDetail(standalone.VariantId);
                return;
            }

            var thumbnailStack = CreateDetailThumbnailStack(
                new[] { item.Id });
            _detail.Add(thumbnailStack);

            var name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"),
                "ee4v-asset-manager__item-metadata-field");
            name.Input.Value = item.Name;
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
            var tags = new TagField(
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
            name.Input.IsReadOnly = !canEditMetadata;
            description.IsReadOnly = !canEditMetadata;
            if (canEditMetadata)
            {
                Action save = () => SaveItemMetadataAutomatically(
                    item.Id,
                    name.Input.Value,
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

            if (_viewState.Page == AssetManagerPage.Archived)
            {
                ShowArchiveSelectionDetail();
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

            ShowLibraryDetail();
        }

        private void ShowLibraryDetail()
        {
            _detail.Clear();
            var items = _manager.SearchItems(new AssetItemQuery
            {
                IncludeArchived = true
            }).Items;
            var unassignedFiles = _manager.GetUnassignedFiles(includeArchived: true);
            var fileCount = items.Sum(item => item.Files?.Count ?? 0) + unassignedFiles.Count;
            var information = new DetailSection(I18N.Get("library.title"));
            information.AddToClassList("ee4v-asset-manager__library-information");
            var datasource = AssetManagerSettings.SelectedDatasource;
            var sourceName = datasource == AssetDatasourceKind.BoothLibraryManager
                ? "BOOTH Library Manager"
                : datasource == AssetDatasourceKind.Ee4v ? "ee4v" : "Eagle";
            var source = new VisualElement();
            source.AddToClassList("ee4v-asset-manager__library-source");
            AddLibraryIcon(source, "library.png");
            var sourceText = UiTextFactory.Create(
                I18N.Get("library.sourceSummary", sourceName),
                UiClassNames.FormLabel,
                "ee4v-asset-manager__library-source-text");
            sourceText.SetColor(UiColorTokens.TextMuted);
            sourceText.SetWhiteSpace(WhiteSpace.Normal);
            source.Add(sourceText);
            information.Add(source);

            var metrics = new VisualElement();
            metrics.AddToClassList("ee4v-asset-manager__library-metrics");
            metrics.Add(CreateLibraryMetric("library.itemCount", items.Count));
            var filesMetric = CreateLibraryMetric("library.fileCount", fileCount);
            filesMetric.AddToClassList("ee4v-asset-manager__library-metric--separated");
            metrics.Add(filesMetric);
            information.Add(metrics);

            var myAssets = new VisualElement();
            myAssets.AddToClassList("ee4v-asset-manager__library-my-assets");
            AddLibraryIcon(myAssets, "cube.png");
            myAssets.Add(UiTextFactory.Create(
                I18N.Get("library.variantCount"),
                "ee4v-asset-manager__library-my-assets-label"));
            var variantCount = UiTextFactory.Create(
                GetVariants().Count.ToString("N0"),
                "ee4v-asset-manager__library-my-assets-value");
            variantCount.SetFontSize(20);
            myAssets.Add(variantCount);
            information.Add(myAssets);

            var organization = new VisualElement();
            organization.AddToClassList("ee4v-asset-manager__library-organization");
            organization.Add(new SectionHeader(I18N.Get("library.organization")));
            AddLibraryValue(organization, "library.collectionCount", _manager.GetCollections().Count);
            AddLibraryValue(organization, "library.tagCount", _manager.GetTags().Count);
            AddLibraryValue(organization, "library.unassignedFileCount", unassignedFiles.Count);
            information.Add(organization);
            var summary = new VisualElement();
            summary.AddToClassList("ee4v-asset-manager__library-summary");
            summary.Add(new SectionHeader(I18N.Get("library.summary")));
            AddLibraryValue(summary, "library.archivedItemCount", items.Count(item => item.IsArchived));
            AddLibraryValue(summary, "library.latestItemUpdate",
                FormatTimestamp(items.Select(item => item.UpdatedAt).DefaultIfEmpty().Max()));
            information.Add(summary);
            _detail.Add(information);
        }

        private static VisualElement CreateLibraryMetric(string labelKey, int count)
        {
            var metric = new VisualElement();
            metric.AddToClassList("ee4v-asset-manager__library-metric");
            var value = UiTextFactory.Create(
                count.ToString("N0"),
                "ee4v-asset-manager__library-metric-value");
            value.SetFontSize(36);
            metric.Add(value);
            var label = UiTextFactory.Create(
                I18N.Get(labelKey),
                UiClassNames.FormLabel,
                "ee4v-asset-manager__library-metric-label");
            label.SetColor(UiColorTokens.TextMuted);
            metric.Add(label);
            return metric;
        }

        private static void AddLibraryValue(VisualElement information, string labelKey, int count)
        {
            AddLibraryValue(information, labelKey, count.ToString("N0"));
        }

        private static void AddLibraryValue(VisualElement information, string labelKey, string text)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-asset-manager__library-row");
            var label = UiTextFactory.Create(
                I18N.Get(labelKey),
                UiClassNames.FormLabel,
                "ee4v-asset-manager__library-row-label");
            label.SetColor(UiColorTokens.TextMuted);
            label.SetWhiteSpace(WhiteSpace.Normal);
            row.Add(label);
            var value = UiTextFactory.Create(text,
                "ee4v-asset-manager__library-row-value");
            value.SetTextAlign(TextAnchor.MiddleRight);
            value.SetWhiteSpace(WhiteSpace.Normal);
            row.Add(value);
            information.Add(row);
        }

        private static void AddLibraryIcon(VisualElement row, string iconFileName)
        {
            var icon = AssetManagerControls.CreateIcon(
                iconFileName, UiSizeTokens.Size16, UiColorTokens.TextMuted);
            if (icon != null)
            {
                icon.AddToClassList("ee4v-asset-manager__library-icon");
                row.Add(icon);
            }
        }

        private void ShowArchiveSelectionDetail()
        {
            var selected = _viewState.SelectedArchiveEntryIds;
            if (selected.Count == 0)
            {
                ShowLibraryDetail();
                return;
            }
            if (selected.Count == 1)
            {
                if (TryGetArchiveVariantId(selected[0], out var variantId)) { ShowVariantDetail(variantId); }
                else { ShowItemDetail(_manager.GetItem(selected[0])); }
                return;
            }
            if (!selected.Any(id => TryGetArchiveVariantId(id, out _)))
            {
                ShowItemSelectionDetail(selected.Select(_manager.GetItem).Where(item => item != null).ToArray(),
                    _viewState.SelectedArchiveEntryId);
                return;
            }
            _detail.Clear();
            _detail.Add(UiTextFactory.Create(I18N.Get("detail.item.selectedCount", selected.Count),
                UiClassNames.SelectionCount));
            var variants = GetVariants().ToDictionary(variant => variant.VariantId, StringComparer.Ordinal);
            foreach (var id in selected)
            {
                if (TryGetArchiveVariantId(id, out var variantId))
                {
                    if (variants.TryGetValue(variantId, out var variant))
                    {
                        _detail.Add(new InfoCard(new InfoCardState(variant.Name, variant.Description)));
                    }
                }
                else
                {
                    var item = _manager.GetItem(id);
                    if (item != null) { _detail.Add(new InfoCard(new InfoCardState(item.Name, item.Description))); }
                }
            }
        }

        private void ShowItemSelectionDetail(
            IReadOnlyList<AssetItem> items, string primaryItemId = null)
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
                    primaryItemId ?? _viewState.SelectedItemId,
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
                _content.Add(AssetManagerControls.CreateEmptyState(
                    I18N.Get("notice.itemMissing"), "library.png"));
                return;
            }

            if (_viewState.IsDerivedAssetsPage)
            {
                BuildDerivedAssetsPage();
                return;
            }

            var standalone = FindStandaloneAsset(item.Id);
            if (standalone != null)
            {
                BuildVariantDetailPage(standalone);
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

        private void StartOriginalAssetCreation()
        {
            if (_createOriginalAsset != null)
            {
                _createOriginalAsset();
                return;
            }
            _originalAssetName = string.Empty;
            _originalAssetDescription = string.Empty;
            _originalAssetPrefab = null;
            _creatingOriginalAsset = true;
            RefreshMain();
        }

        private void BuildOriginalAssetCreationPage()
        {
            CancelGridThumbnails();
            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            _content.Clear();
            _search.style.display = DisplayStyle.None;
            _sortButton.style.display = DisplayStyle.None;
            _gridControls.style.display = DisplayStyle.None;
            var sources = AssetPrefabPickerWindow.FindSources(_manager);
            var prefabs = sources.SelectMany(source => source.Prefabs).Distinct().ToArray();
            var form = new VariantCreationView(null, prefabs, _originalAssetName, _originalAssetDescription,
                _originalAssetPrefab, I18N.Get("workflow.original.create"), () =>
                {
                    _creatingOriginalAsset = false;
                    RefreshMain();
                }, original: true, showPrefabPicker: (anchor, selected, select) =>
                    AssetPrefabPickerWindow.Show(anchor, sources, _manager, select, null, selectPrefab: true));
            _originalAssetPrefab = form.Prefab.Value;
            form.Prefab.ValueChanged += value => _originalAssetPrefab = value;
            form.Name.ValueChanged += value => _originalAssetName = value;
            form.Description.ValueChanged += value => _originalAssetDescription = value;
            form.Create.clicked += () => CreateDerivedAsset(form.Name.Value, form.Prefab.Value, form.Description.Value,
                form.Feedback, form.Create, original: true);
            _content.Add(form);
        }

        private void BuildDerivedAssetsPage()
        {
            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            _itemDetailPane = null;
            _fileTreeSelections = Array.Empty<FileTreeSelection>();
            var item = _manager.GetItem(_viewState.DetailItemId);
            var prefabCandidates = DerivedAssetCreator
                .FindPrefabCandidates(_manager, item);
            var form = new VariantCreationView(
                item,
                prefabCandidates,
                (item?.Name ?? string.Empty) + " Variant",
                string.Empty,
                null,
                I18N.Get("action.createDerivedAsset"),
                _viewState.CloseDerivedAssetsPage);
            form.Create.clicked += () => CreateDerivedAsset(
                form.Name.Value, form.Prefab.Value, form.Description.Value,
                form.Feedback, form.Create);
            _content.Add(form);
        }

        private void CreateDerivedAsset(
            string name,
            GameObject prefab,
            string description,
            UiTextElement message,
            UiButton createButton,
            bool original = false)
        {
            if (!DerivedAssetCreator.IsValidName(name))
            {
                SetDerivedAssetFeedback(
                    message,
                    I18N.Get("notice.derivedAssetNameInvalid"));
                return;
            }

            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (original && prefab != null && !AssetPrefabPickerWindow.FindSources(_manager)
                .SelectMany(source => source.Prefabs).Contains(prefab))
            {
                SetDerivedAssetFeedback(message, I18N.Get("notice.derivedAssetPrefabInvalid"));
                return;
            }
            if (!original && (string.IsNullOrEmpty(prefabPath) ||
                !prefabPath.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase) ||
                !DerivedAssetCreator.FindPrefabCandidates(
                    _manager, _manager.GetItem(_viewState.DetailItemId)).Contains(prefab)))
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

            createButton.SetPrimaryActionEnabled(false);
            var wasDeferringRefresh = _defersManagerRefresh;
            _defersManagerRefresh = true;
            AssetItem owner = null;
            var created = false;
            try
            {
                if (original)
                {
                    owner = _manager.CreateItem(new CreateAssetItemRequest
                    {
                        Name = name.Trim(), Description = description, IsOriginal = true
                    });
                }
                var result = DerivedAssetCreator.Create(
                    new DerivedAssetCreationRequest
                    {
                        ParentItemId = owner?.Id ?? _viewState.DetailItemId,
                        Name = name,
                        Prefab = prefab,
                        Description = description,
                        CreateOriginal = original && prefab == null
                    });
                created = true;
                Selection.activeObject = result.Prefab;
                EditorGUIUtility.PingObject(result.Prefab);
                if (original)
                {
                    _creatingOriginalAsset = false;
                    OpenVariantDetail(result.VariantId);
                }
                else { _viewState.CloseDerivedAssetsPage(); }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!created && owner != null)
                {
                    try
                    {
                        _manager.SetItemArchived(new[] { owner.Id }, true);
                        _manager.DeleteItem(new[] { owner.Id });
                    }
                    catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                }
                SetDerivedAssetFeedback(
                    message,
                    I18N.Get("notice.derivedAssetCreateFailed"));
            }
            finally
            {
                _defersManagerRefresh = wasDeferringRefresh;
                if (!wasDeferringRefresh) { _managerRefreshPending = false; }
                createButton.SetPrimaryActionEnabled(true);
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
            var thumbnail = new ThumbnailStack(
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
            var tagScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            tagScroll.AddToClassList("ee4v-asset-manager__overview-tag-scroll");
            var tags = new VisualElement();
            tags.AddToClassList("ee4v-asset-manager__overview-tags");
            tags.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                tagScroll.style.height = Mathf.Min(evt.newRect.height, tagScroll.resolvedStyle.maxHeight.value);
                tagScroll.nestedInteractionKind = evt.newRect.height > tagScroll.resolvedStyle.maxHeight.value
                    ? ScrollView.NestedInteractionKind.StopScrolling
                    : ScrollView.NestedInteractionKind.Default;
            });
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
            tagScroll.Add(tags);
            summary.Body.Add(tagScroll);
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
            facts.Add(new MetricDisplay(
                I18N.Get("field.fileCount"),
                files.Count.ToString()));
            facts.Add(new MetricDisplay(
                I18N.Get("field.fileTypes"),
                GetFileTypes(files)));
            var sourceFact = new MetricDisplay(
                I18N.Get("field.source"),
                GetItemSources(item, files));
            sourceFact.AddToClassList(
                "ee4v-asset-manager__overview-fact--last");
            facts.Add(sourceFact);
            detail.Add(facts);

            var importSettings = new DetailSection(
                I18N.Get("detail.importSettings"));
            var targetTree = CreateTargetList(item.Id, files);
            UiButton editTargets = null;
            editTargets = AssetManagerControls.CreateButton(
                I18N.Get("action.edit"),
                () => ShowItemTargetEditor(
                    editTargets,
                    item.Id,
                    files),
                "ee4v-ui-details__inline-action");
            var targetSetting = new SettingRow(
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
            var targetList = new SettingList();
            targetList.AddRow(targetSetting);
            importSettings.Add(targetList);
            detail.Add(importSettings);

            AddDerivedAssets(detail, item.Id);
            AddItemInformation(detail, item, files);
        }

        private void AddDerivedAssets(
            VisualElement detail,
            string itemId)
        {
            var section = new DetailSection(
                I18N.Get("detail.derivedAssets"));
            var grid = new VisualElement();
            grid.AddToClassList(
                "ee4v-asset-manager__derived-assets-grid");
            foreach (var derivedAsset in
                     GetVariants()
                         .Where(variant => !variant.IsArchived && string.Equals(
                             variant.ParentItemId, itemId, StringComparison.Ordinal))
                         .OrderBy(variant => variant.Name, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(variant => variant.VariantId, StringComparer.Ordinal))
            {
                var assetCard = new ImageGridCard(_imageCache);
                assetCard.SetWidth(DerivedAssetCardWidth);
                assetCard.SetPlaceholderIcon(
                    AssetManagerControls.LoadFluentIconState(
                        "cube.png",
                        UiSizeTokens.Size24,
                        derivedAsset.Name,
                        UiColorTokens.TextMuted));
                assetCard.SetState(
                    new ImageGridEntry(
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
                    OpenVariantDetail(derivedAsset.VariantId);
                };
                assetCard.ContextClicked += (_, __) => ShowVariantContextMenu(new[] { derivedAsset.VariantId });
                assetCard.RegisterCallback<DetachFromPanelEvent>(_ =>
                    assetCard.Dispose());
                grid.Add(assetCard);
                _ = LoadDerivedVariantThumbnailAsync(assetCard, derivedAsset);
            }
            var addCard = new ImageGridCard(_imageCache);
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
                new ImageGridEntry(
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
            var information = new DetailSection(
                I18N.Get("detail.information"));
            AddBoothInformation(information, item.Booth);
            information.Add(new KeyValueRow(
                I18N.Get("field.totalFileSize"),
                FormatTotalFileSize(files)));
            information.Add(new KeyValueRow(
                I18N.Get("field.createdAt"),
                FormatTimestamp(item.CreatedAt)));
            information.Add(new KeyValueRow(
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
                information.Add(new KeyValueRow(
                    I18N.Get("field.boothStore"),
                    CreateDetailLink(booth.ShopName, shopUrl)));
            }
            if (TryGetWebUrl(booth.ItemUrl, out var itemUrl))
            {
                information.Add(new KeyValueRow(
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

    }
}
