using System.Collections.Generic;
using Ee4v.Core;
using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyDecoration
{
    internal sealed class HierarchyDecorationRenderer
    {
        private const float RowLeft = 32f;
        private const float LineHeight = 1f;

        private static readonly Color DarkBackground =
            new Color32(56, 56, 56, 255);
        private static readonly Color LightBackground =
            new Color32(200, 200, 200, 255);

        private readonly List<Component> _componentBuffer =
            new List<Component>();

        public void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                !context.IsHierarchyGameObject ||
                !(context.Target is GameObject gameObject) ||
                Event.current == null ||
                Event.current.type != EventType.Repaint ||
                !IsSeparator(gameObject))
            {
                return;
            }

            var backgroundRect = GetBackgroundRect(
                context.SelectionRect,
                EditorGUIUtility.currentViewWidth);
            EditorGUI.DrawRect(
                backgroundRect,
                ResolveBackgroundColor(
                    EditorGUIUtility.isProSkin));
            EditorGUI.DrawRect(
                GetLineRect(backgroundRect),
                ResolveLineColor(
                    EditorGUIUtility.isProSkin));
        }

        internal static Rect GetBackgroundRect(
            Rect selectionRect,
            float viewWidth)
        {
            return new Rect(
                RowLeft,
                selectionRect.y,
                Mathf.Max(0f, viewWidth - RowLeft),
                selectionRect.height);
        }

        internal static Rect GetLineRect(Rect backgroundRect)
        {
            return new Rect(
                backgroundRect.x,
                Mathf.Floor(
                    backgroundRect.y +
                    (backgroundRect.height - LineHeight) *
                    0.5f),
                backgroundRect.width,
                LineHeight);
        }

        internal static Color ResolveBackgroundColor(
            bool isProSkin)
        {
            return isProSkin
                ? DarkBackground
                : LightBackground;
        }

        internal static Color ResolveLineColor(bool isProSkin)
        {
            return EditorThemeColors.ResolveHierarchyDepthLine(
                isProSkin);
        }

        private bool IsSeparator(GameObject gameObject)
        {
            if (!string.Equals(
                    gameObject.name,
                    HierarchyDecorationRules.SeparatorName,
                    System.StringComparison.Ordinal))
            {
                return false;
            }

            _componentBuffer.Clear();
            gameObject.GetComponents(_componentBuffer);
            return HierarchyDecorationRules.IsSeparator(
                gameObject.name,
                _componentBuffer.Count,
                gameObject.transform.childCount);
        }
    }
}
