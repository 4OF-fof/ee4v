using System;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class EditorPopupWindowBackend
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo ParentField = typeof(EditorWindow).GetField("m_Parent", InstanceFlags);
        private static readonly Type ViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.View");
        private static readonly Type ContainerWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow");
        private static readonly PropertyInfo WindowProperty = ViewType?.GetProperty("window", InstanceFlags);
        private static readonly MethodInfo SetBackgroundColorMethod = ContainerWindowType?.GetMethod(
            "SetBackgroundColor",
            InstanceFlags,
            null,
            new[] { typeof(Color) },
            null);

        public static bool TryGetDesktopBounds(Vector2 screenPosition, out Rect bounds)
        {
            bounds = default(Rect);

            try
            {
                bounds = InternalEditorUtility.GetBoundsOfDesktopAtPoint(screenPosition);
                return bounds.width > 0f && bounds.height > 0f;
            }
            catch (Exception)
            {
                bounds = default(Rect);
                return false;
            }
        }

        public static bool TrySetBackgroundColor(EditorWindow window, Color color)
        {
            if (window == null || ParentField == null || WindowProperty == null || SetBackgroundColorMethod == null)
            {
                return false;
            }

            try
            {
                var parent = ParentField.GetValue(window);
                var containerWindow = parent == null ? null : WindowProperty.GetValue(parent, null);
                if (containerWindow == null)
                {
                    return false;
                }

                SetBackgroundColorMethod.Invoke(containerWindow, new object[] { color });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
