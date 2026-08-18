using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectExclusionRules
    {
        public static readonly HiddenObjectExclusionRules None =
            new HiddenObjectExclusionRules(null, null);

        public HiddenObjectExclusionRules(
            IReadOnlyList<string> scenePatterns,
            IReadOnlyList<string> objectPatterns)
        {
            ScenePatterns = scenePatterns ?? Array.Empty<string>();
            ObjectPatterns = objectPatterns ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> ScenePatterns { get; }

        public IReadOnlyList<string> ObjectPatterns { get; }
    }

    internal interface IHiddenObjectExclusionSource
    {
        HiddenObjectExclusionRules Load();
    }

    internal static class HiddenObjectExclusionPolicy
    {
        private static readonly char[] PatternSeparators =
        {
            '\r',
            '\n',
            ',',
            ';'
        };

        public static IReadOnlyList<string> ParsePatterns(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(
                    PatternSeparators,
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(pattern => pattern.Trim())
                .Where(pattern => pattern.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<HiddenObjectSnapshotItem> Apply(
            IReadOnlyList<HiddenObjectSnapshotItem> snapshot,
            HiddenObjectExclusionRules rules)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                return Array.Empty<HiddenObjectSnapshotItem>();
            }

            rules = rules ?? HiddenObjectExclusionRules.None;
            var excludedSceneHandles = new HashSet<int>(
                snapshot
                    .Where(item => MatchesAny(
                        item.SceneName,
                        rules.ScenePatterns))
                    .Select(item => item.SceneHandle));
            var excludedInstanceIds = new HashSet<int>(
                snapshot
                    .Where(item =>
                        !excludedSceneHandles.Contains(item.SceneHandle) &&
                        MatchesAny(item.Name, rules.ObjectPatterns))
                    .Select(item => item.InstanceId));

            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = 0; i < snapshot.Count; i++)
                {
                    var item = snapshot[i];
                    if (excludedSceneHandles.Contains(item.SceneHandle) ||
                        excludedInstanceIds.Contains(item.InstanceId) ||
                        !excludedInstanceIds.Contains(
                            item.ParentInstanceId))
                    {
                        continue;
                    }

                    changed |= excludedInstanceIds.Add(item.InstanceId);
                }
            }

            return snapshot
                .Where(item =>
                    !excludedSceneHandles.Contains(item.SceneHandle) &&
                    !excludedInstanceIds.Contains(item.InstanceId))
                .ToArray();
        }

        internal static bool Matches(string value, string pattern)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            var valueIndex = 0;
            var patternIndex = 0;
            var starIndex = -1;
            var retryValueIndex = 0;
            while (valueIndex < value.Length)
            {
                if (patternIndex < pattern.Length &&
                    (pattern[patternIndex] == '?' ||
                     char.ToUpperInvariant(pattern[patternIndex]) ==
                     char.ToUpperInvariant(value[valueIndex])))
                {
                    patternIndex++;
                    valueIndex++;
                    continue;
                }

                if (patternIndex < pattern.Length &&
                    pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex++;
                    retryValueIndex = valueIndex;
                    continue;
                }

                if (starIndex < 0)
                {
                    return false;
                }

                patternIndex = starIndex + 1;
                valueIndex = ++retryValueIndex;
            }

            while (patternIndex < pattern.Length &&
                   pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        private static bool MatchesAny(
            string value,
            IReadOnlyList<string> patterns)
        {
            if (patterns == null)
            {
                return false;
            }

            for (var i = 0; i < patterns.Count; i++)
            {
                if (Matches(value, patterns[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
