using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerProjectFolderIconCandidate
    {
        public AssetManagerProjectFolderIconCandidate(
            string assetGuid,
            string assetPath,
            string itemId)
        {
            AssetGuid = assetGuid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            ItemId = itemId ?? string.Empty;
        }

        public string AssetGuid { get; }

        public string AssetPath { get; }

        public string ItemId { get; }
    }

    internal static class AssetManagerProjectFolderIconSelection
    {
        internal static IReadOnlyDictionary<string, string>
            SelectTopmost(
                IReadOnlyList<
                    AssetManagerProjectFolderIconCandidate>
                    candidates)
        {
            var ordered =
                new List<NormalizedCandidate>();
            var source = candidates ??
                         Array.Empty<
                             AssetManagerProjectFolderIconCandidate>();
            for (var i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                var path = NormalizePath(
                    candidate != null
                        ? candidate.AssetPath
                        : string.Empty);
                if (candidate == null ||
                    string.IsNullOrWhiteSpace(
                        candidate.AssetGuid) ||
                    string.IsNullOrWhiteSpace(
                        candidate.ItemId) ||
                    string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                ordered.Add(
                    new NormalizedCandidate(
                        candidate.AssetGuid,
                        path,
                        candidate.ItemId));
            }

            ordered.Sort(CompareCandidates);
            var selectedPaths = new List<string>();
            var selected =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);
            for (var i = 0; i < ordered.Count; i++)
            {
                var candidate = ordered[i];
                var isDescendant = false;
                for (var pathIndex = 0;
                     pathIndex < selectedPaths.Count;
                     pathIndex++)
                {
                    if (IsSameOrDescendant(
                            selectedPaths[pathIndex],
                            candidate.AssetPath))
                    {
                        isDescendant = true;
                        break;
                    }
                }

                if (isDescendant ||
                    selected.ContainsKey(
                        candidate.AssetGuid))
                {
                    continue;
                }

                selectedPaths.Add(candidate.AssetPath);
                selected.Add(
                    candidate.AssetGuid,
                    candidate.ItemId);
            }

            return selected;
        }

        private static int CompareCandidates(
            NormalizedCandidate left,
            NormalizedCandidate right)
        {
            var depthCompare =
                GetDepth(left.AssetPath).CompareTo(
                    GetDepth(right.AssetPath));
            if (depthCompare != 0)
            {
                return depthCompare;
            }

            var pathCompare = string.Compare(
                left.AssetPath,
                right.AssetPath,
                StringComparison.OrdinalIgnoreCase);
            return pathCompare != 0
                ? pathCompare
                : string.Compare(
                    left.AssetGuid,
                    right.AssetGuid,
                    StringComparison.Ordinal);
        }

        private static bool IsSameOrDescendant(
            string parentPath,
            string candidatePath)
        {
            if (string.Equals(
                    parentPath,
                    candidatePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return candidatePath.Length >
                   parentPath.Length &&
                   candidatePath.StartsWith(
                       parentPath,
                       StringComparison.OrdinalIgnoreCase) &&
                   candidatePath[parentPath.Length] == '/';
        }

        private static int GetDepth(string path)
        {
            var depth = 0;
            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] == '/')
                {
                    depth++;
                }
            }

            return depth;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .TrimEnd('/');
        }

        private sealed class NormalizedCandidate
        {
            public NormalizedCandidate(
                string assetGuid,
                string assetPath,
                string itemId)
            {
                AssetGuid = assetGuid;
                AssetPath = assetPath;
                ItemId = itemId;
            }

            public string AssetGuid { get; }

            public string AssetPath { get; }

            public string ItemId { get; }
        }
    }
}
