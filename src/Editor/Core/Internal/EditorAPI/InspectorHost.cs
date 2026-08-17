using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Internal.EditorAPI
{
    internal sealed class InspectorHostSnapshot
    {
        internal InspectorHostSnapshot(
            EditorWindow window,
            IReadOnlyList<Object> inspectedObjects,
            VisualElement editorsElement,
            VisualElement previewAndLabelElement,
            VisualElement versionControlElement)
        {
            Window = window;
            InspectedObjects = inspectedObjects;
            EditorsElement = editorsElement;
            PreviewAndLabelElement = previewAndLabelElement;
            VersionControlElement = versionControlElement;
        }

        internal EditorWindow Window { get; }
        internal IReadOnlyList<Object> InspectedObjects { get; }
        internal VisualElement EditorsElement { get; }
        internal VisualElement PreviewAndLabelElement { get; }
        internal VisualElement VersionControlElement { get; }
    }

    internal static class InspectorHost
    {
        internal static bool TryGetSnapshots(
            out IReadOnlyList<InspectorHostSnapshot> snapshots)
        {
            return InspectorHostBackend.TryGetSnapshots(
                out snapshots);
        }

        internal static bool TryGetSnapshot(
            EditorWindow window,
            out InspectorHostSnapshot snapshot)
        {
            return InspectorHostBackend.TryGetSnapshot(
                window,
                out snapshot);
        }
    }
}
