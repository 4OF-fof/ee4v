using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ee4v.PlayModeComponentSuppression
{
    internal static class ComponentTypeIdentity
    {
        private static readonly char[] EntrySeparators = { '\r', '\n' };

        internal static string Create(Type type)
        {
            if (!IsSelectable(type))
            {
                throw new ArgumentException(
                    "A concrete MonoBehaviour type is required.",
                    nameof(type));
            }

            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        internal static bool IsSelectable(Type type)
        {
            return type != null &&
                   !type.IsAbstract &&
                   !type.ContainsGenericParameters &&
                   typeof(MonoBehaviour).IsAssignableFrom(type);
        }

        internal static IReadOnlyList<string> Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        internal static string Serialize(IEnumerable<string> identities)
        {
            return string.Join(
                "\n",
                (identities ?? Array.Empty<string>())
                    .Where(identity => !string.IsNullOrWhiteSpace(identity))
                    .Select(identity => identity.Trim())
                    .Distinct(StringComparer.Ordinal));
        }

        internal static Type Resolve(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                return null;
            }

            var type = Type.GetType(identity.Trim(), false);
            return IsSelectable(type) ? type : null;
        }

        internal static IReadOnlyCollection<Type> ResolveAll(string value)
        {
            var types = new HashSet<Type>();
            foreach (var identity in Parse(value))
            {
                var type = Resolve(identity);
                if (type != null)
                {
                    types.Add(type);
                }
            }

            return types;
        }
    }
}
