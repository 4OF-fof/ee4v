using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.AvatarModify.Domain
{
    public sealed class PrefabCandidate
    {
        public PrefabCandidate(
            string assetGuid,
            string assetPath,
            bool hasAvatarDescriptor)
        {
            AssetGuid = assetGuid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            HasAvatarDescriptor = hasAvatarDescriptor;
        }

        public string AssetGuid { get; }
        public string AssetPath { get; }
        public bool HasAvatarDescriptor { get; }
    }

    public static class AvatarSelectionPolicy
    {
        public static IReadOnlyList<PrefabCandidate> OrderCandidates(
            IReadOnlyList<PrefabCandidate> candidates)
        {
            return (candidates ?? Array.Empty<PrefabCandidate>())
                .Where(candidate => candidate != null)
                .OrderByDescending(candidate => candidate.HasAvatarDescriptor)
                .ThenBy(candidate => candidate.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string SelectAutomatically(
            IReadOnlyList<PrefabCandidate> candidates)
        {
            var ordered = OrderCandidates(candidates);
            var descriptors = ordered
                .Where(candidate => candidate.HasAvatarDescriptor)
                .ToArray();
            if (descriptors.Length == 1)
            {
                return descriptors[0].AssetGuid;
            }

            return ordered.Count == 1
                ? ordered[0].AssetGuid
                : string.Empty;
        }
    }

}
