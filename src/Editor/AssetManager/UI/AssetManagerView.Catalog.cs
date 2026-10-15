using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private void RefreshMain()
        {
            var hasDetail = !string.IsNullOrEmpty(_viewState.DetailItemId) ||
                !string.IsNullOrEmpty(_viewState.DetailVariantId);
            if (hasDetail && (_viewState.Page == AssetManagerPage.Archived ||
                FindVariantItem(_viewState.DetailItemId)?.IsArchived == true ||
                (!string.IsNullOrEmpty(_viewState.DetailVariantId) && GetVariants().Any(variant =>
                    variant.VariantId == _viewState.DetailVariantId && variant.IsArchived))))
            {
                _viewState.ShowPageRoot(rememberCurrentLocation: false);
                return;
            }
            _variantState.VariantGalleryView = null;
            var supportsFilters = !ShowsVariants && !ShowsFileList &&
                string.IsNullOrEmpty(_viewState.DetailItemId) &&
                string.IsNullOrEmpty(_viewState.DetailVariantId) &&
                _viewState.Page != AssetManagerPage.Tags;
            _filterButton.style.display = supportsFilters ? DisplayStyle.Flex : DisplayStyle.None;
            var hasFilter = _viewState.SearchFilter != null;
            _filterButton.EnableInClassList("ee4v-asset-manager__filter--active", hasFilter);
            _filterButton.tooltip = I18N.Get(hasFilter ? "searchFilter.active" : "searchFilter.open");
            _filterButton.SetIcon(AssetManagerControls.LoadFluentIconState("filter.png", UiSizeTokens.Size16,
                _filterButton.tooltip, hasFilter ? UiColorTokens.Focus : UiColorTokens.TextPrimary));
            HideVariantRevisionTooltip();
            Cancel(ref _variantState.VariantPreviewCancellation);
            _variantState.VariantPreviewCancellation = new CancellationTokenSource();
            _search.SetPlaceholder(I18N.Get(ShowsVariants
                ? "variant.searchPlaceholder" : "toolbar.search.placeholder"));
            _search.tooltip = I18N.Get(ShowsVariants
                ? "variant.searchPlaceholder" : "toolbar.search.tooltip");
            _reloadButton.tooltip = I18N.Get(ShowsVariants
                ? "variant.reload" : "toolbar.reload");
            _toolbar.style.display = _viewState.IsDerivedAssetsPage || _creatingOriginalAsset
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
            if (_creatingOriginalAsset)
            {
                BuildOriginalAssetCreationPage();
                return;
            }
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
                case AssetManagerPage.Archived:
                    BuildArchived();
                    break;
                case AssetManagerPage.Tags:
                    BuildTags();
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
                if (_viewState.Page == AssetManagerPage.Archived) { _variantManager?.RebuildIndex(); }
                if (_manager is IAssetDatasourceManager)
                {
                    AssetManagerWindowSession.SyncSelectedDatasource(_manager);
                }
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

            _itemGrid.SetItems(items.Select(item =>
                    new ImageGridEntry(
                        item.Id,
                        item.Name))
                .ToArray());
            _itemGrid.SetSelectedItemIds(
                _viewState.SelectedItemIds,
                _viewState.SelectedItemId);
            if (items.Count == 0)
            {
                _content.Add(AssetManagerControls.CreateEmptyState(
                    I18N.Get("notice.noItems")));
                return;
            }
            _content.Add(_itemGrid);
            OnGridVisibleItemsChanged(_itemGrid.GetVisibleItemIds());
        }

        private const string ArchiveVariantPrefix = "variant:";

        private static bool TryGetArchiveVariantId(string entryId, out string variantId)
        {
            variantId = entryId != null && entryId.StartsWith(ArchiveVariantPrefix, StringComparison.Ordinal)
                ? entryId.Substring(ArchiveVariantPrefix.Length) : null;
            return !string.IsNullOrEmpty(variantId);
        }

        private void BuildArchived()
        {
            CancelGridThumbnails();
            _content.Clear();
            _search.style.display = DisplayStyle.Flex;
            _sortButton.style.display = DisplayStyle.Flex;
            _gridControls.style.display = DisplayStyle.Flex;
            var icon = AssetManagerControls.LoadFluentIconState(
                "cube.png", UiSizeTokens.Size24,
                I18N.Get("navigation.variants"), UiColorTokens.TextMuted);
            var entries = GetVisibleItems().Select(item => new
                {
                    Grid = new ImageGridEntry(item.Id, item.Name),
                    item.CreatedAt, item.UpdatedAt, FileCount = item.Files?.Count ?? 0
                })
                .Concat(GetVariants().Where(variant =>
                    _viewState.SearchFilter == null &&
                    !variant.IsOwnedWork &&
                    variant.IsArchived && AssetManagerSearch.MatchesVariant(
                    variant, _search.Value, _viewState.SearchTargets)).Select(variant => new
                {
                    Grid = new ImageGridEntry(ArchiveVariantPrefix + variant.VariantId, variant.Name)
                    {
                        PlaceholderIcon = icon
                    },
                    CreatedAt = DateTime.MinValue, variant.UpdatedAt, FileCount = 0
                }));
            var reverse = _viewState.IsItemSortReversed;
            var ordered = reverse
                ? entries.OrderByDescending(entry => entry.Grid.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Grid.Name, StringComparer.OrdinalIgnoreCase);
            switch (_viewState.ItemSortField)
            {
                case AssetManagerItemSortField.CreatedAt:
                    ordered = reverse ? entries.OrderByDescending(entry => entry.CreatedAt)
                        : entries.OrderBy(entry => entry.CreatedAt);
                    break;
                case AssetManagerItemSortField.UpdatedAt:
                    ordered = reverse ? entries.OrderByDescending(entry => entry.UpdatedAt)
                        : entries.OrderBy(entry => entry.UpdatedAt);
                    break;
                case AssetManagerItemSortField.FileCount:
                    ordered = reverse ? entries.OrderByDescending(entry => entry.FileCount)
                        : entries.OrderBy(entry => entry.FileCount);
                    break;
            }
            ordered = reverse
                ? ordered.ThenByDescending(entry => entry.Grid.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(entry => entry.Grid.Id, StringComparer.Ordinal)
                : ordered.ThenBy(entry => entry.Grid.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Grid.Id, StringComparer.Ordinal);
            var gridEntries = ordered.Select(entry => entry.Grid).ToArray();
            _itemGrid.SetItems(gridEntries);
            _itemGrid.SetSelectedItemIds(_viewState.SelectedArchiveEntryIds, _viewState.SelectedArchiveEntryId);
            if (gridEntries.Length == 0)
            {
                _content.Add(AssetManagerControls.CreateEmptyState(
                    I18N.Get("notice.noItems")));
                return;
            }
            _content.Add(_itemGrid);
            OnGridVisibleItemsChanged(_itemGrid.GetVisibleItemIds());
        }

        private void ShowArchiveContextMenu(IReadOnlyList<string> entryIds)
        {
            var variantIds = entryIds.Where(id => TryGetArchiveVariantId(id, out _))
                .Select(id => id.Substring(ArchiveVariantPrefix.Length)).ToArray();
            var itemIds = entryIds.Where(id => !TryGetArchiveVariantId(id, out _)).ToArray();
            var menu = new GenericMenu();
            var restore = UiTextFactory.CreateGuiContent(I18N.Get("action.restore"));
            var delete = UiTextFactory.CreateGuiContent(I18N.Get("action.delete"));
            if (_variantManager == null || _variantState.VariantBusy)
            {
                menu.AddDisabledItem(restore);
                menu.AddDisabledItem(delete);
            }
            else
            {
                menu.AddItem(restore, false, () => ChangeVariantLifecycle(variantIds, false, itemIds));
                menu.AddSeparator(string.Empty);
                menu.AddItem(delete, false, () => ChangeVariantLifecycle(variantIds, null, itemIds));
            }
            menu.ShowAsContext();
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
            var variants = AssetManagerItemSort.Apply(
                GetVariants().Where(variant => !variant.IsArchived && AssetManagerSearch.MatchesVariant(
                    variant, _search.Value, _viewState.SearchTargets)),
                _viewState.ItemSortField,
                _viewState.IsItemSortReversed);
            if (variants.Count == 0 && !string.IsNullOrWhiteSpace(_search.Value))
            {
                _itemGrid.SetItems(Array.Empty<ImageGridEntry>());
                _content.Add(AssetManagerControls.CreateEmptyState(
                    I18N.Get("notice.noVariants"), "cube.png"));
                return;
            }
            var icon = AssetManagerControls.LoadFluentIconState(
                "cube.png", UiSizeTokens.Size24,
                I18N.Get("navigation.variants"), UiColorTokens.TextMuted);
            var createCard = new ImageGridEntry("create-original-work", I18N.Get("workflow.original.createCard"))
            {
                PlaceholderIcon = AssetManagerControls.LoadFluentIconState(
                    "add.png", UiSizeTokens.Size24, I18N.Get("workflow.original.create"), UiColorTokens.TextMuted),
                Activate = StartOriginalAssetCreation
            };
            _itemGrid.SetItems(new[] { createCard }.Concat(variants.Select(variant => new ImageGridEntry(
                variant.VariantId, variant.IsArchived ? variant.Name + " (" + I18N.Get("detail.item.archived") + ")" : variant.Name)
            {
                PlaceholderIcon = icon
            })).ToArray());
            _itemGrid.SetSelectedItemIds(
                _viewState.SelectedVariantIds, _viewState.SelectedVariantId);
            _content.Add(_itemGrid);
            OnGridVisibleItemsChanged(_itemGrid.GetVisibleItemIds());
        }

        private void SelectItems(
            IReadOnlyList<string> itemIds,
            string primaryItemId)
        {
            if (_viewState.Page == AssetManagerPage.Archived)
            {
                _viewState.SelectArchiveEntries(itemIds, primaryItemId);
                return;
            }
            if (ShowsVariants)
            {
                _viewState.SelectVariants(itemIds, primaryItemId);
                return;
            }
            _viewState.SelectItems(itemIds, primaryItemId);
        }

        private void OpenItemDetail(string itemId)
        {
            if (_viewState.Page == AssetManagerPage.Archived) { return; }
            if (ShowsVariants)
            {
                OpenVariantDetail(itemId);
                return;
            }
            OpenAssetItemDetail(itemId);
        }

        private void OpenAssetItemDetail(string itemId)
        {
            var item = _manager.GetItem(itemId);
            if (item == null || item.IsArchived)
            {
                return;
            }

            _viewState.OpenItemDetail(itemId);
        }

        private void OpenVariantDetail(string variantId)
        {
            var variant = GetVariants().FirstOrDefault(candidate => candidate.VariantId == variantId);
            if (variant == null || variant.IsArchived) { return; }
            _viewState.OpenVariantDetail(variantId);
        }

        private void ShowItemContextMenu(
            IReadOnlyList<string> itemIds,
            VisualElement anchor)
        {
            if (_viewState.Page == AssetManagerPage.Archived &&
                itemIds.Any(id => TryGetArchiveVariantId(id, out _)))
            {
                ShowArchiveContextMenu(itemIds);
                return;
            }
            if (ShowsVariants)
            {
                ShowVariantContextMenu(itemIds);
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
                ImageGridView.MaximumItemsPerRow);
            _gridSizeSlider.SetValueWithoutNotify(
                _itemGrid.ItemsPerRow);
        }

    }
}
