using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal static class HierarchyStyleInheritance
    {
        public static bool TryResolveBackgroundColor<TNode>(
            TNode self,
            Func<TNode, TNode> getParent,
            Func<TNode, HierarchyStyleValue> getStyle,
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
                    style.HasBackgroundColor)
                {
                    color = style.BackgroundColor;
                    return true;
                }

                current = getParent(current);
            }

            color = Color.clear;
            return false;
        }

        public static bool TryResolveBackgroundColor(
            IEnumerable<HierarchyStyleValue> selfToRoot,
            out Color color)
        {
            if (selfToRoot != null)
            {
                foreach (var style in selfToRoot)
                {
                    if (style != null &&
                        style.HasBackgroundColor)
                    {
                        color = style.BackgroundColor;
                        return true;
                    }
                }
            }

            color = Color.clear;
            return false;
        }
    }
}
