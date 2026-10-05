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
            _variantState.VariantGalleryView = null;
            HideVariantRevisionTooltip();
            Cancel(ref _variantState.VariantPreviewCancellation);
            _variantState.VariantPreviewCancellation = new CancellationTokenSource();
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
            var variants = AssetManagerItemSort.Apply(
                GetVariants().Where(variant => AssetManagerSearch.MatchesVariant(
                    variant, _search.Value, _viewState.SearchTargets)),
                _viewState.ItemSortField,
                _viewState.IsItemSortReversed);
            if (variants.Count == 0)
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

    }
}
