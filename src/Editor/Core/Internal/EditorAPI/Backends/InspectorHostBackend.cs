using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class InspectorHostBackend
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private static readonly Type InspectorWindowType =
            typeof(Editor).Assembly.GetType(
                "UnityEditor.InspectorWindow");
        private static readonly Type EditorElementType =
            typeof(Editor).Assembly.GetType(
                "UnityEditor.UIElements.EditorElement");
        private static readonly PropertyInfo EditorElementEditorProperty =
            EditorElementType?.GetProperty(
                "editor",
                InstanceFlags);
        private static readonly MethodInfo GetInspectedObjectsMethod =
            InspectorWindowType?.GetMethod(
                "GetInspectedObjects",
                InstanceFlags,
                null,
                Type.EmptyTypes,
                null);
        private static readonly PropertyInfo EditorsElementProperty =
            InspectorWindowType?.GetProperty(
                "editorsElement",
                InstanceFlags);
        private static readonly PropertyInfo ScrollViewportRectProperty =
            InspectorWindowType?.GetProperty(
                "scrollViewportRect",
                InstanceFlags);
        private static readonly PropertyInfo
            PreviewAndLabelElementProperty =
                InspectorWindowType?.GetProperty(
                    "previewAndLabelElement",
                    InstanceFlags);
        private static readonly PropertyInfo
            VersionControlElementProperty =
                InspectorWindowType?.GetProperty(
                    "versionControlElement",
                    InstanceFlags);

        internal static bool TryGetStates(
            out IReadOnlyList<InspectorState> states)
        {
            if (InspectorWindowType == null)
            {
                states = Array.Empty<InspectorState>();
                return false;
            }

            var result = new List<InspectorState>();
            var windows = Resources
                .FindObjectsOfTypeAll(InspectorWindowType)
                .OfType<EditorWindow>();
            foreach (var window in windows)
            {
                if (TryGetState(
                        window,
                        out var state))
                {
                    result.Add(state);
                }
            }

            states = result;
            return true;
        }

        internal static bool TryGetState(
            EditorWindow window,
            out InspectorState state)
        {
            state = null;
            if (window == null ||
                InspectorWindowType == null ||
                !InspectorWindowType.IsInstanceOfType(window) ||
                GetInspectedObjectsMethod == null ||
                EditorsElementProperty == null)
            {
                return false;
            }

            try
            {
                var objects =
                    GetInspectedObjectsMethod.Invoke(
                        window,
                        Array.Empty<object>())
                    as UnityEngine.Object[] ??
                    Array.Empty<UnityEngine.Object>();
                var editors =
                    EditorsElementProperty.GetValue(window)
                    as VisualElement;
                if (editors == null)
                {
                    return false;
                }

                state = new InspectorState(
                    window,
                    objects,
                    GetEditorTargets(editors),
                    editors,
                    GetEditorsViewportRect(window),
                    PreviewAndLabelElementProperty
                        ?.GetValue(window) as VisualElement,
                    VersionControlElementProperty
                        ?.GetValue(window) as VisualElement);
                return true;
            }
            catch (Exception)
            {
                state = null;
                return false;
            }
        }

        private static IReadOnlyList<UnityEngine.Object>
            GetEditorTargets(VisualElement editorsElement)
        {
            if (EditorElementType == null ||
                EditorElementEditorProperty == null)
            {
                return Array.Empty<UnityEngine.Object>();
            }

            var result = new List<UnityEngine.Object>();
            for (var i = 0; i < editorsElement.childCount; i++)
            {
                var editorElement = editorsElement[i];
                if (!EditorElementType.IsInstanceOfType(editorElement) ||
                    !(EditorElementEditorProperty.GetValue(editorElement)
                        is Editor editor))
                {
                    continue;
                }

                var targets = editor.targets;
                if (targets != null)
                {
                    result.AddRange(targets);
                }
            }

            return result;
        }

        private static Rect GetEditorsViewportRect(
            EditorWindow window)
        {
            return ScrollViewportRectProperty?.GetValue(window)
                is Rect rect
                ? rect
                : Rect.zero;
        }
    }
}
