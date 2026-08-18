using System;
using System.Collections.Generic;

namespace Ee4v.ItemStyle
{
    public sealed class ItemStyleAltTrigger<T>
    {
        private T _triggeredItem;
        private bool _hasTriggered;

        public bool TryActivate(
            T item,
            bool altPressed,
            bool pointerInside)
        {
            if (!altPressed)
            {
                _hasTriggered = false;
                return false;
            }

            if (!pointerInside ||
                EqualityComparer<T>.Default.Equals(
                    item,
                    default(T)) ||
                _hasTriggered &&
                EqualityComparer<T>.Default.Equals(
                    _triggeredItem,
                    item))
            {
                return false;
            }

            _triggeredItem = item;
            _hasTriggered = true;
            return true;
        }
    }

    public static class ItemStyleSelection
    {
        public static IReadOnlyList<T> Resolve<T>(
            T hoveredItem,
            IReadOnlyList<T> selectedItems)
        {
            var comparer = EqualityComparer<T>.Default;
            if (comparer.Equals(hoveredItem, default(T)))
            {
                return Array.Empty<T>();
            }

            if (selectedItems == null || selectedItems.Count <= 1)
            {
                return new[] { hoveredItem };
            }

            var containsHovered = false;
            var unique = new List<T>();
            var visited = new HashSet<T>(comparer);
            for (var i = 0; i < selectedItems.Count; i++)
            {
                var item = selectedItems[i];
                if (comparer.Equals(item, default(T)) ||
                    !visited.Add(item))
                {
                    continue;
                }

                unique.Add(item);
                containsHovered |= comparer.Equals(
                    item,
                    hoveredItem);
            }

            return containsHovered && unique.Count > 1
                ? unique
                : new[] { hoveredItem };
        }
    }
}
