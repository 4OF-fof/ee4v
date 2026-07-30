using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Domain
{
    internal static class FileDependencyGraphPolicy
    {
        internal static IReadOnlyList<string> ResolveImportOrder(
            string rootFileId,
            Func<string, IReadOnlyList<string>> getDependencies)
        {
            if (getDependencies == null)
            {
                throw new ArgumentNullException(nameof(getDependencies));
            }

            var order = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            Visit(rootFileId, getDependencies, visiting, visited, order);
            return order;
        }

        internal static void EnsureCanReplace(
            string sourceFileId,
            IReadOnlyList<string> targetFileIds,
            Func<string, IReadOnlyList<string>> getDependencies)
        {
            if (getDependencies == null)
            {
                throw new ArgumentNullException(nameof(getDependencies));
            }

            var targets = targetFileIds ?? Array.Empty<string>();
            ResolveImportOrder(
                sourceFileId,
                fileId =>
                    string.Equals(
                        fileId,
                        sourceFileId,
                        StringComparison.Ordinal)
                        ? targets
                        : getDependencies(fileId));
        }

        private static void Visit(
            string fileId,
            Func<string, IReadOnlyList<string>> getDependencies,
            ISet<string> visiting,
            ISet<string> visited,
            ICollection<string> order)
        {
            if (visited.Contains(fileId))
            {
                return;
            }

            if (!visiting.Add(fileId))
            {
                throw new CatalogRuleException(
                    CatalogRuleError.DependencyCycle);
            }

            var dependencies =
                getDependencies(fileId) ?? Array.Empty<string>();
            for (var i = 0; i < dependencies.Count; i++)
            {
                Visit(
                    dependencies[i],
                    getDependencies,
                    visiting,
                    visited,
                    order);
            }

            visiting.Remove(fileId);
            visited.Add(fileId);
            order.Add(fileId);
        }
    }
}
