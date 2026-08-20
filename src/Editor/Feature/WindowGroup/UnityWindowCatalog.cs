using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.WindowGroup
{
    internal sealed class WindowTypeOption
    {
        internal WindowTypeOption(
            string typeId,
            string displayName,
            EditorWindow window)
        {
            TypeId = typeId;
            DisplayName = displayName;
            Window = window;
        }

        internal string TypeId { get; }

        internal string DisplayName { get; }

        internal EditorWindow Window { get; }

        internal void Focus()
        {
            if (Window == null)
            {
                return;
            }

            Window.Show();
            Window.Focus();
            Window.Repaint();
        }
    }

    internal static class UnityWindowCatalog
    {
        internal static IReadOnlyList<EditorWindow> GetOpenWindows()
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => window != null)
                .ToArray();
        }

        internal static IReadOnlyList<WindowTypeOption>
            GetOpenWindowTypes()
        {
            return GetOpenWindows()
                .Where(window =>
                    !string.IsNullOrWhiteSpace(
                        window.titleContent?.text))
                .GroupBy(window =>
                    WindowTypeIdentity.GetId(window.GetType()))
                .Select(group =>
                {
                    var first = group
                        .OrderBy(
                            window => window.titleContent.text,
                            StringComparer.CurrentCultureIgnoreCase)
                        .First();
                    var type = first.GetType();
                    var title = first.titleContent.text;
                    return new WindowTypeOption(
                        group.Key,
                        string.IsNullOrWhiteSpace(title)
                            ? WindowTypeIdentity.GetDisplayName(type)
                            : title,
                        first);
                })
                .OrderBy(
                    option => option.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal static class WindowTypeIdentity
    {
        internal static string GetId(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type.FullName + ", " +
                   type.Assembly.GetName().Name;
        }

        internal static string GetDisplayName(Type type)
        {
            return GetDisplayName(type?.Name);
        }

        internal static string GetDisplayName(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return string.Empty;
            }

            var fullName = typeId.Split(',')[0];
            var separator = fullName.LastIndexOf('.');
            var name = separator >= 0
                ? fullName.Substring(separator + 1)
                : fullName;
            if (name.EndsWith(
                    "Window",
                    StringComparison.Ordinal) &&
                name.Length > "Window".Length)
            {
                name = name.Substring(
                    0,
                    name.Length - "Window".Length);
            }

            return ObjectNames.NicifyVariableName(name);
        }
    }
}
