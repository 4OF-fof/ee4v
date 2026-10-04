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
            var prefabPreview = new PrefabScenePreview();
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
            var prefab = new PrefabSelector(
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

    }
}
