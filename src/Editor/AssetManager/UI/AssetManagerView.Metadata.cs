using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
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

        private void SaveVariantMetadataAutomatically(DerivedAssetInfo variant, string name,
            string description, VisualElement error)
        {
            if (_variantManager == null || _variantState.VariantBusy) { return; }
            if (_variantState.VariantMetadataSaveCount == 0 && variant.Name == name &&
                (variant.Description ?? string.Empty) == description) { return; }
            var queue = VariantMetadataSaveQueues.GetValue(_variantManager, _ => new VariantMetadataSaveQueue());
            var previousSave = queue.Pending;
            _variantState.VariantMetadataSaveCount++;
            _variantState.VariantMetadataSaveTask = SaveVariantMetadataAsync(previousSave, variant,
                new UpdateAssetVariantRequest { Name = name, Description = description }, error);
            queue.Pending = _variantState.VariantMetadataSaveTask;
        }

        private Task PendingVariantMetadataSave => _variantManager == null ? Task.CompletedTask :
            VariantMetadataSaveQueues.GetValue(_variantManager, _ => new VariantMetadataSaveQueue()).Pending;

        private async Task SaveVariantMetadataAsync(Task previousSave, DerivedAssetInfo variant,
            UpdateAssetVariantRequest request, VisualElement error)
        {
            try
            {
                await previousSave;
                var current = GetVariants().FirstOrDefault(candidate => candidate.VariantId == variant.VariantId);
                if (current == null || (current.Name == request.Name &&
                    (current.Description ?? string.Empty) == request.Description)) { return; }
                await _variantManager.UpdateMetadata(variant.VariantId, request);
                variant.Name = request.Name;
                variant.Description = request.Description;
                if (variant.Prefab != null) { variant.AssetPath = AssetDatabase.GetAssetPath(variant.Prefab); }
                error.Clear();
            }
            catch (Exception exception)
            {
                error.Clear();
                error.Add(UiTextFactory.CreateHelpBox(exception.Message, HelpBoxMessageType.Error));
                Debug.LogException(exception);
            }
            finally
            {
                _variantState.VariantMetadataSaveCount--;
                if (!_disposed && _variantState.VariantMetadataSaveCount == 0)
                {
                    if (ShowsMain)
                    {
                        if (string.IsNullOrEmpty(_viewState.DetailVariantId)) { RefreshMain(); }
                        else
                        {
                            if (_viewState.DetailVariantId == variant.VariantId)
                            {
                                _content.Q<UiTextElement>(className: "ee4v-asset-manager__variant-overview-title")
                                    ?.SetText(variant.Name);
                                var description = _content.Q<UiTextElement>(
                                    className: "ee4v-asset-manager__variant-overview-description");
                                if (description != null)
                                {
                                    description.SetText(variant.Description);
                                    description.tooltip = variant.Description;
                                    description.parent.style.display = string.IsNullOrWhiteSpace(variant.Description)
                                        ? DisplayStyle.None : DisplayStyle.Flex;
                                }
                            }
                            RefreshHistoryNavigation();
                        }
                    }
                    if (ShowsInformation &&
                        (_viewState.DetailVariantId ?? _viewState.SelectedVariantId) != variant.VariantId)
                    {
                        RefreshDetail();
                    }
                }
            }
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
                .Where(tag => !GetSourceTagPaths(item).Contains(
                    tag, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedTags.SequenceEqual(
                    GetEditableTagPaths(item),
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

        private static IReadOnlyList<string> GetSourceTagPaths(
            AssetItem item)
        {
            return (item?.Tags ?? Array.Empty<AssetTag>())
                .Where(tag => tag != null && tag.IsSourceOwned)
                .Select(tag => tag.Path)
                .ToArray();
        }

        private static IReadOnlyList<string> GetEditableTagPaths(
            AssetItem item)
        {
            return (item?.Tags ?? Array.Empty<AssetTag>())
                .Where(tag => tag != null && !tag.IsSourceOwned)
                .Select(tag => tag.Path)
                .ToArray();
        }

    }
}
