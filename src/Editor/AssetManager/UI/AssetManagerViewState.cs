using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.AssetManager.UI
{
    internal enum AssetManagerViewMode
    {
        Navigation,
        Main,
        Information
    }

    internal enum AssetManagerPage
    {
        Library,
        Imported,
        Archived,
        Tags,
        Collection,
        UnassignedFiles,
        Variants
    }

    internal enum AssetManagerViewStateChange
    {
        Navigation,
        ItemSelection,
        FileSelection,
        ItemDetailPage,
        ItemSort,
        SearchTargets,
        VariantSelection
    }

    internal sealed class AssetManagerViewState
    {
        private readonly Stack<NavigationLocation> _backHistory =
            new Stack<NavigationLocation>();
        private readonly Stack<NavigationLocation> _forwardHistory =
            new Stack<NavigationLocation>();
        private IReadOnlyList<string> _selectedItemIds =
            Array.Empty<string>();
        private IReadOnlyList<string> _selectedVariantIds =
            Array.Empty<string>();
        private NavigationLocation _location =
            new NavigationLocation(AssetManagerPage.Library, null, null);

        public AssetManagerPage Page { get; private set; }
        public string CollectionId { get; private set; }
        public string TagPath { get; private set; }
        public string DetailItemId { get; private set; }
        public string DetailVariantId { get; private set; }
        public bool IsDerivedAssetsPage { get; private set; }
        public IReadOnlyList<string> SelectedItemIds => _selectedItemIds;
        public string SelectedItemId { get; private set; }
        public string SelectedFileId { get; private set; }
        public IReadOnlyList<string> SelectedVariantIds => _selectedVariantIds;
        public string SelectedVariantId { get; private set; }
        public bool CanGoBack => _backHistory.Count > 0;
        public bool CanGoForward => _forwardHistory.Count > 0;
        public AssetManagerItemSortField ItemSortField { get; private set; } =
            AssetManagerItemSortField.Name;
        public bool IsItemSortReversed { get; private set; }
        public AssetManagerSearchTarget SearchTargets { get; private set; } =
            AssetManagerSearchTarget.All;

        public event Action<AssetManagerViewStateChange> Changed;

        public void SelectPage(AssetManagerPage page)
        {
            Navigate(new NavigationLocation(page, null, null));
        }

        public void SelectCollection(string collectionId)
        {
            Navigate(new NavigationLocation(
                AssetManagerPage.Collection,
                collectionId,
                null));
        }

        public void SelectTag(string tagPath)
        {
            if (string.IsNullOrWhiteSpace(tagPath))
            {
                SelectPage(AssetManagerPage.Tags);
                return;
            }

            Navigate(new NavigationLocation(
                AssetManagerPage.Tags,
                null,
                null,
                tagPath));
        }

        public void OpenItemDetail(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            Navigate(new NavigationLocation(
                Page == AssetManagerPage.Variants ? AssetManagerPage.Library : Page,
                CollectionId,
                itemId,
                TagPath));
        }

        public void OpenVariantDetail(string variantId)
        {
            if (string.IsNullOrWhiteSpace(variantId))
            {
                return;
            }

            Navigate(new NavigationLocation(
                Page,
                CollectionId,
                null,
                TagPath,
                variantId));
        }

        public void OpenDerivedAssetsPage(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                !string.Equals(
                    DetailItemId,
                    itemId,
                    StringComparison.Ordinal) ||
                IsDerivedAssetsPage)
            {
                return;
            }

            IsDerivedAssetsPage = true;
            SelectedFileId = null;
            Changed?.Invoke(AssetManagerViewStateChange.ItemDetailPage);
        }

        public void CloseDerivedAssetsPage()
        {
            if (!IsDerivedAssetsPage)
            {
                return;
            }

            IsDerivedAssetsPage = false;
            Changed?.Invoke(AssetManagerViewStateChange.ItemDetailPage);
        }

        public void ShowPageRoot()
        {
            Navigate(new NavigationLocation(
                Page,
                CollectionId,
                null,
                TagPath));
        }

        public void GoBack()
        {
            if (!CanGoBack)
            {
                return;
            }

            _forwardHistory.Push(_location);
            ApplyLocation(_backHistory.Pop());
        }

        public void GoForward()
        {
            if (!CanGoForward)
            {
                return;
            }

            _backHistory.Push(_location);
            ApplyLocation(_forwardHistory.Pop());
        }

        public void SelectItem(string itemId)
        {
            SelectItems(
                string.IsNullOrEmpty(itemId)
                    ? Array.Empty<string>()
                    : new[] { itemId },
                itemId);
        }

        public void SelectItems(
            IEnumerable<string> itemIds,
            string primaryItemId)
        {
            var selected = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (itemIds != null)
            {
                foreach (var itemId in itemIds)
                {
                    if (!string.IsNullOrEmpty(itemId) &&
                        unique.Add(itemId))
                    {
                        selected.Add(itemId);
                    }
                }
            }

            var nextPrimary = !string.IsNullOrEmpty(primaryItemId) &&
                              unique.Contains(primaryItemId)
                ? primaryItemId
                : selected.Count > 0
                    ? selected[selected.Count - 1]
                    : null;
            if (HasSameItemSelection(selected, nextPrimary))
            {
                return;
            }

            _selectedItemIds = selected.ToArray();
            SelectedItemId = nextPrimary;
            SelectedFileId = null;
            Changed?.Invoke(AssetManagerViewStateChange.ItemSelection);
        }

        public void SelectFile(string fileId)
        {
            IsDerivedAssetsPage = false;
            SelectedFileId = string.IsNullOrEmpty(fileId)
                ? null
                : fileId;
            Changed?.Invoke(AssetManagerViewStateChange.FileSelection);
        }

        public void SelectVariants(IEnumerable<string> ids, string primaryId)
        {
            var selected = (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var primary = selected.Contains(primaryId, StringComparer.Ordinal)
                ? primaryId : selected.LastOrDefault();
            if (SelectedVariantId == primary && _selectedVariantIds.SequenceEqual(selected))
            {
                return;
            }
            _selectedVariantIds = selected;
            SelectedVariantId = primary;
            Changed?.Invoke(AssetManagerViewStateChange.VariantSelection);
        }

        public void SetItemSortField(AssetManagerItemSortField field)
        {
            if (ItemSortField == field)
            {
                return;
            }

            ItemSortField = field;
            Changed?.Invoke(AssetManagerViewStateChange.ItemSort);
        }

        public void ToggleItemSortDirection()
        {
            IsItemSortReversed = !IsItemSortReversed;
            Changed?.Invoke(AssetManagerViewStateChange.ItemSort);
        }

        public bool IncludesSearchTarget(AssetManagerSearchTarget target)
        {
            return (SearchTargets & target) != 0;
        }

        public void SetSearchTarget(
            AssetManagerSearchTarget target,
            bool enabled)
        {
            var next = enabled
                ? SearchTargets | target
                : SearchTargets & ~target;
            if (next == SearchTargets)
            {
                return;
            }

            SearchTargets = next;
            Changed?.Invoke(AssetManagerViewStateChange.SearchTargets);
        }

        private void Navigate(NavigationLocation location)
        {
            if (_location.IsSame(location))
            {
                ResetSelectionAndInformation();
                Changed?.Invoke(AssetManagerViewStateChange.Navigation);
                return;
            }

            _backHistory.Push(_location);
            _forwardHistory.Clear();
            ApplyLocation(location);
        }

        private void ApplyLocation(NavigationLocation location)
        {
            _location = location;
            Page = location.Page;
            CollectionId = location.CollectionId;
            TagPath = location.TagPath;
            DetailItemId = location.ItemId;
            DetailVariantId = location.VariantId;
            IsDerivedAssetsPage = false;
            ResetSelectionAndInformation();
            Changed?.Invoke(AssetManagerViewStateChange.Navigation);
        }

        private void ResetSelectionAndInformation()
        {
            _selectedItemIds = Array.Empty<string>();
            SelectedItemId = null;
            SelectedFileId = null;
            _selectedVariantIds = Array.Empty<string>();
            SelectedVariantId = null;
        }

        private bool HasSameItemSelection(
            IReadOnlyList<string> selectedItemIds,
            string primaryItemId)
        {
            if (!string.Equals(
                    SelectedItemId,
                    primaryItemId,
                    StringComparison.Ordinal) ||
                _selectedItemIds.Count != selectedItemIds.Count)
            {
                return false;
            }

            for (var index = 0; index < selectedItemIds.Count; index++)
            {
                if (!string.Equals(
                        _selectedItemIds[index],
                        selectedItemIds[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private sealed class NavigationLocation
        {
            public NavigationLocation(
                AssetManagerPage page,
                string collectionId,
                string itemId,
                string tagPath = null,
                string variantId = null)
            {
                Page = page;
                CollectionId = collectionId;
                ItemId = itemId;
                TagPath = tagPath;
                VariantId = variantId;
            }

            public AssetManagerPage Page { get; }
            public string CollectionId { get; }
            public string ItemId { get; }
            public string TagPath { get; }
            public string VariantId { get; }

            public bool IsSame(NavigationLocation other)
            {
                return other != null &&
                    Page == other.Page &&
                    string.Equals(
                        CollectionId,
                        other.CollectionId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ItemId,
                        other.ItemId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        TagPath,
                        other.TagPath,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        VariantId,
                        other.VariantId,
                        StringComparison.Ordinal);
            }
        }
    }
}
