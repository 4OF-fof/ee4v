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
            Visit(
                rootFileId,
                getDependencies,
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                order);
            return order;
        }

        internal static void EnsureCanReplace(
            IReadOnlyList<string> dependentFileIds,
            IReadOnlyList<string> dependencyFileIds,
            Func<string, IReadOnlyList<string>> getDependencies)
        {
            if (getDependencies == null)
            {
                throw new ArgumentNullException(nameof(getDependencies));
            }

            var dependents = new HashSet<string>(
                dependentFileIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var replacements = dependencyFileIds ?? Array.Empty<string>();
            foreach (var dependentFileId in dependents)
            {
                ResolveImportOrder(
                    dependentFileId,
                    fileId => dependents.Contains(fileId)
                        ? replacements
                        : getDependencies(fileId));
            }
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
                throw new AssetRuleException(
                    "File dependency cycles are not allowed.");
            }

            var dependencies = getDependencies(fileId) ??
                               Array.Empty<string>();
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
