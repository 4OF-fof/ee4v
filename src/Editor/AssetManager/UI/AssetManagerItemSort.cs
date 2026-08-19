using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    internal enum AssetManagerItemSortField
    {
        Name,
        CreatedAt,
        UpdatedAt,
        FileCount
    }

    internal static class AssetManagerItemSort
    {
        public static IReadOnlyList<AssetItem> Apply(
            IEnumerable<AssetItem> items,
            AssetManagerItemSortField field,
            bool reverse)
        {
            var source = items ?? Enumerable.Empty<AssetItem>();
            IOrderedEnumerable<AssetItem> ordered;
            switch (field)
            {
                case AssetManagerItemSortField.CreatedAt:
                    ordered = reverse
                        ? source.OrderByDescending(item => item.CreatedAt)
                        : source.OrderBy(item => item.CreatedAt);
                    break;
                case AssetManagerItemSortField.UpdatedAt:
                    ordered = reverse
                        ? source.OrderByDescending(item => item.UpdatedAt)
                        : source.OrderBy(item => item.UpdatedAt);
                    break;
                case AssetManagerItemSortField.FileCount:
                    ordered = reverse
                        ? source.OrderByDescending(GetFileCount)
                        : source.OrderBy(GetFileCount);
                    break;
                default:
                    ordered = reverse
                        ? source.OrderByDescending(
                            item => item.Name ?? string.Empty,
                            StringComparer.OrdinalIgnoreCase)
                        : source.OrderBy(
                            item => item.Name ?? string.Empty,
                            StringComparer.OrdinalIgnoreCase);
                    break;
            }

            if (field != AssetManagerItemSortField.Name)
            {
                ordered = reverse
                    ? ordered.ThenByDescending(
                        item => item.Name ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    : ordered.ThenBy(
                        item => item.Name ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase);
            }

            return (reverse
                    ? ordered.ThenByDescending(
                        item => item.Id ?? string.Empty,
                        StringComparer.Ordinal)
                    : ordered.ThenBy(
                        item => item.Id ?? string.Empty,
                        StringComparer.Ordinal))
                .ToArray();
        }

        public static IReadOnlyList<AssetFile> Apply(
            IEnumerable<AssetFile> files,
            AssetManagerItemSortField field,
            bool reverse)
        {
            var source = files ?? Enumerable.Empty<AssetFile>();
            var fileField = GetFileSortField(field);
            IOrderedEnumerable<AssetFile> ordered;
            switch (fileField)
            {
                case AssetManagerItemSortField.CreatedAt:
                    ordered = reverse
                        ? source.OrderByDescending(file => file.CreatedAt)
                        : source.OrderBy(file => file.CreatedAt);
                    break;
                case AssetManagerItemSortField.UpdatedAt:
                    ordered = reverse
                        ? source.OrderByDescending(file => file.UpdatedAt)
                        : source.OrderBy(file => file.UpdatedAt);
                    break;
                default:
                    ordered = reverse
                        ? source.OrderByDescending(
                            file => file.FileName ?? string.Empty,
                            StringComparer.OrdinalIgnoreCase)
                        : source.OrderBy(
                            file => file.FileName ?? string.Empty,
                            StringComparer.OrdinalIgnoreCase);
                    break;
            }

            if (fileField != AssetManagerItemSortField.Name)
            {
                ordered = reverse
                    ? ordered.ThenByDescending(
                        file => file.FileName ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    : ordered.ThenBy(
                        file => file.FileName ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase);
            }

            return (reverse
                    ? ordered.ThenByDescending(
                        file => file.Id ?? string.Empty,
                        StringComparer.Ordinal)
                    : ordered.ThenBy(
                        file => file.Id ?? string.Empty,
                        StringComparer.Ordinal))
                .ToArray();
        }

        public static AssetManagerItemSortField GetFileSortField(
            AssetManagerItemSortField field)
        {
            return field == AssetManagerItemSortField.FileCount
                ? AssetManagerItemSortField.Name
                : field;
        }

        private static int GetFileCount(AssetItem item)
        {
            return item.Files?.Count ?? 0;
        }
    }
}
