using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI
{
    internal static class EditorPopupWindow
    {
        public static bool TryGetDesktopBounds(Vector2 screenPosition, out Rect bounds)
        {
            return EditorPopupWindowBackend.TryGetDesktopBounds(screenPosition, out bounds);
        }

        public static bool TrySetBackgroundColor(EditorWindow window, Color color)
        {
            return EditorPopupWindowBackend.TrySetBackgroundColor(window, color);
        }
    }
}
