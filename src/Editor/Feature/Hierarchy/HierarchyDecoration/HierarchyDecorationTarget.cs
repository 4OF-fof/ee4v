using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ee4v.HierarchyDecoration
{
    internal sealed class HierarchyDecorationTarget
    {
        public HierarchyDecorationTarget(
            string namePrefix = null,
            Type requiredComponentType = null,
            bool? isEmpty = null,
            bool? hasChildren = null,
            bool? hasParent = null)
        {
            if (namePrefix != null &&
                string.IsNullOrWhiteSpace(namePrefix))
            {
                throw new ArgumentException(
                    "Name prefix must not be empty.",
                    nameof(namePrefix));
            }

            if (requiredComponentType != null &&
                !typeof(Component).IsAssignableFrom(
                    requiredComponentType))
            {
                throw new ArgumentException(
                    "Required component type must derive from Component.",
                    nameof(requiredComponentType));
            }

            if (namePrefix == null &&
                requiredComponentType == null &&
                !isEmpty.HasValue &&
                !hasChildren.HasValue &&
                !hasParent.HasValue)
            {
                throw new ArgumentException(
                    "At least one target condition is required.");
            }

            NamePrefix = namePrefix;
            RequiredComponentType = requiredComponentType;
            IsEmpty = isEmpty;
            HasChildren = hasChildren;
            HasParent = hasParent;
        }

        public string NamePrefix { get; }

        public Type RequiredComponentType { get; }

        public bool? IsEmpty { get; }

        public bool? HasChildren { get; }

        public bool? HasParent { get; }

        public bool Matches(
            string objectName,
            IReadOnlyList<Component> components,
            int childCount,
            bool hasParent,
            out string styleName)
        {
            styleName = objectName;
            if (NamePrefix != null)
            {
                if (objectName == null ||
                    !objectName.StartsWith(
                        NamePrefix,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                styleName = objectName.Substring(NamePrefix.Length);
            }

            if (RequiredComponentType != null &&
                !ContainsRequiredComponent(components))
            {
                return false;
            }

            if (IsEmpty.HasValue &&
                (components == null ||
                 IsEmpty.Value != IsEmptyGameObject(components)))
            {
                return false;
            }

            return (!HasChildren.HasValue ||
                    HasChildren.Value == (childCount > 0)) &&
                (!HasParent.HasValue ||
                 HasParent.Value == hasParent);
        }

        public bool TryNormalizeDuplicateName(
            string objectName,
            IReadOnlyList<Component> components,
            int childCount,
            bool hasParent,
            out string normalizedName)
        {
            normalizedName = objectName;
            if (!Matches(
                    objectName,
                    components,
                    childCount,
                    hasParent,
                    out _) ||
                string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            var suffixStart = objectName.LastIndexOf(
                " (",
                StringComparison.Ordinal);
            if (suffixStart < 0 ||
                objectName[objectName.Length - 1] != ')')
            {
                return false;
            }

            var numberText = objectName.Substring(
                suffixStart + 2,
                objectName.Length - suffixStart - 3);
            if (!int.TryParse(
                    numberText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var duplicateIndex) ||
                duplicateIndex < 1)
            {
                return false;
            }

            normalizedName = objectName.Substring(0, suffixStart);
            return true;
        }

        private bool ContainsRequiredComponent(
            IReadOnlyList<Component> components)
        {
            if (components == null)
            {
                return false;
            }

            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component != null &&
                    RequiredComponentType.IsInstanceOfType(component))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEmptyGameObject(
            IReadOnlyList<Component> components)
        {
            return components.Count == 1 &&
                components[0] != null &&
                components[0].GetType() == typeof(Transform);
        }
    }
}
