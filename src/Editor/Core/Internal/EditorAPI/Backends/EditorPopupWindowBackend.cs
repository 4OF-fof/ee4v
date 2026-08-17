using System;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class EditorPopupWindowBackend
    {
        private const string ColorPickerTypeName =
            "UnityEditor.ColorPicker";
        private const string ObjectSelectorTypeName =
            "UnityEditor.ObjectSelector";
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags =
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;
        private static readonly FieldInfo ParentField = typeof(EditorWindow).GetField("m_Parent", InstanceFlags);
        private static readonly Type EyeDropperType =
            typeof(EditorWindow).Assembly.GetType(
                "UnityEditor.EyeDropper");
        private static readonly PropertyInfo
            EyeDropperIsOpenedProperty =
                EyeDropperType?.GetProperty(
                    "IsOpened",
                    StaticFlags);
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

        public static bool TryReadScreenPixels(Rect screenRect, out Color[] pixels, out int width, out int height)
        {
            pixels = null;
            width = Mathf.CeilToInt(screenRect.width);
            height = Mathf.CeilToInt(screenRect.height);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            try
            {
                pixels = InternalEditorUtility.ReadScreenPixel(screenRect.position, width, height);
                return pixels != null && pixels.Length == width * height;
            }
            catch (Exception)
            {
                pixels = null;
                width = 0;
                height = 0;
                return false;
            }
        }

        public static bool IsTransientPicker(
            EditorWindow window)
        {
            return window != null &&
                IsTransientPickerTypeName(
                    window.GetType().FullName);
        }

        public static bool HasOpenTransientPicker()
        {
            try
            {
                var windows =
                    Resources.FindObjectsOfTypeAll<
                        EditorWindow>();
                for (var i = 0;
                     i < windows.Length;
                     i++)
                {
                    if (IsTransientPicker(windows[i]) &&
                        IsWindowOpen(windows[i]))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        public static bool IsEyeDropperOpen()
        {
            if (EyeDropperIsOpenedProperty == null)
            {
                return false;
            }

            try
            {
                var value =
                    EyeDropperIsOpenedProperty.GetValue(
                        null,
                        null);
                return value is bool isOpened &&
                    isOpened;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsWindowOpen(
            EditorWindow window)
        {
            if (window == null ||
                ParentField == null)
            {
                return false;
            }

            try
            {
                return ParentField.GetValue(window) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool IsTransientPickerTypeName(
            string fullTypeName)
        {
            return string.Equals(
                    fullTypeName,
                    ColorPickerTypeName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    fullTypeName,
                    ObjectSelectorTypeName,
                    StringComparison.Ordinal);
        }
    }
}
