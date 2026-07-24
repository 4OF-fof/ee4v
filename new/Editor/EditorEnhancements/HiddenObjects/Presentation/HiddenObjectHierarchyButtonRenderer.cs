using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.UI;
using UnityEngine;

namespace Ee4v.HiddenObjects
{
    internal static class HiddenObjectHierarchyButtonRenderer
    {
        private const float ButtonWidth = 22f;
        private const float IconSize = 16f;

        public static void Draw(ItemInjectionContext context)
        {
            if (!context.IsHierarchySceneHeader)
            {
                return;
            }

            var buttonRect = new Rect(
                context.SelectionRect.xMax - ButtonWidth,
                context.SelectionRect.y,
                ButtonWidth,
                context.SelectionRect.height);
            var iconRect = new Rect(
                buttonRect.x + (buttonRect.width - IconSize) * 0.5f,
                buttonRect.y + (buttonRect.height - IconSize) * 0.5f,
                IconSize,
                IconSize);

            if (Event.current.type == EventType.Repaint &&
                UiBuiltinIconResolver.TryResolve(
                    UiBuiltinIcon.VisibilityHidden,
                    out var texture))
            {
                GUI.DrawTexture(
                    iconRect,
                    texture,
                    ScaleMode.ScaleToFit,
                    true);
            }

            GUI.Label(
                buttonRect,
                new GUIContent(
                    string.Empty,
                    I18N.Get("hierarchyButton.tooltip")));

            if (Event.current.type != EventType.MouseDown ||
                Event.current.button != 0 ||
                !buttonRect.Contains(Event.current.mousePosition))
            {
                return;
            }

            HiddenObjectsWindow.OpenForScene(
                context.HierarchyScene.handle);
            Event.current.Use();
        }
    }
}
