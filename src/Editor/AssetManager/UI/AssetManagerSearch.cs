using System;
using System.Linq;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    [Flags]
    internal enum AssetManagerSearchTarget
    {
        None = 0,
        Name = 1 << 0,
        Description = 1 << 1,
        Tags = 1 << 2,
        All = Name | Description | Tags
    }

    internal static class AssetManagerSearch
    {
        public static bool MatchesItem(
            AssetItem item,
            string search,
            AssetManagerSearchTarget targets)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            if (item == null)
            {
                return false;
            }

            var value = search.Trim();
            return (HasTarget(targets, AssetManagerSearchTarget.Name) &&
                    Contains(item.Name, value)) ||
                (HasTarget(
                     targets,
                     AssetManagerSearchTarget.Description) &&
                 Contains(item.Description, value)) ||
                (HasTarget(targets, AssetManagerSearchTarget.Tags) &&
                 item.Tags != null &&
                 item.Tags.Any(tag => Contains(tag?.Path, value)));
        }

        public static bool MatchesFile(
            AssetFile file,
            string search,
            AssetManagerSearchTarget targets)
        {
            return string.IsNullOrWhiteSpace(search) ||
                file != null &&
                HasTarget(targets, AssetManagerSearchTarget.Name) &&
                Contains(file.FileName, search.Trim());
        }

        public static bool MatchesVariant(
            DerivedAssetInfo variant,
            string search,
            AssetManagerSearchTarget targets)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }
            return variant != null &&
                ((HasTarget(targets, AssetManagerSearchTarget.Name) &&
                  Contains(variant.Name, search.Trim())) ||
                 (HasTarget(targets, AssetManagerSearchTarget.Description) &&
                  Contains(variant.Description, search.Trim())));
        }

        public static AssetFilterNode BuildBackendFilter(
            string search,
            AssetManagerSearchTarget targets)
        {
            if (string.IsNullOrWhiteSpace(search) ||
                HasTarget(targets, AssetManagerSearchTarget.Tags))
            {
                return null;
            }

            var value = search.Trim();
            var filters = new[]
                {
                    HasTarget(targets, AssetManagerSearchTarget.Name)
                        ? AssetFilterNode.Condition(
                            AssetFilterConditionType.NameContains,
                            value)
                        : null,
                    HasTarget(targets, AssetManagerSearchTarget.Description)
                        ? AssetFilterNode.Condition(
                            AssetFilterConditionType.DescriptionContains,
                            value)
                        : null
                }
                .Where(filter => filter != null)
                .ToArray();
            if (filters.Length == 0)
            {
                return null;
            }

            return filters.Length == 1
                ? filters[0]
                : AssetFilterNode.Or(filters);
        }

        private static bool HasTarget(
            AssetManagerSearchTarget targets,
            AssetManagerSearchTarget target)
        {
            return (targets & target) != 0;
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
