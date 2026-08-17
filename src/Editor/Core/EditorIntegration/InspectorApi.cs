using System.Collections.Generic;
using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.EditorIntegration
{
    public sealed class InspectorState
    {
        internal InspectorState(InspectorHostSnapshot snapshot)
        {
            Window = snapshot.Window;
            InspectedObjects = snapshot.InspectedObjects;
            EditorsElement = snapshot.EditorsElement;
            PreviewAndLabelElement = snapshot.PreviewAndLabelElement;
            VersionControlElement = snapshot.VersionControlElement;
        }

        public EditorWindow Window { get; }
        public IReadOnlyList<Object> InspectedObjects { get; }
        public VisualElement EditorsElement { get; }
        public VisualElement PreviewAndLabelElement { get; }
        public VisualElement VersionControlElement { get; }
    }

    public static class InspectorApi
    {
        public static bool TryGetStates(
            out IReadOnlyList<InspectorState> states)
        {
            if (!InspectorHost.TryGetSnapshots(out var snapshots))
            {
                states = new InspectorState[0];
                return false;
            }

            var result = new InspectorState[snapshots.Count];
            for (var i = 0; i < snapshots.Count; i++)
            {
                result[i] = new InspectorState(snapshots[i]);
            }

            states = result;
            return true;
        }

        public static bool TryGetState(
            EditorWindow window,
            out InspectorState state)
        {
            if (!InspectorHost.TryGetSnapshot(
                    window,
                    out var snapshot))
            {
                state = null;
                return false;
            }

            state = new InspectorState(snapshot);
            return true;
        }
    }
}
