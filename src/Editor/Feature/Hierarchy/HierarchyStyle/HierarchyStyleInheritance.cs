using System;
using System.Collections.Generic;
using Ee4v.ItemStyle;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal static class HierarchyStyleInheritance
    {
        public static bool TryResolveBackgroundColor<TNode>(
            TNode self,
            Func<TNode, TNode> getParent,
            Func<TNode, ItemStyleValue> getStyle,
            out Color color)
            where TNode : class
        {
            if (getParent == null)
            {
                throw new ArgumentNullException(
                    nameof(getParent));
            }

            if (getStyle == null)
            {
                throw new ArgumentNullException(
                    nameof(getStyle));
            }

            var current = self;
            while (current != null)
            {
                var style = getStyle(current);
                if (style != null &&
                    style.HasColor)
                {
                    color = style.Color;
                    return true;
                }

                current = getParent(current);
            }

            color = Color.clear;
            return false;
        }

        public static bool TryResolveBackgroundColor(
            IEnumerable<ItemStyleValue> selfToRoot,
            out Color color)
        {
            if (selfToRoot != null)
            {
                foreach (var style in selfToRoot)
                {
                    if (style != null &&
                        style.HasColor)
                    {
                        color = style.Color;
                        return true;
                    }
                }
            }

            color = Color.clear;
            return false;
        }
    }
}
