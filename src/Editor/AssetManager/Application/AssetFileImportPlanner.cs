using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetFileImportPlan
    {
        internal string FileId { get; set; }
        internal IReadOnlyList<string> TargetPaths { get; set; }
    }

    internal static class AssetFileImportPlanner
    {
        internal static IReadOnlyList<AssetFileImportPlan> Resolve(
            IEnumerable<AssetFileTarget> selectedTargets,
            Func<string, IReadOnlyList<AssetFileDependency>> getDependencies)
        {
            var selected = selectedTargets.ToArray();
            var dependencies = new Dictionary<string, IReadOnlyList<AssetFileDependency>>(
                StringComparer.Ordinal);
            IReadOnlyList<AssetFileDependency> ReadDependencies(string fileId)
            {
                if (!dependencies.TryGetValue(fileId, out var result))
                {
                    result = getDependencies(fileId);
                    dependencies.Add(fileId, result);
                }
                return result;
            }

            var targetPaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var pendingTargets = new Queue<AssetFileTarget>(selected);
            var visitedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pendingTargets.Count > 0)
            {
                var target = pendingTargets.Dequeue();
                if (!visitedTargets.Add(target.FileId + "\n" + target.TargetPath))
                {
                    continue;
                }
                if (!targetPaths.TryGetValue(target.FileId, out var paths))
                {
                    paths = new List<string>();
                    targetPaths.Add(target.FileId, paths);
                }
                paths.Add(target.TargetPath);
                foreach (var dependency in ReadDependencies(target.FileId).Where(dependency =>
                             string.Equals(dependency.DependentTargetPath, target.TargetPath,
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    pendingTargets.Enqueue(new AssetFileTarget
                    {
                        FileId = dependency.DependencyFileId,
                        TargetPath = dependency.TargetPath
                    });
                }
            }

            var order = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fileId in selected.Select(target => target.FileId).Distinct(StringComparer.Ordinal))
            {
                foreach (var dependencyFileId in AssetManagerRequestValidator.ResolveDependencyOrder(
                             fileId, id => ReadDependencies(id)
                                 .Select(dependency => dependency.DependencyFileId)
                                 .Where(dependencyId => !string.Equals(dependencyId, id, StringComparison.Ordinal))
                                 .Distinct(StringComparer.Ordinal).ToArray()))
                {
                    if (seen.Add(dependencyFileId))
                    {
                        order.Add(dependencyFileId);
                    }
                }
            }

            return order.Select(fileId => new AssetFileImportPlan
            {
                FileId = fileId,
                TargetPaths = targetPaths.TryGetValue(fileId, out var paths)
                    ? (IReadOnlyList<string>)paths
                    : Array.Empty<string>()
            }).ToArray();
        }
    }
}
