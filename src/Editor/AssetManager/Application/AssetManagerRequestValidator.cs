using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Domain;

namespace Ee4v.AssetManager.Application
{
    internal static class AssetManagerRequestValidator
    {
        internal static void Require(string value, string field)
        {
            Execute(() => AssetManagerRules.Require(value, field));
        }

        internal static void RequireRequest(object request, string field)
        {
            if (request == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    field + " is required.");
            }
        }

        internal static IReadOnlyList<string> NormalizeTags(
            IReadOnlyList<string> paths)
        {
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var source = paths ?? Array.Empty<string>();
            for (var i = 0; i < source.Count; i++)
            {
                var path = Execute(
                    () => AssetManagerRules.NormalizeTagPath(source[i]));
                if (seen.Add(path))
                {
                    normalized.Add(path);
                }
            }

            return normalized;
        }

        internal static IReadOnlyList<string> NormalizeIds(
            IReadOnlyList<string> ids,
            string field)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    field + " is required.");
            }

            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
            {
                Require(ids[i], field);
                var id = ids[i].Trim();
                if (seen.Add(id))
                {
                    normalized.Add(id);
                }
            }

            return normalized;
        }

        internal static IReadOnlyList<string> NormalizeOptionalIds(
            IReadOnlyList<string> ids,
            string field)
        {
            if (ids == null || ids.Count == 0)
            {
                return Array.Empty<string>();
            }

            return NormalizeIds(ids, field);
        }

        internal static void ValidateDependencyReplacement(
            IReadOnlyList<string> dependentFileIds,
            IReadOnlyList<string> dependencyFileIds,
            Func<string, IReadOnlyList<string>> getDependencies)
        {
            Execute(() => FileDependencyGraphPolicy.EnsureCanReplace(
                dependentFileIds,
                dependencyFileIds,
                getDependencies));
        }

        internal static IReadOnlyList<string> ResolveDependencyOrder(
            string fileId,
            Func<string, IReadOnlyList<string>> getDependencies)
        {
            return Execute(() =>
                FileDependencyGraphPolicy.ResolveImportOrder(
                    fileId,
                    getDependencies));
        }

        internal static IReadOnlyList<string> NormalizeTargetPaths(
            IReadOnlyList<string> paths)
        {
            var normalized = new List<string>();
            var seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var source = paths ?? Array.Empty<string>();
            for (var i = 0; i < source.Count; i++)
            {
                var path = Execute(
                    () => AssetManagerRules.NormalizeTargetPath(source[i]));
                if (seen.Add(path))
                {
                    normalized.Add(path);
                }
            }

            return normalized;
        }

        internal static IReadOnlyList<string> NormalizeAssetGuids(
            IReadOnlyList<string> guids)
        {
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var source = guids ?? Array.Empty<string>();
            for (var i = 0; i < source.Count; i++)
            {
                var guid = Execute(
                    () => AssetManagerRules.NormalizeAssetGuid(source[i]));
                if (seen.Add(guid))
                {
                    normalized.Add(guid);
                }
            }

            return normalized;
        }

        internal static void ValidateFilter(AssetFilterNode root)
        {
            Execute(() => AssetManagerRules.ValidateFilter(root));
        }

        private static void Execute(Action action)
        {
            try
            {
                action();
            }
            catch (AssetRuleException exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    exception.Message,
                    exception);
            }
        }

        private static T Execute<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (AssetRuleException exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    exception.Message,
                    exception);
            }
        }
    }
}
