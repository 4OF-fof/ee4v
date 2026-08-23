using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.EditorIntegration
{
    public sealed class InspectorState
    {
        internal InspectorState(
            EditorWindow window,
            IReadOnlyList<Object> inspectedObjects,
            IReadOnlyList<Object> editorTargets,
            VisualElement editorsElement,
            Rect editorsViewportRect,
            VisualElement previewAndLabelElement,
            VisualElement versionControlElement)
        {
            Window = window;
            InspectedObjects = inspectedObjects;
            EditorTargets = editorTargets;
            EditorsElement = editorsElement;
            EditorsViewportRect = editorsViewportRect;
            PreviewAndLabelElement = previewAndLabelElement;
            VersionControlElement = versionControlElement;
        }

        public EditorWindow Window { get; }
        public IReadOnlyList<Object> InspectedObjects { get; }
        public IReadOnlyList<Object> EditorTargets { get; }
        public VisualElement EditorsElement { get; }
        public Rect EditorsViewportRect { get; }
        public VisualElement PreviewAndLabelElement { get; }
        public VisualElement VersionControlElement { get; }
    }

    public static class InspectorApi
    {
        public static bool TryGetStates(
            out IReadOnlyList<InspectorState> states)
        {
            return InspectorHostBackend.TryGetStates(out states);
        }

        public static bool TryGetState(
            EditorWindow window,
            out InspectorState state)
        {
            return InspectorHostBackend.TryGetState(window, out state);
        }
    }
}
