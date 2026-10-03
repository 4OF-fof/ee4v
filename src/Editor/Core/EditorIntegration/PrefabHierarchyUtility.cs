using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ee4v.Core.EditorIntegration
{
    public static class PrefabHierarchyUtility
    {
        public static IReadOnlyCollection<string> SplitName(string name)
        {
            var words = new List<string>();
            var start = -1;
            for (var index = 0; index <= name.Length; index++)
            {
                var end = index == name.Length ||
                    !char.IsLetterOrDigit(name[index]);
                var camelBreak = !end && start >= 0 &&
                    char.IsUpper(name[index]) &&
                    char.IsLower(name[index - 1]);
                if ((end || camelBreak) && start >= 0)
                {
                    words.Add(name.Substring(start, index - start)
                        .ToLowerInvariant());
                    start = -1;
                }
                if (!end && start < 0)
                {
                    start = index;
                }
            }
            return words;
        }

        public static bool IsInScope(
            Transform target,
            Transform root,
            int? selectedSiblingIndex,
            IReadOnlyCollection<int> prefabSiblingIndices)
        {
            if (root == null || target == null ||
                (target != root && !target.IsChildOf(root)))
            {
                return false;
            }
            if (!selectedSiblingIndex.HasValue)
            {
                return true;
            }
            if (selectedSiblingIndex.Value >= 0)
            {
                var index = selectedSiblingIndex.Value;
                if (index >= root.childCount)
                {
                    return false;
                }
                var selected = root.GetChild(index);
                return target == selected || target.IsChildOf(selected);
            }

            var current = target;
            while (current.parent != null && current.parent != root)
            {
                current = current.parent;
            }
            return current.parent != root ||
                   !prefabSiblingIndices.Contains(current.GetSiblingIndex());
        }
    }
}
