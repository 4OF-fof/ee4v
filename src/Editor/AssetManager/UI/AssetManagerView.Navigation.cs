using System;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
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
            }
            AddSortMenuItem(
                menu,
                GetSortLabel(AssetManagerItemSortField.UpdatedAt),
                AssetManagerItemSortField.UpdatedAt);
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
                return AssetManagerItemSort.GetVariantSortField(
                    _viewState.ItemSortField);
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
                RegisterCollectionDrag(button, collection);
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
            RegisterCollectionReordering(collectionSection, collections);
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

    }
}
