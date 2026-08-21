using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.SceneSwitcher
{
    internal static class SceneSwitcherHierarchyTrigger
    {
        private const float AnchorX = 48f;
        private const float RightInset = 46f;
        private const float AnchorHeight = 16f;

        public static void Draw(ItemInjectionContext context)
        {
            if (!context.IsHierarchySceneHeader)
            {
                return;
            }

            var hitRect = GetAnchorRect(
                context.SelectionRect,
                EditorGUIUtility.currentViewWidth);
            if (hitRect.width <= 0f)
            {
                return;
            }

            var tooltip = UiTextFactory.CreateGuiContent();
            tooltip.tooltip = I18N.Get("hierarchy.tooltip");
            GUI.Label(hitRect, tooltip);

            var current = Event.current;
            if (current.type != EventType.MouseDown ||
                current.button != 0 ||
                !hitRect.Contains(current.mousePosition))
            {
                return;
            }

            var screenPosition =
                GUIUtility.GUIToScreenPoint(hitRect.position);
            SceneSwitcherWindow.ShowAt(
                new Rect(screenPosition, hitRect.size),
                context.HierarchyScene.handle,
                SceneSwitcherBootstrap.Controller);
            current.Use();
        }

        internal static Rect GetAnchorRect(
            Rect selectionRect,
            float currentViewWidth)
        {
            var contentRight = selectionRect.xMax > 0f
                ? Mathf.Min(
                    currentViewWidth,
                    selectionRect.xMax)
                : currentViewWidth;
            return new Rect(
                AnchorX,
                selectionRect.y,
                Mathf.Max(
                    0f,
                    contentRight - AnchorX - RightInset),
                AnchorHeight);
        }
    }
}
