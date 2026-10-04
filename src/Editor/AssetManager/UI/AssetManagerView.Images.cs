using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.Core.Images;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
        private void OnGridVisibleItemsChanged(
            IReadOnlyList<string> itemIds)
        {
            if (_itemGrid.parent != _content)
            {
                return;
            }

            CancelGridThumbnails();
            var pending = (itemIds ?? Array.Empty<string>())
                .Where(itemId => !_itemGrid.HasThumbnailResult(itemId))
                .ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            _ = LoadGridThumbnailsAsync(pending);
        }

        private async Task LoadGridThumbnailsAsync(
            IReadOnlyList<string> itemIds)
        {
            var cancellation = new CancellationTokenSource();
            _gridThumbnailCancellation = cancellation;
            var savedVariants = ShowsVariants && _variantManager != null
                ? _variantManager.GetVariants().ToDictionary(variant => variant.Id, StringComparer.Ordinal)
                : null;
            try
            {
                await Task.Yield();
                cancellation.Token.ThrowIfCancellationRequested();
                using (var gate = new SemaphoreSlim(
                           MaximumConcurrentGridThumbnails,
                           MaximumConcurrentGridThumbnails))
                {
                    var tasks = itemIds.Select(async itemId =>
                    {
                        await gate.WaitAsync(cancellation.Token);
                        try
                        {
                            AssetThumbnail thumbnail;
                            if (ShowsVariants)
                            {
                                thumbnail = savedVariants != null && savedVariants.TryGetValue(itemId, out var variant)
                                    ? await _variantManager.GetRevisionThumbnail(itemId, variant.HeadRevisionId, cancellation.Token)
                                    : null;
                            }
                            else
                            {
                                thumbnail = await _manager.GetThumbnail(itemId, cancellation.Token);
                            }
                            if (!ReferenceEquals(
                                    _gridThumbnailCancellation,
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
                        finally
                        {
                            gate.Release();
                        }
                    }).ToArray();
                    await Task.WhenAll(tasks);
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
                    _gridThumbnailCancellation = null;
                }

                cancellation.Dispose();
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
                        _search.Value,
                        _viewState.SearchTargets)
                };
                items = _manager.SearchItems(query).Items;
            }

            var importedItemIds =
                _viewState.Page == AssetManagerPage.Imported
                    ? GetImportedItemIdsInProject()
                    : null;
            var visibleItems = items
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Archived ||
                    item.IsArchived)
                .Where(item =>
                    _viewState.Page != AssetManagerPage.Imported ||
                    importedItemIds.Contains(item.Id))
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

        private ISet<string> GetImportedItemIdsInProject()
        {
            if (_importedItemIdsInProject != null)
            {
                return _importedItemIdsInProject;
            }

            var existingGuids = new HashSet<string>(StringComparer.Ordinal);
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var associations = _manager.GetImportedAssetAssociations();
            for (var i = 0; i < associations.Count; i++)
            {
                var association = associations[i];
                if (association == null ||
                    string.IsNullOrEmpty(association.ItemId) ||
                    string.IsNullOrEmpty(association.AssetGuid))
                {
                    continue;
                }

                if (existingGuids.Contains(association.AssetGuid) ||
                    IsImportedAssetInProject(association.AssetGuid))
                {
                    existingGuids.Add(association.AssetGuid);
                    itemIds.Add(association.ItemId);
                }
            }

            _importedItemIdsInProject = itemIds;
            return _importedItemIdsInProject;
        }

        private static bool IsImportedAssetInProject(string assetGuid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            return !string.IsNullOrEmpty(assetPath) &&
                   !AssetDatabase.IsValidFolder(assetPath) &&
                   AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null;
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
                case AssetManagerPage.Variants:
                    return I18N.Get("navigation.variants");
                case AssetManagerPage.Imported:
                    return I18N.Get("navigation.imported");
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

        private AssetThumbnailStack CreateDetailThumbnailStack(
            IReadOnlyList<string> itemIds)
        {
            var stack = new AssetThumbnailStack(_imageCache, itemIds);
            _detailThumbnailStack = stack;
            return stack;
        }

        private async Task LoadDetailThumbnailsAsync(
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
                    _thumbnailCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private async Task LoadItemOverviewThumbnailAsync(
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
                    _itemOverviewThumbnailCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private PreviewContainer CreateVariantRevisionPreview(string variantId, string revisionId)
        {
            var key = string.IsNullOrEmpty(revisionId) ? null : "variant-revision:" + revisionId;
            return CreateVariantPreview(key, key == null || _variantManager == null ? null :
                (Func<Task<AssetThumbnail>>)(() => _variantManager.GetRevisionThumbnail(variantId, revisionId)));
        }

        private PreviewContainer CreateVariantPreview(string key, Func<Task<AssetThumbnail>> load)
        {
            var preview = new PreviewContainer();
            var image = new CachedImage(_imageCache) { scaleMode = ScaleMode.ScaleToFit, userData = key };
            image.AddToClassList("ee4v-asset-manager__variant-preview-image");
            preview.Content.Add(image);
            preview.RegisterCallback<DetachFromPanelEvent>(_ => image.Dispose());
            if (key != null && load != null) { _ = LoadVariantPreviewAsync(preview, image, key, load); }
            return preview;
        }

        private void SetVariantPreview(PreviewContainer preview, string key, Func<Task<AssetThumbnail>> load)
        {
            var image = preview.Q<CachedImage>();
            if (key != null && string.Equals(image.userData as string, key, StringComparison.Ordinal) &&
                image.DisplayedTexture != null) { return; }
            image.userData = key;
            image.ClearSource();
            preview.SetHasContent(false);
            if (key != null && load != null)
            {
                _ = LoadVariantPreviewAsync(preview, image, key, load);
            }
        }

        private async Task<bool> EnsureVariantPreviewSourceAsync(string key, Func<Task<AssetThumbnail>> load,
            CancellationToken cancellation)
        {
            if (!_imageCache.HasSource(key))
            {
                if (!_variantState.VariantPreviewLoads.TryGetValue(key, out var pending))
                {
                    pending = load();
                    _variantState.VariantPreviewLoads.Add(key, pending);
                }
                var thumbnail = await pending;
                if (_disposed || cancellation.IsCancellationRequested) { return false; }
                _imageCache.SetSource(key, thumbnail != null && thumbnail.Found ? thumbnail.Data : null);
            }
            return !_disposed && !cancellation.IsCancellationRequested;
        }

        private async Task LoadVariantPreviewAsync(PreviewContainer preview, CachedImage image,
            string key, Func<Task<AssetThumbnail>> load)
        {
            var cancellation = _variantState.VariantPreviewCancellation?.Token ?? CancellationToken.None;
            try
            {
                if (!await EnsureVariantPreviewSourceAsync(key, load, cancellation) ||
                    !string.Equals(image.userData as string, key, StringComparison.Ordinal)) { return; }
                image.SetSource(key);
                preview.SetHasContent(image.DisplayedTexture != null);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!_disposed && !cancellation.IsCancellationRequested) { Debug.LogException(exception); }
                _variantState.VariantPreviewLoads.Remove(key);
            }
        }

        private async Task LoadDerivedVariantThumbnailAsync(AssetItemGridCard card, DerivedAssetInfo variant)
        {
            var saved = _variantManager?.GetVariants().FirstOrDefault(candidate => candidate.Id == variant.VariantId);
            if (saved == null) { return; }
            var cancellation = _variantState.VariantPreviewCancellation?.Token ?? CancellationToken.None;
            var key = "variant-revision:" + saved.HeadRevisionId;
            try
            {
                if (!_variantState.VariantPreviewLoads.TryGetValue(key, out var pending))
                {
                    pending = _variantManager.GetRevisionThumbnail(variant.VariantId, saved.HeadRevisionId);
                    _variantState.VariantPreviewLoads.Add(key, pending);
                }
                var thumbnail = await pending;
                if (_disposed || cancellation.IsCancellationRequested) { return; }
                _imageCache.SetSource(variant.AssetPath, thumbnail.Data);
                card.SetState(new AssetItemGridEntry(variant.AssetPath, variant.Name), selected: false);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!_disposed && !cancellation.IsCancellationRequested) { Debug.LogException(exception); }
                _variantState.VariantPreviewLoads.Remove(key);
            }
        }

    }
}
